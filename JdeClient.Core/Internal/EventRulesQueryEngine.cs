using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JdeClient.Core.Exceptions;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using JdeClient.Core.XmlEngine;
using static JdeClient.Core.Interop.JdeStructures;
using static JdeClient.Core.Interop.JdeKernelApi;

namespace JdeClient.Core.Internal;

/// <summary>
/// Loads event rules metadata, XML, and decoded lines from JDE spec data.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class EventRulesQueryEngine : IEventRulesQueryEngine
{
    private readonly HUSER _hUser;
    private readonly JdeClientOptions _options;
    private const int ProductTypeFda = 1;
    private const int ProductTypeRda = 2;
    private const int ProductTypeNer = 3;
    private const int ProductTypeTer = 4;
    private const int SpecKeyBusFuncByObject = 2;
    private const int SpecKeyDataStructureByTemplate = 1;
    private const int MaxBusinessFunctionPayloadBytes = 32 * 1024 * 1024;
    private const int MaxArchiveExtractionDepth = 3;
    private const string CentralObjectsDataSourcePrefix = "Central Objects - ";
    private static readonly byte[] ZipLocalFileHeaderSignature = { 0x50, 0x4B, 0x03, 0x04 };
    private static readonly byte[] ZipCentralDirectoryHeaderSignature = { 0x50, 0x4B, 0x01, 0x02 };
    private static readonly byte[] ZipEndOfCentralDirectorySignature = { 0x50, 0x4B, 0x05, 0x06 };
    private readonly record struct BusinessFunctionSpecDetails(string FunctionName, string TemplateName, string EventSpecKey);

    /// <summary>
    /// Create a new query engine bound to a JDE user handle.
    /// </summary>
    public EventRulesQueryEngine(HUSER hUser, JdeClientOptions options)
    {
        _hUser = hUser;
        _options = options;
    }

    /// <summary>
    /// Build an event rules tree for a business function (BSFN).
    /// </summary>
    public JdeEventRulesNode GetBusinessFunctionTree(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Object name is required.", nameof(objectName));
        }
        var children = new List<JdeEventRulesNode>();
        TableLayout? layout = _options.UseRowLayoutTables
            ? TableLayoutLoader.Load(F9862Structures.TableName)
            : null;
        IntPtr rowBuffer = IntPtr.Zero;
        HREQUEST hRequest = OpenTable(F9862Structures.TableName, new ID(F9862Structures.IdObjectNameFunctionName));
        try
        {
            var key = new F9862Structures.Key1
            {
                ObjectName = objectName,
                FunctionName = string.Empty
            };
            int keySize = Marshal.SizeOf<F9862Structures.Key1>();
            IntPtr keyPtr = Marshal.AllocHGlobal(keySize);
            try
            {
                Marshal.StructureToPtr(key, keyPtr, false);
                // Filter rows by object name using the F9862 key structure.
                int keyResult = JDB_SelectKeyed(hRequest, new ID(F9862Structures.IdObjectNameFunctionName), keyPtr, 1);
                if (keyResult != JDEDB_PASSED)
                {
                    return BuildRootNode(objectName, children);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(keyPtr);
            }
            // Allocate a row buffer when layout-based reads are enabled.
            if (layout != null && layout.Size > 0)
            {
                rowBuffer = Marshal.AllocHGlobal(layout.Size + 64);
            }
            while (true)
            {
                int fetchResult = JDB_Fetch(hRequest, rowBuffer, 0);
                if (fetchResult == JDEDB_NO_MORE_DATA)
                {
                    break;
                }
                if (fetchResult == JDEDB_SKIPPED)
                {
                    continue;
                }
                if (fetchResult != JDEDB_PASSED)
                {
                    continue;
                }
                string functionName = ReadColumnString(layout, rowBuffer, hRequest, F9862Structures.TableName, F9862Structures.Columns.FunctionName, 33);
                if (string.IsNullOrWhiteSpace(functionName))
                {
                    continue;
                }
                string eventSpecKey = ReadColumnString(layout, rowBuffer, hRequest, F9862Structures.TableName, F9862Structures.Columns.EventSpecKey, 37);
                string dataStructure = ReadColumnString(layout, rowBuffer, hRequest, F9862Structures.TableName, F9862Structures.Columns.DataStructureName, 11);
                var node = new JdeEventRulesNode
                {
                    Id = $"{objectName}:{functionName}",
                    Name = functionName,
                    NodeType = JdeEventRulesNodeType.Function,
                    EventSpecKey = string.IsNullOrWhiteSpace(eventSpecKey) ? null : eventSpecKey,
                    DataStructureName = string.IsNullOrWhiteSpace(dataStructure) ? null : dataStructure
                };
                children.Add(node);
            }
}
        finally
        {
            JDB_CloseTable(hRequest);
            if (rowBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(rowBuffer);
            }
        }
        children = children
            .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return BuildRootNode(objectName, children);
    }

    /// <summary>
    /// Build an event rules tree for a named event rule (NER).
    /// </summary>
    public JdeEventRulesNode GetNamedEventRuleTree(string objectName)
    {
        return GetBusinessFunctionTree(objectName);
    }

    /// <summary>
    /// Build an event rules tree for an interactive application (APPL).
    /// </summary>
    public JdeEventRulesNode GetApplicationEventRulesTree(string objectName)
    {
        return GetEventRulesLinkTree(objectName, ProductTypeFda);
    }

    /// <summary>
    /// Build an event rules tree for a batch application/report (UBE).
    /// </summary>
    public JdeEventRulesNode GetReportEventRulesTree(string objectName)
    {
        return GetEventRulesLinkTree(objectName, ProductTypeRda);
    }

    /// <summary>
    /// Build an event rules tree for a table (TBLE).
    /// </summary>
    public JdeEventRulesNode GetTableEventRulesTree(string objectName)
    {
        return GetEventRulesLinkTree(objectName, ProductTypeTer);
    }

    /// <summary>
    /// Decode event rules into a line list for display.
    /// </summary>
    public IReadOnlyList<JdeEventRuleLine> GetEventRulesLines(string eventSpecKey)
    {
        if (string.IsNullOrWhiteSpace(eventSpecKey))
        {
            return Array.Empty<JdeEventRuleLine>();
        }
        var documents = GetEventRulesXmlDocuments(eventSpecKey);
        if (documents.Count == 0)
        {
            return Array.Empty<JdeEventRuleLine>();
        }
        var lines = new List<JdeEventRuleLine>();
        int sequence = 0;
        foreach (var document in documents)
        {
            sequence++;
            AppendXmlLines(lines, document.Xml, sequence);
        }
        return lines;
    }

    /// <summary>
    /// Retrieve event rules XML documents for a spec key.
    /// </summary>
    public IReadOnlyList<JdeEventRulesXmlDocument> GetEventRulesXmlDocuments(string eventSpecKey)
    {
        if (string.IsNullOrWhiteSpace(eventSpecKey))
        {
            return Array.Empty<JdeEventRulesXmlDocument>();
        }
        var builders = LoadEventRulesXmlBuilders(eventSpecKey);
        if (builders.Count == 0)
        {
            return Array.Empty<JdeEventRulesXmlDocument>();
        }
        var documents = new List<JdeEventRulesXmlDocument>();
        foreach (var builder in builders)
        {
            string xml = builder.BuildXml();
            if (string.IsNullOrWhiteSpace(xml))
            {
                continue;
            }
            documents.Add(new JdeEventRulesXmlDocument
            {
                EventSpecKey = builder.EventSpecKey,
                Xml = xml,
                RecordCount = builder.RecordCount
            });
        }
        return documents;
    }

    /// <summary>
    /// Retrieve event rules XML documents for a spec key at an explicit location.
    /// </summary>
    public IReadOnlyList<JdeEventRulesXmlDocument> GetEventRulesXmlDocuments(
        string eventSpecKey,
        JdeSpecLocation location,
        string? dataSourceOverride)
    {
        return GetEventRulesXmlDocumentsForLocation(eventSpecKey, location, dataSourceOverride);
    }

    /// <summary>
    /// Retrieve data structure (DSTMPL) XML documents for a template name.
    /// </summary>
    public IReadOnlyList<JdeSpecXmlDocument> GetDataStructureXmlDocuments(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return Array.Empty<JdeSpecXmlDocument>();
        }
        var builders = LoadDataStructureXmlBuilders(templateName);
        if (builders.Count == 0)
        {
            return Array.Empty<JdeSpecXmlDocument>();
        }
        var documents = new List<JdeSpecXmlDocument>();
        foreach (var builder in builders)
        {
            string xml = builder.BuildXml();
            if (string.IsNullOrWhiteSpace(xml))
            {
                continue;
            }
            documents.Add(new JdeSpecXmlDocument
            {
                SpecKey = builder.EventSpecKey,
                Xml = xml,
                RecordCount = builder.RecordCount
            });
        }
        return documents;
    }

    /// <summary>
    /// Retrieve data structure XML documents for a template at an explicit location.
    /// </summary>
    public IReadOnlyList<JdeSpecXmlDocument> GetDataStructureXmlDocuments(
        string templateName,
        JdeSpecLocation location,
        string? dataSourceOverride)
    {
        return GetDataStructureXmlDocumentsForLocation(templateName, location, dataSourceOverride);
    }

    private IReadOnlyList<JdeEventRulesXmlDocument> GetEventRulesXmlDocumentsForLocation(
        string eventSpecKey,
        JdeSpecLocation location,
        string? dataSourceOverride)
    {
        if (string.IsNullOrWhiteSpace(eventSpecKey))
        {
            return Array.Empty<JdeEventRulesXmlDocument>();
        }

        var builders = LoadEventRulesXmlBuildersForLocation(eventSpecKey, location, dataSourceOverride);
        if (builders.Count == 0)
        {
            return Array.Empty<JdeEventRulesXmlDocument>();
        }

        var documents = new List<JdeEventRulesXmlDocument>();
        foreach (var builder in builders)
        {
            string xml = builder.BuildXml();
            if (string.IsNullOrWhiteSpace(xml))
            {
                continue;
            }

            documents.Add(new JdeEventRulesXmlDocument
            {
                EventSpecKey = builder.EventSpecKey,
                Xml = xml,
                RecordCount = builder.RecordCount
            });
        }

        return documents;
    }

    private IReadOnlyList<JdeSpecXmlDocument> GetDataStructureXmlDocumentsForLocation(
        string templateName,
        JdeSpecLocation location,
        string? dataSourceOverride)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return Array.Empty<JdeSpecXmlDocument>();
        }

        var builders = LoadDataStructureXmlBuildersForLocation(templateName, location, dataSourceOverride);
        if (builders.Count == 0)
        {
            return Array.Empty<JdeSpecXmlDocument>();
        }

        var documents = new List<JdeSpecXmlDocument>();
        foreach (var builder in builders)
        {
            string xml = builder.BuildXml();
            if (string.IsNullOrWhiteSpace(xml))
            {
                continue;
            }

            documents.Add(new JdeSpecXmlDocument
            {
                SpecKey = builder.EventSpecKey,
                Xml = xml,
                RecordCount = builder.RecordCount
            });
        }

        return documents;
    }

    /// <summary>
    /// Retrieve C business function payload/documents from BUSFUNC specs.
    /// </summary>
    public IReadOnlyList<JdeBusinessFunctionCodeDocument> GetBusinessFunctionCodeDocuments(string objectName, string? functionName)
    {
        return GetBusinessFunctionCodeDocuments(
            objectName,
            functionName,
            JdeBusinessFunctionCodeLocation.Auto,
            dataSourceOverride: null);
    }

    /// <summary>
    /// Retrieve C business function payload/documents from BUSFUNC specs.
    /// </summary>
    public IReadOnlyList<JdeBusinessFunctionCodeDocument> GetBusinessFunctionCodeDocuments(
        string objectName,
        string? functionName,
        JdeBusinessFunctionCodeLocation location,
        string? dataSourceOverride)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return Array.Empty<JdeBusinessFunctionCodeDocument>();
        }

        string objectFilter = NormalizeText(objectName);
        string? functionFilter = string.IsNullOrWhiteSpace(functionName) ? null : NormalizeText(functionName);
        string? resolvedDataSource = NormalizeBusinessFunctionDataSourceOverride(dataSourceOverride);
        TableLayout? layout = _options.UseRowLayoutTables
            ? TableLayoutLoader.Load(F98762Structures.TableName)
            : null;

        LogSpecDebug($"[BUSFUNC] location={location}, dataSourceOverride='{resolvedDataSource ?? string.Empty}'");

        var documents = LoadBusinessFunctionDocuments(
            objectFilter,
            functionFilter,
            layout,
            location,
            resolvedDataSource);

        return documents
            .OrderBy(doc => doc.FunctionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(doc => doc.SourceFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<JdeBusinessFunctionCodeDocument> LoadBusinessFunctionDocuments(
        string objectName,
        string? functionName,
        TableLayout? layout,
        JdeBusinessFunctionCodeLocation location,
        string? dataSourceOverride)
    {
        if (location == JdeBusinessFunctionCodeLocation.Local)
        {
            return FetchBusinessFunctionDocumentsFromLocation(
                objectName,
                functionName,
                layout,
                JdeSpecLocation.LocalUser,
                dataSourceOverride: null);
        }

        if (location == JdeBusinessFunctionCodeLocation.Central)
        {
            return FetchBusinessFunctionDocumentsFromLocation(
                objectName,
                functionName,
                layout,
                JdeSpecLocation.CentralObjects,
                dataSourceOverride);
        }

        var localDocuments = FetchBusinessFunctionDocumentsFromLocation(
            objectName,
            functionName,
            layout,
            JdeSpecLocation.LocalUser,
            dataSourceOverride: null);
        if (localDocuments.Count > 0)
        {
            return localDocuments;
        }

        return FetchBusinessFunctionDocumentsFromLocation(
            objectName,
            functionName,
            layout,
            JdeSpecLocation.CentralObjects,
            dataSourceOverride);
    }

    private IReadOnlyList<JdeBusinessFunctionCodeDocument> FetchBusinessFunctionDocumentsFromLocation(
        string objectName,
        string? functionName,
        TableLayout? layout,
        JdeSpecLocation location,
        string? dataSourceOverride)
    {
        if (!TryOpenSpecHandleAtLocation(
                JdeSpecFileType.BusFunc,
                F98762Structures.TableName,
                fallbackTableName: null,
                location,
                dataSourceOverride,
                out IntPtr hSpec))
        {
            return Array.Empty<JdeBusinessFunctionCodeDocument>();
        }

        try
        {
            if (!TrySelectBusinessFunctionByObject(hSpec, objectName))
            {
                return Array.Empty<JdeBusinessFunctionCodeDocument>();
            }

            var functionDetails = LoadBusinessFunctionSpecDetails(objectName);
            byte[] repositoryBlob = TryLoadBusinessFunctionRepositoryBlob(objectName, location, dataSourceOverride);
            var workflowSourceCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var documents = new List<JdeBusinessFunctionCodeDocument>();
            while (true)
            {
                var specData = new JdeSpecData();
                int fetchResult = JdeSpecEncapApi.jdeSpecFetch(hSpec, ref specData);
                if (fetchResult != JDESPEC_SUCCESS)
                {
                    LogSpecDebug($"[BUSFUNC] {location} jdeSpecFetch end: {fetchResult}");
                    break;
                }

                try
                {
                    var document = CreateBusinessFunctionCodeDocument(
                        layout,
                        objectName,
                        ref specData,
                        location,
                        dataSourceOverride,
                        functionDetails,
                        repositoryBlob,
                        workflowSourceCache);
                    if (document == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(objectName) &&
                        !string.Equals(document.ObjectName, objectName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(functionName) &&
                        !string.Equals(document.FunctionName, functionName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    documents.Add(document);
                }
                finally
                {
                    JdeSpecEncapApi.jdeSpecFreeData(ref specData);
                }
            }

            return documents;
        }
        finally
        {
            JdeSpecEncapApi.jdeSpecClose(hSpec);
        }
    }

    private IReadOnlyList<XmlDocumentBuilder> LoadEventRulesXmlBuilders(string eventSpecKey)
    {
        if (!TryOpenSpecHandle(JdeSpecFileType.GbrSpec, F98741Structures.TableName, fallbackTableName: null, out IntPtr hSpec))
        {
            return Array.Empty<XmlDocumentBuilder>();
        }
        IntPtr hConvert = IntPtr.Zero;
        var buildersByKey = new Dictionary<string, XmlDocumentBuilder>(StringComparer.OrdinalIgnoreCase);
        var orderedBuilders = new List<XmlDocumentBuilder>();
        try
        {
            // Initialize the XML converter used to transform spec blobs into XML.
            int convertInit = JdeSpecEncapApi.jdeSpecInitXMLConvertHandle(out hConvert, JdeSpecFileType.GbrSpec);
            if (convertInit != JDESPEC_SUCCESS || hConvert == IntPtr.Zero)
            {
                LogSpecDebug($"[ER] jdeSpecInitXMLConvertHandle failed: {convertInit}");
                return Array.Empty<XmlDocumentBuilder>();
            }
            var key = new JdeSpecKeyGbrSpec
            {
                EventSpecKey = eventSpecKey,
                Sequence = 0
            };
            int keySize = Marshal.SizeOf<JdeSpecKeyGbrSpec>();
            IntPtr keyPtr = Marshal.AllocHGlobal(keySize);
            try
            {
                Marshal.StructureToPtr(key, keyPtr, false);
                int keyResult = JdeSpecEncapApi.jdeSpecSelectKeyed(hSpec, keyPtr, keySize, 1);
                LogSpecDebug($"[ER] jdeSpecSelectKeyed result: {keyResult}, keySize={keySize}");
                if (keyResult != JDESPEC_SUCCESS)
                {
                    return Array.Empty<XmlDocumentBuilder>();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(keyPtr);
            }
            int recordIndex = 0;
            // Stream spec records and convert each to XML.
            while (true)
            {
                var specData = new JdeSpecData();
                int fetchResult = JdeSpecEncapApi.jdeSpecFetch(hSpec, ref specData);
                if (fetchResult != JDESPEC_SUCCESS)
                {
                    LogSpecDebug($"[ER] jdeSpecFetch end: {fetchResult}");
                    break;
                }
                try
                {
                    string? xml = TryConvertSpecDataToXml(hConvert, ref specData);
                    if (string.IsNullOrWhiteSpace(xml))
                    {
                        continue;
                    }
                    recordIndex++;
                    AddXmlToBuilders(buildersByKey, orderedBuilders, xml, eventSpecKey);
                }
                finally
                {
                    JdeSpecEncapApi.jdeSpecFreeData(ref specData);
                }
            }
            LogSpecDebug($"[ER] Fetched {recordIndex} GBRSPEC records.");
        }
        finally
        {
            if (hConvert != IntPtr.Zero)
            {
                JdeSpecEncapApi.jdeSpecClose(hConvert);
            }
            JdeSpecEncapApi.jdeSpecClose(hSpec);
        }
        return orderedBuilders;
    }

    private IReadOnlyList<XmlDocumentBuilder> LoadDataStructureXmlBuilders(string templateName)
    {
        if (!TryOpenSpecHandle(JdeSpecFileType.Dstmpl, F98743Structures.TableName, F98741Structures.TableName, out IntPtr hSpec))
        {
            return Array.Empty<XmlDocumentBuilder>();
        }
        IntPtr hConvert = IntPtr.Zero;
        var buildersByKey = new Dictionary<string, XmlDocumentBuilder>(StringComparer.OrdinalIgnoreCase);
        var orderedBuilders = new List<XmlDocumentBuilder>();
        try
        {
            // Initialize the XML converter used to transform spec blobs into XML.
            int convertInit = JdeSpecEncapApi.jdeSpecInitXMLConvertHandle(out hConvert, JdeSpecFileType.Dstmpl);
            if (convertInit != JDESPEC_SUCCESS || hConvert == IntPtr.Zero)
            {
                LogSpecDebug($"[DS] jdeSpecInitXMLConvertHandle failed: {convertInit}");
                return Array.Empty<XmlDocumentBuilder>();
            }
            var key = new JdeSpecKeyDstmpl
            {
                TemplateName = new NID(templateName)
            };
            int keySize = Marshal.SizeOf<JdeSpecKeyDstmpl>();
            IntPtr keyPtr = Marshal.AllocHGlobal(keySize);
            try
            {
                Marshal.StructureToPtr(key, keyPtr, false);
                // Prefer the single-record fetch for DSTMPL when available.
                var singleData = new JdeSpecData();
                int singleResult = JdeSpecEncapApi.jdeSpecFetchSingle(hSpec, ref singleData, keyPtr, 1);
                LogSpecDebug($"[DS] jdeSpecFetchSingle result: {singleResult}");
                if (singleResult == JDESPEC_SUCCESS)
                {
                    try
                    {
                        string? xml = TryConvertSpecDataToXmlDirect(hConvert, ref singleData);
                        if (!string.IsNullOrWhiteSpace(xml))
                        {
                            AddXmlToBuilders(buildersByKey, orderedBuilders, xml, templateName);
                            LogSpecDebug("[DS] Fetched 1 DSTMPL record via fetch single.");
                            return orderedBuilders;
                        }
                    }
                    finally
                    {
                        JdeSpecEncapApi.jdeSpecFreeData(ref singleData);
                    }
                }
                int keyResult = JdeSpecEncapApi.jdeSpecSelectKeyed(hSpec, keyPtr, keySize, 1);
                LogSpecDebug($"[DS] jdeSpecSelectKeyed result: {keyResult}, keySize={keySize}");
                if (keyResult != JDESPEC_SUCCESS)
                {
                    return Array.Empty<XmlDocumentBuilder>();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(keyPtr);
            }
            int recordIndex = 0;
            while (true)
            {
                var specData = new JdeSpecData();
                int fetchResult = JdeSpecEncapApi.jdeSpecFetch(hSpec, ref specData);
                if (fetchResult != JDESPEC_SUCCESS)
                {
                    LogSpecDebug($"[DS] jdeSpecFetch end: {fetchResult}");
                    break;
                }
                try
                {
                    string? xml = TryConvertSpecDataToXmlDirect(hConvert, ref specData);
                    if (string.IsNullOrWhiteSpace(xml))
                    {
                        continue;
                    }
                    recordIndex++;
                    AddXmlToBuilders(buildersByKey, orderedBuilders, xml, templateName);
                }
                finally
                {
                    JdeSpecEncapApi.jdeSpecFreeData(ref specData);
                }
            }
            LogSpecDebug($"[DS] Fetched {recordIndex} DSTMPL records.");
        }
        finally
        {
            if (hConvert != IntPtr.Zero)
            {
                JdeSpecEncapApi.jdeSpecClose(hConvert);
            }
            JdeSpecEncapApi.jdeSpecClose(hSpec);
        }
        return orderedBuilders;
    }

    private IReadOnlyList<XmlDocumentBuilder> LoadEventRulesXmlBuildersForLocation(
        string eventSpecKey,
        JdeSpecLocation location,
        string? dataSourceOverride)
    {
        if (!TryOpenSpecHandleAtLocation(
                JdeSpecFileType.GbrSpec,
                F98741Structures.TableName,
                fallbackTableName: null,
                location,
                dataSourceOverride,
                out IntPtr hSpec))
        {
            return Array.Empty<XmlDocumentBuilder>();
        }

        IntPtr hConvert = IntPtr.Zero;
        var buildersByKey = new Dictionary<string, XmlDocumentBuilder>(StringComparer.OrdinalIgnoreCase);
        var orderedBuilders = new List<XmlDocumentBuilder>();
        try
        {
            int convertInit = JdeSpecEncapApi.jdeSpecInitXMLConvertHandle(out hConvert, JdeSpecFileType.GbrSpec);
            if (convertInit != JDESPEC_SUCCESS || hConvert == IntPtr.Zero)
            {
                LogSpecDebug($"[ER] jdeSpecInitXMLConvertHandle failed: {convertInit}");
                return Array.Empty<XmlDocumentBuilder>();
            }

            var key = new JdeSpecKeyGbrSpec
            {
                EventSpecKey = eventSpecKey,
                Sequence = 0
            };
            int keySize = Marshal.SizeOf<JdeSpecKeyGbrSpec>();
            IntPtr keyPtr = Marshal.AllocHGlobal(keySize);
            try
            {
                Marshal.StructureToPtr(key, keyPtr, false);
                int keyResult = JdeSpecEncapApi.jdeSpecSelectKeyed(hSpec, keyPtr, keySize, 1);
                LogSpecDebug($"[ER] jdeSpecSelectKeyed result: {keyResult}, keySize={keySize}");
                if (keyResult != JDESPEC_SUCCESS)
                {
                    return Array.Empty<XmlDocumentBuilder>();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(keyPtr);
            }

            int recordIndex = 0;
            while (true)
            {
                var specData = new JdeSpecData();
                int fetchResult = JdeSpecEncapApi.jdeSpecFetch(hSpec, ref specData);
                if (fetchResult != JDESPEC_SUCCESS)
                {
                    LogSpecDebug($"[ER] jdeSpecFetch end: {fetchResult}");
                    break;
                }

                try
                {
                    string? xml = TryConvertSpecDataToXml(hConvert, ref specData);
                    if (string.IsNullOrWhiteSpace(xml))
                    {
                        continue;
                    }

                    recordIndex++;
                    AddXmlToBuilders(buildersByKey, orderedBuilders, xml, eventSpecKey);
                }
                finally
                {
                    JdeSpecEncapApi.jdeSpecFreeData(ref specData);
                }
            }

            LogSpecDebug($"[ER] Fetched {recordIndex} GBRSPEC records.");
        }
        finally
        {
            if (hConvert != IntPtr.Zero)
            {
                JdeSpecEncapApi.jdeSpecClose(hConvert);
            }

            JdeSpecEncapApi.jdeSpecClose(hSpec);
        }

        return orderedBuilders;
    }

    private IReadOnlyList<XmlDocumentBuilder> LoadDataStructureXmlBuildersForLocation(
        string templateName,
        JdeSpecLocation location,
        string? dataSourceOverride)
    {
        if (!TryOpenSpecHandleAtLocation(
                JdeSpecFileType.Dstmpl,
                F98743Structures.TableName,
                F98741Structures.TableName,
                location,
                dataSourceOverride,
                out IntPtr hSpec))
        {
            return Array.Empty<XmlDocumentBuilder>();
        }

        IntPtr hConvert = IntPtr.Zero;
        var buildersByKey = new Dictionary<string, XmlDocumentBuilder>(StringComparer.OrdinalIgnoreCase);
        var orderedBuilders = new List<XmlDocumentBuilder>();
        try
        {
            int convertInit = JdeSpecEncapApi.jdeSpecInitXMLConvertHandle(out hConvert, JdeSpecFileType.Dstmpl);
            if (convertInit != JDESPEC_SUCCESS || hConvert == IntPtr.Zero)
            {
                LogSpecDebug($"[DS] jdeSpecInitXMLConvertHandle failed: {convertInit}");
                return Array.Empty<XmlDocumentBuilder>();
            }

            var key = new JdeSpecKeyDstmpl
            {
                TemplateName = new NID(templateName)
            };
            int keySize = Marshal.SizeOf<JdeSpecKeyDstmpl>();
            IntPtr keyPtr = Marshal.AllocHGlobal(keySize);
            try
            {
                Marshal.StructureToPtr(key, keyPtr, false);
                var singleData = new JdeSpecData();
                int singleResult = JdeSpecEncapApi.jdeSpecFetchSingle(hSpec, ref singleData, keyPtr, 1);
                LogSpecDebug($"[DS] jdeSpecFetchSingle result: {singleResult}");
                if (singleResult == JDESPEC_SUCCESS)
                {
                    try
                    {
                        string? xml = TryConvertSpecDataToXmlDirect(hConvert, ref singleData);
                        if (!string.IsNullOrWhiteSpace(xml))
                        {
                            AddXmlToBuilders(buildersByKey, orderedBuilders, xml, templateName);
                            LogSpecDebug("[DS] Fetched 1 DSTMPL record via fetch single.");
                            return orderedBuilders;
                        }
                    }
                    finally
                    {
                        JdeSpecEncapApi.jdeSpecFreeData(ref singleData);
                    }
                }

                int keyResult = JdeSpecEncapApi.jdeSpecSelectKeyed(hSpec, keyPtr, keySize, 1);
                LogSpecDebug($"[DS] jdeSpecSelectKeyed result: {keyResult}, keySize={keySize}");
                if (keyResult != JDESPEC_SUCCESS)
                {
                    return Array.Empty<XmlDocumentBuilder>();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(keyPtr);
            }

            int recordIndex = 0;
            while (true)
            {
                var specData = new JdeSpecData();
                int fetchResult = JdeSpecEncapApi.jdeSpecFetch(hSpec, ref specData);
                if (fetchResult != JDESPEC_SUCCESS)
                {
                    LogSpecDebug($"[DS] jdeSpecFetch end: {fetchResult}");
                    break;
                }

                try
                {
                    string? xml = TryConvertSpecDataToXmlDirect(hConvert, ref specData);
                    if (string.IsNullOrWhiteSpace(xml))
                    {
                        continue;
                    }

                    recordIndex++;
                    AddXmlToBuilders(buildersByKey, orderedBuilders, xml, templateName);
                }
                finally
                {
                    JdeSpecEncapApi.jdeSpecFreeData(ref specData);
                }
            }

            LogSpecDebug($"[DS] Fetched {recordIndex} DSTMPL records.");
        }
        finally
        {
            if (hConvert != IntPtr.Zero)
            {
                JdeSpecEncapApi.jdeSpecClose(hConvert);
            }

            JdeSpecEncapApi.jdeSpecClose(hSpec);
        }

        return orderedBuilders;
    }

    private bool TryOpenSpecHandle(JdeSpecFileType specType, string tableName, string? fallbackTableName, out IntPtr hSpec)
    {
        hSpec = IntPtr.Zero;
        // Build candidate data sources and attempt central/local spec open in order.
        var candidates = BuildSpecDataSourceCandidates(tableName, fallbackTableName);
        foreach (string? dataSource in candidates)
        {
            string resolved = dataSource ?? string.Empty;
            if (specType == JdeSpecFileType.Dstmpl && TryOpenCentralIndexed(specType, resolved, out hSpec))
            {
                return true;
            }
            int result = JdeSpecEncapApi.jdeSpecOpen(out hSpec, _hUser, specType, JdeSpecLocation.CentralObjects, resolved);
            LogSpecDebug($"[SPEC] jdeSpecOpen {specType} CentralObjects result={result}, ds='{resolved}', handle=0x{hSpec.ToInt64():X}");
            if (result == JDESPEC_SUCCESS && hSpec != IntPtr.Zero)
            {
                return true;
            }
        }
        if (specType == JdeSpecFileType.Dstmpl)
        {
            int indexedResult = JdeSpecEncapApi.jdeSpecOpenLocalIndexed(out hSpec, _hUser, specType, new ID(1));
            LogSpecDebug($"[SPEC] jdeSpecOpenLocalIndexed {specType} result={indexedResult}, handle=0x{hSpec.ToInt64():X}");
            if (indexedResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero)
            {
                return true;
            }
        }
        int localResult = JdeSpecEncapApi.jdeSpecOpenLocal(out hSpec, _hUser, specType);
        LogSpecDebug($"[SPEC] jdeSpecOpenLocal {specType} result={localResult}, handle=0x{hSpec.ToInt64():X}");
        if (localResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero)
        {
            return true;
        }
        foreach (string? dataSource in candidates)
        {
            string resolved = dataSource ?? string.Empty;
            int result = JdeSpecEncapApi.jdeSpecOpen(out hSpec, _hUser, specType, JdeSpecLocation.LocalUser, resolved);
            LogSpecDebug($"[SPEC] jdeSpecOpen {specType} LocalUser result={result}, ds='{resolved}', handle=0x{hSpec.ToInt64():X}");
            if (result == JDESPEC_SUCCESS && hSpec != IntPtr.Zero)
            {
                return true;
            }
        }
        return false;
    }

    private bool TryOpenCentralIndexed(JdeSpecFileType specType, string dataSource, out IntPtr hSpec)
    {
        hSpec = IntPtr.Zero;
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return false;
        }
        string? pathCode = TryExtractPathCode(dataSource);
        if (string.IsNullOrWhiteSpace(pathCode))
        {
            return false;
        }
        int indexedResult = JdeSpecEncapApi.jdeSpecOpenCentralIndexed(out hSpec, _hUser, specType, new ID(1), pathCode);
        LogSpecDebug($"[SPEC] jdeSpecOpenCentralIndexed {specType} result={indexedResult}, path='{pathCode}', handle=0x{hSpec.ToInt64():X}");
        if (indexedResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero)
        {
            return true;
        }
        int centralResult = JdeSpecEncapApi.jdeSpecOpenCentral(out hSpec, _hUser, specType, pathCode);
        LogSpecDebug($"[SPEC] jdeSpecOpenCentral {specType} result={centralResult}, path='{pathCode}', handle=0x{hSpec.ToInt64():X}");
        return centralResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero;
    }

    private static string? TryExtractPathCode(string dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return null;
        }
        int separator = dataSource.LastIndexOf('-');
        if (separator >= 0 && separator + 1 < dataSource.Length)
        {
            return dataSource.Substring(separator + 1).Trim();
        }
        return dataSource.Trim();
    }

    private static string? NormalizeBusinessFunctionDataSourceOverride(string? dataSourceOverride)
    {
        if (string.IsNullOrWhiteSpace(dataSourceOverride))
        {
            return null;
        }

        string trimmed = dataSourceOverride.Trim();
        if (trimmed.StartsWith(CentralObjectsDataSourcePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return LooksLikePathCode(trimmed)
            ? $"{CentralObjectsDataSourcePrefix}{trimmed}"
            : trimmed;
    }

    private List<string?> BuildSpecDataSourceCandidates(
        string tableName,
        string? fallbackTableName,
        string? preferredDataSource = null,
        bool includeResolvedDefaults = true)
    {
        var candidates = new List<string?>();
        if (!string.IsNullOrWhiteSpace(preferredDataSource))
        {
            candidates.Add(preferredDataSource);
        }

        if (includeResolvedDefaults)
        {
            string? primary = DataSourceResolver.ResolveTableDataSource(_hUser, tableName);
            if (!string.IsNullOrWhiteSpace(primary))
            {
                AddIfMissing(candidates, primary);
            }
            if (!string.IsNullOrWhiteSpace(fallbackTableName))
            {
                string? fallback = DataSourceResolver.ResolveTableDataSource(_hUser, fallbackTableName);
                if (!string.IsNullOrWhiteSpace(fallback))
                {
                    AddIfMissing(candidates, fallback);
                }
            }
        }
        if (includeResolvedDefaults)
        {
            AddIfMissing(candidates, string.Empty);
        }
        return candidates;
    }

    private static void AddIfMissing(List<string?> candidates, string value)
    {
        if (candidates.Any(candidate => string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        candidates.Add(value);
    }

    private bool TryOpenSpecHandleAtLocation(
        JdeSpecFileType specType,
        string tableName,
        string? fallbackTableName,
        JdeSpecLocation location,
        string? preferredDataSource,
        out IntPtr hSpec)
    {
        hSpec = IntPtr.Zero;
        bool includeResolvedDefaults = string.IsNullOrWhiteSpace(preferredDataSource);
        var candidates = BuildSpecDataSourceCandidates(
            tableName,
            fallbackTableName,
            preferredDataSource,
            includeResolvedDefaults);
        foreach (string? dataSource in candidates)
        {
            string resolved = dataSource ?? string.Empty;
            ID? indexId = specType switch
            {
                JdeSpecFileType.BusFunc => new ID(SpecKeyBusFuncByObject),
                JdeSpecFileType.Dstmpl => new ID(SpecKeyDataStructureByTemplate),
                _ => null
            };

            if (indexId.HasValue &&
                TryOpenIndexedSpecHandle(specType, location, indexId.Value, resolved, out hSpec))
            {
                return true;
            }

            if (location == JdeSpecLocation.CentralObjects)
            {
                string? pathCode = TryExtractPathCode(resolved);
                if (!string.IsNullOrWhiteSpace(pathCode) &&
                    !string.Equals(pathCode, resolved, StringComparison.OrdinalIgnoreCase))
                {
                    if (indexId.HasValue &&
                        TryOpenIndexedSpecHandle(specType, location, indexId.Value, pathCode, out hSpec))
                    {
                        return true;
                    }

                    int pathCodeResult = JdeSpecEncapApi.jdeSpecOpen(
                        out hSpec,
                        _hUser,
                        specType,
                        location,
                        pathCode);
                    LogSpecDebug($"[SPEC] jdeSpecOpen {specType} {location} result={pathCodeResult}, ds='{pathCode}', handle=0x{hSpec.ToInt64():X}");
                    if (pathCodeResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero)
                    {
                        return true;
                    }
                }
            }

            int result = JdeSpecEncapApi.jdeSpecOpen(out hSpec, _hUser, specType, location, resolved);
            LogSpecDebug($"[SPEC] jdeSpecOpen {specType} {location} result={result}, ds='{resolved}', handle=0x{hSpec.ToInt64():X}");
            if (result == JDESPEC_SUCCESS && hSpec != IntPtr.Zero)
            {
                return true;
            }
        }

        if (location == JdeSpecLocation.LocalUser)
        {
            if (specType == JdeSpecFileType.BusFunc || specType == JdeSpecFileType.Dstmpl)
            {
                int localIndex = specType == JdeSpecFileType.BusFunc
                    ? SpecKeyBusFuncByObject
                    : SpecKeyDataStructureByTemplate;
                int localIndexedResult = JdeSpecEncapApi.jdeSpecOpenLocalIndexed(
                    out hSpec,
                    _hUser,
                    specType,
                    new ID(localIndex));
                LogSpecDebug(
                    $"[SPEC] jdeSpecOpenLocalIndexed {specType} index={localIndex} result={localIndexedResult}, handle=0x{hSpec.ToInt64():X}");
                if (localIndexedResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero)
                {
                    return true;
                }
            }

            int localResult = JdeSpecEncapApi.jdeSpecOpenLocal(out hSpec, _hUser, specType);
            LogSpecDebug($"[SPEC] jdeSpecOpenLocal {specType} result={localResult}, handle=0x{hSpec.ToInt64():X}");
            return localResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero;
        }

        return false;
    }

    private bool TryOpenIndexedSpecHandle(
        JdeSpecFileType specType,
        JdeSpecLocation location,
        ID indexId,
        string locationSource,
        out IntPtr hSpec)
    {
        hSpec = IntPtr.Zero;
        int indexedResult = JdeSpecEncapApi.jdeSpecOpenIndexed(
            out hSpec,
            _hUser,
            specType,
            location,
            indexId,
            locationSource);
        LogSpecDebug(
            $"[SPEC] jdeSpecOpenIndexed {specType} {location} index={indexId.Value} result={indexedResult}, ds='{locationSource}', handle=0x{hSpec.ToInt64():X}");
        return indexedResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero;
    }

    private bool TrySelectBusinessFunctionByObject(IntPtr hSpec, string objectName)
    {
        var key = new JdeSpecKeyBusFuncByObject
        {
            ObjectName = new NID(objectName)
        };
        return TrySelectSpecKey(hSpec, key, "[BUSFUNC] object");
    }

    private bool TrySelectSpecKey<T>(IntPtr hSpec, T key, string debugScope)
        where T : struct
    {
        int keySize = Marshal.SizeOf<T>();
        IntPtr keyPtr = Marshal.AllocHGlobal(keySize);
        try
        {
            Marshal.StructureToPtr(key, keyPtr, false);
            int keyResult = JdeSpecEncapApi.jdeSpecSelectKeyed(hSpec, keyPtr, keySize, 1);
            LogSpecDebug($"{debugScope} jdeSpecSelectKeyed result: {keyResult}, keySize={keySize}");
            return keyResult == JDESPEC_SUCCESS;
        }
        finally
        {
            Marshal.FreeHGlobal(keyPtr);
        }
    }

    private JdeBusinessFunctionCodeDocument? CreateBusinessFunctionCodeDocument(
        TableLayout? layout,
        string objectName,
        ref JdeSpecData specData,
        JdeSpecLocation location,
        string? dataSourceOverride,
        IReadOnlyDictionary<string, BusinessFunctionSpecDetails> functionDetails,
        byte[] repositoryBlob,
        IDictionary<string, string> workflowSourceCache)
    {
        byte[] payload = ReadBusinessFunctionPayload(ref specData);
        if (payload.Length == 0 && specData.RdbRecord == IntPtr.Zero)
        {
            return null;
        }

        string objectValue = ReadSpecRecordString(layout, specData.RdbRecord, F98762Structures.Columns.ObjectName);
        string functionName = ReadSpecRecordString(layout, specData.RdbRecord, F98762Structures.Columns.FunctionName);
        string sourceFileName = ReadSpecRecordString(layout, specData.RdbRecord, F98762Structures.Columns.SourceFileName);
        string version = ReadSpecRecordString(layout, specData.RdbRecord, F98762Structures.Columns.Version);

        string resolvedObjectName = string.IsNullOrWhiteSpace(objectValue) ? objectName : objectValue;
        string resolvedFunctionName = string.IsNullOrWhiteSpace(functionName) ? string.Empty : functionName;
        if (string.IsNullOrWhiteSpace(resolvedFunctionName) &&
            functionDetails.Count == 1)
        {
            resolvedFunctionName = functionDetails.Keys.FirstOrDefault() ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(resolvedFunctionName))
        {
            resolvedFunctionName = InferBusinessFunctionNameFromPayload(payload, resolvedObjectName);
        }

        byte[] sourcePayload = payload;
        string sourceCode = ExtractBusinessFunctionSourceCode(payload, resolvedFunctionName, resolvedObjectName);
        string headerCode = ExtractBusinessFunctionHeaderCode(payload, resolvedObjectName);
        if (repositoryBlob.Length > 0)
        {
            string repositorySource = ExtractBusinessFunctionSourceCode(repositoryBlob, resolvedFunctionName, resolvedObjectName);
            string repositoryHeader = ExtractBusinessFunctionHeaderCode(repositoryBlob, resolvedObjectName);
            if (!string.IsNullOrWhiteSpace(repositorySource) || !string.IsNullOrWhiteSpace(repositoryHeader))
            {
                sourcePayload = repositoryBlob;
                if (!string.IsNullOrWhiteSpace(repositorySource))
                {
                    sourceCode = repositorySource;
                }

                if (!string.IsNullOrWhiteSpace(repositoryHeader))
                {
                    headerCode = repositoryHeader;
                }

                LogSpecDebug($"[BUSFUNC] Source resolved from F98780R OMRBLOB for {resolvedObjectName}.{resolvedFunctionName}");
            }
        }

        if ((!LooksLikeCSource(sourceCode) || string.IsNullOrWhiteSpace(sourceCode)) &&
            functionDetails.TryGetValue(resolvedFunctionName, out BusinessFunctionSpecDetails details))
        {
            string workflowSource = TryBuildBusinessFunctionWorkflowSource(
                resolvedObjectName,
                details,
                location,
                dataSourceOverride,
                workflowSourceCache);
            if (!string.IsNullOrWhiteSpace(workflowSource))
            {
                sourceCode = workflowSource;
                LogSpecDebug($"[BUSFUNC] Source resolved from DSTMPL/GBRSPEC workflow for {resolvedObjectName}.{resolvedFunctionName}");
            }
        }

        return new JdeBusinessFunctionCodeDocument
        {
            ObjectName = resolvedObjectName,
            FunctionName = resolvedFunctionName,
            SourceFileName = sourceFileName,
            Version = version,
            DataType = specData.DataType,
            PayloadSize = sourcePayload.Length,
            SourceCode = sourceCode,
            HeaderCode = headerCode,
            SourceLooksLikeCode = LooksLikeCSource(sourceCode),
            Payload = sourcePayload
        };
    }

    private IReadOnlyDictionary<string, BusinessFunctionSpecDetails> LoadBusinessFunctionSpecDetails(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return new Dictionary<string, BusinessFunctionSpecDetails>(StringComparer.OrdinalIgnoreCase);
        }

        var details = new Dictionary<string, BusinessFunctionSpecDetails>(StringComparer.OrdinalIgnoreCase);
        HREQUEST hRequest = OpenTable(F9862Structures.TableName, new ID(F9862Structures.IdObjectNameFunctionName));
        TableLayout? layout = _options.UseRowLayoutTables
            ? TableLayoutLoader.Load(F9862Structures.TableName)
            : null;
        IntPtr rowBuffer = IntPtr.Zero;
        try
        {
            var key = new F9862Structures.Key1
            {
                ObjectName = objectName,
                FunctionName = string.Empty
            };

            int keySize = Marshal.SizeOf<F9862Structures.Key1>();
            IntPtr keyPtr = Marshal.AllocHGlobal(keySize);
            try
            {
                Marshal.StructureToPtr(key, keyPtr, false);
                int keyResult = JDB_SelectKeyed(hRequest, new ID(F9862Structures.IdObjectNameFunctionName), keyPtr, 1);
                if (keyResult != JDEDB_PASSED)
                {
                    return details;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(keyPtr);
            }

            if (layout != null && layout.Size > 0)
            {
                rowBuffer = Marshal.AllocHGlobal(layout.Size + 64);
            }

            while (true)
            {
                int fetchResult = JDB_Fetch(hRequest, rowBuffer, 0);
                if (fetchResult == JDEDB_NO_MORE_DATA)
                {
                    break;
                }

                if (fetchResult == JDEDB_SKIPPED)
                {
                    continue;
                }

                if (fetchResult != JDEDB_PASSED)
                {
                    continue;
                }

                string functionName = ReadColumnString(layout, rowBuffer, hRequest, F9862Structures.TableName, F9862Structures.Columns.FunctionName, 33);
                if (string.IsNullOrWhiteSpace(functionName))
                {
                    continue;
                }

                string templateName = ReadColumnString(layout, rowBuffer, hRequest, F9862Structures.TableName, F9862Structures.Columns.DataStructureName, 11);
                string eventSpecKey = ReadColumnString(layout, rowBuffer, hRequest, F9862Structures.TableName, F9862Structures.Columns.EventSpecKey, 37);

                if (!details.ContainsKey(functionName))
                {
                    details[functionName] = new BusinessFunctionSpecDetails(functionName, templateName, eventSpecKey);
                }
            }
        }
        finally
        {
            JDB_CloseTable(hRequest);
            if (rowBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(rowBuffer);
            }
        }

        return details;
    }

    private byte[] TryLoadBusinessFunctionRepositoryBlob(
        string objectName,
        JdeSpecLocation location,
        string? dataSourceOverride)
    {
        if (location != JdeSpecLocation.CentralObjects)
        {
            return Array.Empty<byte>();
        }

        string? repositoryDataSource = ResolveBusinessFunctionRepositoryDataSource(location, dataSourceOverride);
        const bool allowFallbackToDefault = false;
        if (!TryOpenTable(
                F98780RStructures.TableName,
                new ID(F98780RStructures.IdObjectReleaseVersion),
                repositoryDataSource,
                allowFallbackToDefault,
                out HREQUEST hRequest))
        {
            return Array.Empty<byte>();
        }

        try
        {
            var key = new F98780RStructures.Key1
            {
                ObjectId = objectName,
                Release = string.Empty,
                Version = string.Empty
            };

            int keySize = Marshal.SizeOf<F98780RStructures.Key1>();
            IntPtr keyPtr = Marshal.AllocHGlobal(keySize);
            try
            {
                Marshal.StructureToPtr(key, keyPtr, false);
                int keyResult = JDB_SelectKeyed(
                    hRequest,
                    new ID(F98780RStructures.IdObjectReleaseVersion),
                    keyPtr,
                    1);
                if (keyResult != JDEDB_PASSED)
                {
                    LogSpecDebug($"[BUSFUNC] F98780R select failed for {objectName} (result={keyResult}).");
                    return Array.Empty<byte>();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(keyPtr);
            }

            while (true)
            {
                int fetchResult = JDB_Fetch(hRequest, IntPtr.Zero, 0);
                if (fetchResult == JDEDB_NO_MORE_DATA)
                {
                    break;
                }

                if (fetchResult == JDEDB_SKIPPED)
                {
                    continue;
                }

                if (fetchResult != JDEDB_PASSED)
                {
                    break;
                }

                byte[] blob = ReadBlob(hRequest, F98780RStructures.TableName, F98780RStructures.Columns.ObjectRepositoryBlob);
                if (blob.Length > 0)
                {
                    LogSpecDebug($"[BUSFUNC] F98780R OMRBLOB payload bytes={blob.Length} for {objectName}");
                    return blob;
                }
            }
        }
        finally
        {
            JDB_CloseTable(hRequest);
        }

        LogSpecDebug($"[BUSFUNC] F98780R returned no OMRBLOB rows for {objectName}.");
        return Array.Empty<byte>();
    }

    private string? ResolveBusinessFunctionRepositoryDataSource(JdeSpecLocation location, string? dataSourceOverride)
    {
        if (location != JdeSpecLocation.CentralObjects)
        {
            return null;
        }

        string? explicitDataSource = NormalizeCentralObjectsDataSource(dataSourceOverride);
        if (!string.IsNullOrWhiteSpace(explicitDataSource))
        {
            return explicitDataSource;
        }

        string?[] candidates =
        {
            DataSourceResolver.ResolveTableDataSource(_hUser, F98780RStructures.TableName),
            DataSourceResolver.ResolveTableDataSource(_hUser, F98762Structures.TableName),
            DataSourceResolver.ResolveTableDataSource(_hUser, F98743Structures.TableName),
            DataSourceResolver.ResolveTableDataSource(_hUser, F98741Structures.TableName),
            DataSourceResolver.ResolveTableDataSource(_hUser, F98740Structures.TableName)
        };

        foreach (string? candidate in candidates)
        {
            string? normalized = NormalizeCentralObjectsDataSource(candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static string? NormalizeCentralObjectsDataSource(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.StartsWith(CentralObjectsDataSourcePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        string? pathCode = TryExtractCentralPathCode(trimmed);
        if (!string.IsNullOrWhiteSpace(pathCode))
        {
            return $"{CentralObjectsDataSourcePrefix}{pathCode}";
        }

        return null;
    }

    private static string? TryExtractCentralPathCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        string? extracted = TryExtractPathCode(trimmed);
        if (LooksLikePathCode(extracted))
        {
            return extracted;
        }

        return LooksLikePathCode(trimmed) ? trimmed : null;
    }

    private static bool LooksLikePathCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string token = value.Trim();
        bool hasLetter = false;
        bool hasDigit = false;
        foreach (char ch in token)
        {
            if (char.IsLetter(ch))
            {
                hasLetter = true;
                continue;
            }

            if (char.IsDigit(ch))
            {
                hasDigit = true;
                continue;
            }

            return false;
        }

        return hasLetter && hasDigit && token.Length >= 4;
    }

    private bool TryOpenTable(
        string tableName,
        ID indexId,
        string? dataSourceOverride,
        bool allowFallbackToDefault,
        out HREQUEST hRequest)
    {
        hRequest = new HREQUEST();
        string? requested = string.IsNullOrWhiteSpace(dataSourceOverride)
            ? null
            : dataSourceOverride.Trim();
        int result = JDB_OpenTable(_hUser, new NID(tableName), indexId, IntPtr.Zero, 0, requested, out hRequest);
        if (result != JDEDB_PASSED || !hRequest.IsValid)
        {
            if (allowFallbackToDefault)
            {
                string? resolved = DataSourceResolver.ResolveTableDataSource(_hUser, tableName);
                if (!string.IsNullOrWhiteSpace(resolved) &&
                    !string.Equals(resolved, requested, StringComparison.OrdinalIgnoreCase))
                {
                    result = JDB_OpenTable(_hUser, new NID(tableName), indexId, IntPtr.Zero, 0, resolved, out hRequest);
                }
            }

            if (allowFallbackToDefault &&
                (result != JDEDB_PASSED || !hRequest.IsValid) &&
                !string.IsNullOrWhiteSpace(requested))
            {
                result = JDB_OpenTable(_hUser, new NID(tableName), indexId, IntPtr.Zero, 0, null, out hRequest);
            }
        }

        if (result != JDEDB_PASSED || !hRequest.IsValid)
        {
            LogSpecDebug($"[BUSFUNC] Failed to open {tableName} with ds='{requested ?? string.Empty}' (result={result})");
            return false;
        }

        LogRequestDataSource(hRequest, tableName);
        return true;
    }

    private string TryBuildBusinessFunctionWorkflowSource(
        string objectName,
        BusinessFunctionSpecDetails details,
        JdeSpecLocation location,
        string? dataSourceOverride,
        IDictionary<string, string> workflowSourceCache)
    {
        if (string.IsNullOrWhiteSpace(details.FunctionName))
        {
            return string.Empty;
        }

        if (workflowSourceCache.TryGetValue(details.FunctionName, out string? cached))
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(details.EventSpecKey) ||
            string.IsNullOrWhiteSpace(details.TemplateName))
        {
            workflowSourceCache[details.FunctionName] = string.Empty;
            return string.Empty;
        }

        var dsDocuments = GetDataStructureXmlDocumentsForLocation(details.TemplateName, location, dataSourceOverride);
        string dataStructureXml = dsDocuments.FirstOrDefault(doc => !string.IsNullOrWhiteSpace(doc.Xml))?.Xml ?? string.Empty;
        if (string.IsNullOrWhiteSpace(dataStructureXml))
        {
            workflowSourceCache[details.FunctionName] = string.Empty;
            return string.Empty;
        }

        var eventDocuments = GetEventRulesXmlDocumentsForLocation(details.EventSpecKey, location, dataSourceOverride);
        if (eventDocuments.Count == 0)
        {
            workflowSourceCache[details.FunctionName] = string.Empty;
            return string.Empty;
        }

        var combined = new List<string>();
        foreach (var eventDocument in eventDocuments)
        {
            if (string.IsNullOrWhiteSpace(eventDocument.Xml))
            {
                continue;
            }

            try
            {
                var engine = new JdeXmlEngine(eventDocument.Xml, dataStructureXml);
                engine.ConvertXmlToReadableEr();
                if (!string.IsNullOrWhiteSpace(engine.ReadableEventRule))
                {
                    combined.Add(engine.ReadableEventRule.TrimEnd());
                }
            }
            catch (Exception ex)
            {
                LogSpecDebug($"[BUSFUNC] Workflow format failed for {objectName}.{details.FunctionName}: {ex.Message}");
            }
        }

        string source = combined.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine + Environment.NewLine, combined);
        workflowSourceCache[details.FunctionName] = source;
        return source;
    }

    private static string InferBusinessFunctionNameFromPayload(byte[] payload, string objectName)
    {
        if (payload.Length == 0)
        {
            return string.Empty;
        }

        string text = NormalizeExtractedText(DecodePayloadAsUnicode(payload));
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string normalizedObject = NormalizeText(objectName);
        foreach (string token in ExtractAlphanumericTokens(text))
        {
            if (string.Equals(token, normalizedObject, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (LooksLikeObjectIdentifier(token))
            {
                continue;
            }

            bool hasLetter = token.Any(char.IsLetter);
            bool hasLower = token.Any(char.IsLower);
            if (!hasLetter || !hasLower)
            {
                continue;
            }

            return token;
        }

        return string.Empty;
    }

    private static IEnumerable<string> ExtractAlphanumericTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var builder = new StringBuilder();
        foreach (char ch in text)
        {
            bool isTokenChar = char.IsLetterOrDigit(ch) || ch == '_';
            if (isTokenChar)
            {
                builder.Append(ch);
                continue;
            }

            if (builder.Length >= 4)
            {
                yield return builder.ToString();
            }

            builder.Clear();
        }

        if (builder.Length >= 4)
        {
            yield return builder.ToString();
        }
    }

    private static bool LooksLikeObjectIdentifier(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length < 2)
        {
            return false;
        }

        if (token[0] != 'B' && token[0] != 'D')
        {
            return false;
        }

        for (int i = 1; i < token.Length; i++)
        {
            if (!char.IsDigit(token[i]))
            {
                return false;
            }
        }

        return true;
    }

    private byte[] ReadBusinessFunctionPayload(ref JdeSpecData specData)
    {
        if (specData.SpecData == IntPtr.Zero || specData.DataLen == 0)
        {
            return Array.Empty<byte>();
        }

        int requestedLength = specData.DataLen > int.MaxValue ? int.MaxValue : (int)specData.DataLen;
        if (requestedLength <= 0)
        {
            return Array.Empty<byte>();
        }

        int copyLength = Math.Min(requestedLength, MaxBusinessFunctionPayloadBytes);
        var payload = new byte[copyLength];
        Marshal.Copy(specData.SpecData, payload, 0, copyLength);
        if (copyLength < requestedLength)
        {
            LogSpecDebug($"[BUSFUNC] Payload truncated from {requestedLength} to {copyLength} bytes.");
        }
        return payload;
    }

    private static string ReadSpecRecordString(TableLayout? layout, IntPtr rdbRecord, string columnName)
    {
        if (layout == null || rdbRecord == IntPtr.Zero)
        {
            return string.Empty;
        }

        var value = layout.ReadValueByColumn(rdbRecord, columnName).Value;
        return NormalizeText(value switch
        {
            null => string.Empty,
            string text => text,
            _ => value.ToString() ?? string.Empty
        });
    }

    private static string ExtractBusinessFunctionSourceCode(byte[] payload, string functionName, string objectName)
    {
        if (payload.Length == 0)
        {
            return string.Empty;
        }

        if (TryExtractSourceFromArchivePayload(payload, objectName, out string archiveSource))
        {
            return archiveSource;
        }

        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AddDecodedCandidate(candidates, seen, DecodePayloadAsUnicode(payload));
        AddDecodedCandidate(candidates, seen, DecodePayloadAsUtf8(payload));
        AddDecodedCandidate(candidates, seen, DecodePayloadAsAscii(payload));

        if (TryUncompress(payload, out byte[] uncompressed) &&
            uncompressed.Length > 0 &&
            !payload.SequenceEqual(uncompressed))
        {
            AddDecodedCandidate(candidates, seen, DecodePayloadAsUnicode(uncompressed));
            AddDecodedCandidate(candidates, seen, DecodePayloadAsUtf8(uncompressed));
            AddDecodedCandidate(candidates, seen, DecodePayloadAsAscii(uncompressed));
        }

        string best = string.Empty;
        int bestScore = int.MinValue;
        foreach (string candidate in candidates)
        {
            string segment = ExtractLikelyCodeSegment(candidate);
            int score = ScoreCodeCandidate(segment, functionName);
            if (score > bestScore)
            {
                bestScore = score;
                best = segment;
            }
        }

        if (string.IsNullOrWhiteSpace(best) && candidates.Count > 0)
        {
            return candidates[0];
        }

        return best;
    }

    private static string ExtractBusinessFunctionHeaderCode(byte[] payload, string objectName)
    {
        if (payload.Length == 0)
        {
            return string.Empty;
        }

        return TryExtractHeaderFromArchivePayload(payload, objectName, out string headerCode)
            ? headerCode
            : string.Empty;
    }

    private static bool TryExtractSourceFromArchivePayload(byte[] payload, string objectName, out string sourceCode)
    {
        sourceCode = string.Empty;
        if (payload.Length < 64 || string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        string normalizedObject = NormalizeText(objectName);
        if (string.IsNullOrWhiteSpace(normalizedObject))
        {
            return false;
        }

        if (TryExtractSourceFromZipPayload(payload, normalizedObject, 0, out sourceCode))
        {
            return true;
        }

        string[] entryCandidates =
        {
            $"source64/{normalizedObject}.c",
            $"source/{normalizedObject}.c",
            $"source64\\{normalizedObject}.c",
            $"source\\{normalizedObject}.c"
        };

        foreach (string entryName in entryCandidates)
        {
            if (!TryExtractArchiveEntry(payload, entryName, out byte[] entryBytes))
            {
                continue;
            }

            string text = NormalizeExtractedText(DecodePayloadAsUtf8(entryBytes));
            if (string.IsNullOrWhiteSpace(text))
            {
                text = NormalizeExtractedText(DecodePayloadAsAscii(entryBytes));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            sourceCode = text;
            return true;
        }

        return false;
    }

    private static bool TryExtractSourceFromZipPayload(byte[] payload, string objectName, int depth, out string sourceCode)
    {
        sourceCode = string.Empty;
        if (depth > MaxArchiveExtractionDepth || payload.Length < 4)
        {
            return false;
        }

        using (var directStream = new MemoryStream(payload, writable: false))
        {
            if (TryExtractSourceFromZipStream(directStream, objectName, depth, out sourceCode))
            {
                return true;
            }
        }

        int searchOffset = 0;
        while (searchOffset >= 0 && searchOffset < payload.Length)
        {
            int zipOffset = IndexOfSequence(payload, ZipLocalFileHeaderSignature, searchOffset);
            if (zipOffset < 0)
            {
                break;
            }

            searchOffset = zipOffset + 1;
            if (zipOffset <= 0)
            {
                continue;
            }

            using var slicedStream = new MemoryStream(payload, zipOffset, payload.Length - zipOffset, writable: false);
            if (TryExtractSourceFromZipStream(slicedStream, objectName, depth, out sourceCode))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractSourceFromZipStream(Stream stream, string objectName, int depth, out string sourceCode)
    {
        sourceCode = string.Empty;
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count == 0)
            {
                return false;
            }

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string normalizedEntryName = NormalizeArchiveEntryName(entry.FullName);
                if (string.IsNullOrWhiteSpace(normalizedEntryName))
                {
                    continue;
                }

                if (IsBusinessFunctionSourceEntry(normalizedEntryName, objectName, ".c"))
                {
                    if (TryReadArchiveEntryText(entry, out string sourceText) && !string.IsNullOrWhiteSpace(sourceText))
                    {
                        sourceCode = sourceText;
                        return true;
                    }
                }

                if (depth >= MaxArchiveExtractionDepth ||
                    !normalizedEntryName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryReadArchiveEntryBytes(entry, out byte[] nestedPayload) || nestedPayload.Length == 0)
                {
                    continue;
                }

                if (TryExtractSourceFromZipPayload(nestedPayload, objectName, depth + 1, out string nestedSource) &&
                    !string.IsNullOrWhiteSpace(nestedSource))
                {
                    sourceCode = nestedSource;
                    return true;
                }
            }

            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryExtractHeaderFromArchivePayload(byte[] payload, string objectName, out string headerCode)
    {
        headerCode = string.Empty;
        if (payload.Length < 64 || string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        string normalizedObject = NormalizeText(objectName);
        if (string.IsNullOrWhiteSpace(normalizedObject))
        {
            return false;
        }

        if (TryExtractHeaderFromZipPayload(payload, normalizedObject, 0, out headerCode))
        {
            return true;
        }

        string[] entryCandidates =
        {
            $"include64/{normalizedObject}.h",
            $"include/{normalizedObject}.h",
            $"include64\\{normalizedObject}.h",
            $"include\\{normalizedObject}.h"
        };

        foreach (string entryName in entryCandidates)
        {
            if (!TryExtractArchiveEntry(payload, entryName, out byte[] entryBytes))
            {
                continue;
            }

            string text = NormalizeExtractedText(DecodePayloadAsUtf8(entryBytes));
            if (string.IsNullOrWhiteSpace(text))
            {
                text = NormalizeExtractedText(DecodePayloadAsAscii(entryBytes));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            headerCode = text;
            return true;
        }

        return false;
    }

    private static bool TryExtractHeaderFromZipPayload(byte[] payload, string objectName, int depth, out string headerCode)
    {
        headerCode = string.Empty;
        if (depth > MaxArchiveExtractionDepth || payload.Length < 4)
        {
            return false;
        }

        using (var directStream = new MemoryStream(payload, writable: false))
        {
            if (TryExtractHeaderFromZipStream(directStream, objectName, depth, out headerCode))
            {
                return true;
            }
        }

        int searchOffset = 0;
        while (searchOffset >= 0 && searchOffset < payload.Length)
        {
            int zipOffset = IndexOfSequence(payload, ZipLocalFileHeaderSignature, searchOffset);
            if (zipOffset < 0)
            {
                break;
            }

            searchOffset = zipOffset + 1;
            if (zipOffset <= 0)
            {
                continue;
            }

            using var slicedStream = new MemoryStream(payload, zipOffset, payload.Length - zipOffset, writable: false);
            if (TryExtractHeaderFromZipStream(slicedStream, objectName, depth, out headerCode))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryExtractHeaderFromZipStream(Stream stream, string objectName, int depth, out string headerCode)
    {
        headerCode = string.Empty;
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count == 0)
            {
                return false;
            }

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string normalizedEntryName = NormalizeArchiveEntryName(entry.FullName);
                if (string.IsNullOrWhiteSpace(normalizedEntryName))
                {
                    continue;
                }

                if (IsBusinessFunctionSourceEntry(normalizedEntryName, objectName, ".h"))
                {
                    if (TryReadArchiveEntryText(entry, out string headerText) && !string.IsNullOrWhiteSpace(headerText))
                    {
                        headerCode = headerText;
                        return true;
                    }
                }

                if (depth >= MaxArchiveExtractionDepth ||
                    !normalizedEntryName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryReadArchiveEntryBytes(entry, out byte[] nestedPayload) || nestedPayload.Length == 0)
                {
                    continue;
                }

                if (TryExtractHeaderFromZipPayload(nestedPayload, objectName, depth + 1, out string nestedHeader) &&
                    !string.IsNullOrWhiteSpace(nestedHeader))
                {
                    headerCode = nestedHeader;
                    return true;
                }
            }

            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryReadArchiveEntryBytes(ZipArchiveEntry entry, out byte[] entryBytes)
    {
        entryBytes = Array.Empty<byte>();
        try
        {
            using Stream entryStream = entry.Open();
            long expectedLength = entry.Length > 0 && entry.Length < 64 * 1024 * 1024 ? entry.Length : 0;
            using var output = expectedLength > 0
                ? new MemoryStream((int)expectedLength)
                : new MemoryStream();
            entryStream.CopyTo(output);
            entryBytes = output.ToArray();
            return entryBytes.Length > 0;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryReadArchiveEntryText(ZipArchiveEntry entry, out string text)
    {
        text = string.Empty;
        if (!TryReadArchiveEntryBytes(entry, out byte[] entryBytes))
        {
            return false;
        }

        text = NormalizeExtractedText(DecodePayloadAsUtf8(entryBytes));
        if (!string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        text = NormalizeExtractedText(DecodePayloadAsAscii(entryBytes));
        return !string.IsNullOrWhiteSpace(text);
    }

    private static bool IsBusinessFunctionSourceEntry(string normalizedEntryName, string objectName, string extension)
    {
        string fileName = $"{objectName}{extension}";
        string[] prefixes = { "source64/", "source/", "include64/", "include/" };
        foreach (string prefix in prefixes)
        {
            string target = prefix + fileName;
            if (normalizedEntryName.Equals(target, StringComparison.OrdinalIgnoreCase) ||
                normalizedEntryName.EndsWith("/" + target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeArchiveEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return string.Empty;
        }

        return entryName.Replace('\\', '/').TrimStart('/');
    }

    private static bool TryExtractArchiveEntry(byte[] payload, string entryName, out byte[] entryBytes)
    {
        entryBytes = Array.Empty<byte>();
        byte[] nameBytes = Encoding.ASCII.GetBytes(entryName);
        int searchOffset = 0;
        while (searchOffset >= 0 && searchOffset < payload.Length)
        {
            int nameOffset = IndexOfSequenceAsciiIgnoreCase(payload, nameBytes, searchOffset);
            if (nameOffset < 0)
            {
                break;
            }

            searchOffset = nameOffset + 1;
            int headerOffset = nameOffset - 30;
            if (headerOffset < 0 || headerOffset + 30 > payload.Length)
            {
                continue;
            }

            if (ReadUInt32LittleEndian(payload, headerOffset) != 0x04034B50)
            {
                continue;
            }

            int fileNameLength = ReadUInt16LittleEndian(payload, headerOffset + 26);
            int extraLength = ReadUInt16LittleEndian(payload, headerOffset + 28);
            if (fileNameLength <= 0)
            {
                continue;
            }

            int compressionMethod = ReadUInt16LittleEndian(payload, headerOffset + 8);
            int generalPurposeBitFlags = ReadUInt16LittleEndian(payload, headerOffset + 6);
            int compressedSize = unchecked((int)ReadUInt32LittleEndian(payload, headerOffset + 18));
            int uncompressedSize = unchecked((int)ReadUInt32LittleEndian(payload, headerOffset + 22));
            int dataOffset = nameOffset + fileNameLength + extraLength;
            if (dataOffset < 0 || dataOffset > payload.Length)
            {
                continue;
            }

            if (compressedSize <= 0 ||
                dataOffset + compressedSize > payload.Length ||
                (generalPurposeBitFlags & 0x0008) != 0)
            {
                int nextOffset = FindNextZipStructureOffset(payload, dataOffset + 1);
                compressedSize = Math.Max(0, nextOffset - dataOffset);
            }

            if (compressedSize <= 0 || dataOffset + compressedSize > payload.Length)
            {
                continue;
            }

            byte[] compressedData = new byte[compressedSize];
            Buffer.BlockCopy(payload, dataOffset, compressedData, 0, compressedSize);

            if (compressionMethod == 0)
            {
                entryBytes = compressedData;
                return true;
            }

            if (compressionMethod == 8 &&
                TryInflateDeflate(compressedData, uncompressedSize, out byte[] inflated) &&
                inflated.Length > 0)
            {
                entryBytes = inflated;
                return true;
            }
        }

        return false;
    }

    private static int FindNextZipStructureOffset(byte[] payload, int startOffset)
    {
        int nextOffset = payload.Length;
        int localOffset = IndexOfSequence(payload, ZipLocalFileHeaderSignature, startOffset);
        if (localOffset >= 0 && localOffset < nextOffset)
        {
            nextOffset = localOffset;
        }

        int centralOffset = IndexOfSequence(payload, ZipCentralDirectoryHeaderSignature, startOffset);
        if (centralOffset >= 0 && centralOffset < nextOffset)
        {
            nextOffset = centralOffset;
        }

        int endOffset = IndexOfSequence(payload, ZipEndOfCentralDirectorySignature, startOffset);
        if (endOffset >= 0 && endOffset < nextOffset)
        {
            nextOffset = endOffset;
        }

        return nextOffset;
    }

    private static bool TryInflateDeflate(byte[] compressedData, int uncompressedSizeHint, out byte[] inflated)
    {
        inflated = Array.Empty<byte>();
        try
        {
            using var input = new MemoryStream(compressedData, writable: false);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: false);
            int bufferSize = uncompressedSizeHint > 0 && uncompressedSizeHint < 64 * 1024 * 1024
                ? uncompressedSizeHint
                : Math.Min(compressedData.Length * 4, 64 * 1024 * 1024);
            if (bufferSize <= 0)
            {
                bufferSize = 4096;
            }

            using var output = new MemoryStream(bufferSize);
            deflate.CopyTo(output);
            inflated = output.ToArray();
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static int IndexOfSequence(byte[] buffer, byte[] pattern, int startIndex)
    {
        if (pattern.Length == 0 || buffer.Length == 0 || startIndex >= buffer.Length)
        {
            return -1;
        }

        int lastStart = buffer.Length - pattern.Length;
        for (int i = Math.Max(0, startIndex); i <= lastStart; i++)
        {
            bool matched = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (buffer[i + j] == pattern[j])
                {
                    continue;
                }

                matched = false;
                break;
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfSequenceAsciiIgnoreCase(byte[] buffer, byte[] pattern, int startIndex)
    {
        if (pattern.Length == 0 || buffer.Length == 0 || startIndex >= buffer.Length)
        {
            return -1;
        }

        int lastStart = buffer.Length - pattern.Length;
        for (int i = Math.Max(0, startIndex); i <= lastStart; i++)
        {
            bool matched = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                byte left = buffer[i + j];
                byte right = pattern[j];
                if (left == right)
                {
                    continue;
                }

                byte leftLower = left is >= (byte)'A' and <= (byte)'Z'
                    ? (byte)(left + 32)
                    : left;
                byte rightLower = right is >= (byte)'A' and <= (byte)'Z'
                    ? (byte)(right + 32)
                    : right;
                if (leftLower == rightLower)
                {
                    continue;
                }

                matched = false;
                break;
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }

    private static ushort ReadUInt16LittleEndian(byte[] data, int offset)
    {
        if (offset < 0 || offset + 2 > data.Length)
        {
            return 0;
        }

        return (ushort)(data[offset] | (data[offset + 1] << 8));
    }

    private static uint ReadUInt32LittleEndian(byte[] data, int offset)
    {
        if (offset < 0 || offset + 4 > data.Length)
        {
            return 0;
        }

        return (uint)(
            data[offset] |
            (data[offset + 1] << 8) |
            (data[offset + 2] << 16) |
            (data[offset + 3] << 24));
    }

    private static void AddDecodedCandidate(List<string> candidates, HashSet<string> seen, string candidate)
    {
        string normalized = NormalizeExtractedText(candidate);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (seen.Add(normalized))
        {
            candidates.Add(normalized);
        }
    }

    private static string DecodePayloadAsUnicode(byte[] payload)
    {
        return payload.Length == 0 ? string.Empty : Encoding.Unicode.GetString(payload);
    }

    private static string DecodePayloadAsUtf8(byte[] payload)
    {
        return payload.Length == 0 ? string.Empty : Encoding.UTF8.GetString(payload);
    }

    private static string DecodePayloadAsAscii(byte[] payload)
    {
        return payload.Length == 0 ? string.Empty : Encoding.ASCII.GetString(payload);
    }

    private static string NormalizeExtractedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (char ch in text)
        {
            if (ch == '\0')
            {
                continue;
            }

            if (ch == '\r' || ch == '\n' || ch == '\t' || !char.IsControl(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Trim();
    }

    private static string ExtractLikelyCodeSegment(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string[] markers =
        {
            "#include",
            "JDEBFRTN",
            "JDEBFWINAPI",
            "static ",
            "void ",
            "int ",
            "BOOL ",
            "ID ",
            "/*"
        };

        int start = -1;
        foreach (string marker in markers)
        {
            int index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            if (start < 0 || index < start)
            {
                start = index;
            }
        }

        return start <= 0 ? text.Trim() : text.Substring(start).Trim();
    }

    private static int ScoreCodeCandidate(string text, string functionName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return int.MinValue;
        }

        int score = 0;
        if (text.Contains("#include", StringComparison.Ordinal))
        {
            score += 12;
        }

        if (text.Contains("JDEBFWINAPI", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("JDEBFRTN", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(functionName) &&
            text.Contains(functionName, StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        if (text.Contains("{", StringComparison.Ordinal) && text.Contains("}", StringComparison.Ordinal))
        {
            score += 6;
        }

        if (text.Contains(";", StringComparison.Ordinal))
        {
            score += 4;
        }

        int newlineCount = text.Count(ch => ch == '\n');
        score += Math.Min(newlineCount, 20);

        int printableChars = text.Count(ch => ch == '\r' || ch == '\n' || ch == '\t' || !char.IsControl(ch));
        int printablePercent = text.Length == 0 ? 0 : (printableChars * 100) / text.Length;
        score += printablePercent / 10;
        if (printablePercent < 60)
        {
            score -= 12;
        }

        return score;
    }

    private static bool LooksLikeCSource(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.Contains("#include", StringComparison.Ordinal) ||
            text.Contains("JDEBFWINAPI", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("JDEBFRTN", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!(text.Contains("{", StringComparison.Ordinal) &&
              text.Contains("}", StringComparison.Ordinal) &&
              text.Contains(";", StringComparison.Ordinal)))
        {
            return false;
        }

        int keywordHits = 0;
        if (text.Contains("static ", StringComparison.Ordinal)) keywordHits++;
        if (text.Contains(" void ", StringComparison.Ordinal) || text.StartsWith("void ", StringComparison.Ordinal)) keywordHits++;
        if (text.Contains(" int ", StringComparison.Ordinal) || text.StartsWith("int ", StringComparison.Ordinal)) keywordHits++;
        if (text.Contains(" char ", StringComparison.Ordinal) || text.StartsWith("char ", StringComparison.Ordinal)) keywordHits++;
        if (text.Contains(" BOOL ", StringComparison.Ordinal)) keywordHits++;

        return keywordHits >= 2;
    }

    private string? TryConvertSpecDataToXml(IntPtr hConvert, ref JdeSpecData specData)
    {
        if (specData.SpecData == IntPtr.Zero || specData.DataLen == 0)
        {
            return null;
        }
        string? directXml = TryConvertSpecDataToXmlDirect(hConvert, ref specData);
        if (!string.IsNullOrWhiteSpace(directXml))
        {
            return directXml;
        }
        int insertResult = JdeSpecEncapApi.jdeSpecInsertRecordToConsolidatedBuffer(hConvert, ref specData);
        if (insertResult != JDESPEC_SUCCESS)
        {
            LogSpecDebug($"[ER] InsertRecord failed: {insertResult} ({GetSpecResultText(insertResult)})");
            return null;
        }
        var xmlData = new JdeSpecData();
        try
        {
            int result = JdeSpecEncapApi.jdeSpecConvertConsolidatedToXML(hConvert, ref xmlData);
            if (result != JDESPEC_SUCCESS || xmlData.SpecData == IntPtr.Zero)
            {
                LogSpecConvertFailure(hConvert, result, specData.DataType);
                return null;
            }
            return ReadUtf8Xml(xmlData);
        }
        finally
        {
            if (xmlData.SpecData != IntPtr.Zero)
            {
                JdeSpecEncapApi.jdeSpecFreeData(ref xmlData);
            }
        }
    }

    private string? TryConvertSpecDataToXmlDirect(IntPtr hConvert, ref JdeSpecData specData)
    {
        if (specData.SpecData == IntPtr.Zero || specData.DataLen == 0)
        {
            return null;
        }
        var xmlData = new JdeSpecData();
        try
        {
            int result = JdeSpecEncapApi.jdeSpecConvertToXML_UTF16(hConvert, ref specData, ref xmlData);
            if (result != JDESPEC_SUCCESS || xmlData.SpecData == IntPtr.Zero)
            {
                LogSpecConvertFailure(hConvert, result, specData.DataType);
                return null;
            }
            return ReadUnicodeXml(xmlData);
        }
        finally
        {
            if (xmlData.SpecData != IntPtr.Zero)
            {
                JdeSpecEncapApi.jdeSpecFreeData(ref xmlData);
            }
        }
    }

    private static string? ReadUtf8Xml(JdeSpecData xmlData)
    {
        if (xmlData.SpecData == IntPtr.Zero)
        {
            return null;
        }
        int byteLength = xmlData.DataLen > int.MaxValue ? int.MaxValue : (int)xmlData.DataLen;
        if (byteLength > 0)
        {
            var buffer = new byte[byteLength];
            Marshal.Copy(xmlData.SpecData, buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer);
        }
        return Marshal.PtrToStringUTF8(xmlData.SpecData);
    }

    private static string? ReadUnicodeXml(JdeSpecData xmlData)
    {
        if (xmlData.SpecData == IntPtr.Zero)
        {
            return null;
        }
        int byteLength = xmlData.DataLen > int.MaxValue ? int.MaxValue : (int)xmlData.DataLen;
        if (byteLength > 0)
        {
            var buffer = new byte[byteLength];
            Marshal.Copy(xmlData.SpecData, buffer, 0, buffer.Length);
            return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        }
        return Marshal.PtrToStringUni(xmlData.SpecData);
    }

    private void LogSpecConvertFailure(IntPtr hConvert, int result, JdeSpecDataType attemptType)
    {
        if (!_options.EnableDebug && !_options.EnableSpecDebug)
        {
            return;
        }
        string resultText = GetSpecResultText(result);
        var lastError = new JdeSpecLastError();
        int lastErrorResult = JdeSpecEncapApi.jdeSpecGetLastErrorInfo(hConvert, ref lastError);
        if (lastErrorResult == JDESPEC_SUCCESS)
        {
            LogSpecDebug($"[ER] XML convert failed: result={result} ({resultText}), type={attemptType}, last={lastError.Result}, db={lastError.DbType}, extra={lastError.ExtraInfo}");
        }
        else
        {
            LogSpecDebug($"[ER] XML convert failed: result={result} ({resultText}), type={attemptType}, lastErrorResult={lastErrorResult}");
        }
    }

    private static string GetSpecResultText(int result)
    {
        var buffer = new StringBuilder(256);
        int status = JdeSpecEncapApi.jdeSpecGetResultText(buffer, buffer.Capacity, result);
        return status == JDESPEC_SUCCESS ? buffer.ToString() : "Unknown";
    }

    private void LogSpecDebug(string message)
    {
        if (_options.EnableSpecDebug)
        {
            _options.WriteLog(message);
        }
    }

    private static void AddXmlToBuilders(
        Dictionary<string, XmlDocumentBuilder> buildersByKey,
        List<XmlDocumentBuilder> orderedBuilders,
        string xml,
        string fallbackKey)
    {
        if (!TryParseXmlRoot(xml, out XElement? root))
        {
            XmlDocumentBuilder builder = GetOrCreateBuilder(buildersByKey, orderedBuilders, fallbackKey);
            builder.AddRaw(xml);
            return;
        }
        string key = root.Attribute("szEventSpecKey")?.Value ?? fallbackKey;
        XmlDocumentBuilder target = GetOrCreateBuilder(buildersByKey, orderedBuilders, key);
        target.AddRoot(root);
    }

    private static XmlDocumentBuilder GetOrCreateBuilder(
        Dictionary<string, XmlDocumentBuilder> buildersByKey,
        List<XmlDocumentBuilder> orderedBuilders,
        string eventSpecKey)
    {
        if (!buildersByKey.TryGetValue(eventSpecKey, out XmlDocumentBuilder? builder))
        {
            builder = new XmlDocumentBuilder(eventSpecKey);
            buildersByKey[eventSpecKey] = builder;
            orderedBuilders.Add(builder);
        }
        return builder;
    }

    private static bool TryParseXmlRoot(string xml, out XElement? root)
    {
        root = null;
        if (string.IsNullOrWhiteSpace(xml))
        {
            return false;
        }
        try
        {
            var document = XDocument.Parse(xml);
            root = document.Root;
            return root != null;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static void AppendXmlLines(List<JdeEventRuleLine> lines, string xml, int sequence)
    {
        foreach (string line in SplitLines(PrettyPrintXml(xml)))
        {
            lines.Add(new JdeEventRuleLine
            {
                Sequence = sequence,
                RecordType = 0,
                Text = line
            });
        }
        lines.Add(new JdeEventRuleLine
        {
            Sequence = sequence,
            RecordType = 0,
            Text = string.Empty
        });
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return string.Empty;
            yield break;
        }
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n', StringSplitOptions.None);
        foreach (string line in lines)
        {
            yield return line;
        }
    }

    private static string PrettyPrintXml(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return string.Empty;
        }
        try
        {
            var document = XDocument.Parse(xml);
            string formatted = document.ToString();
            if (document.Declaration != null)
            {
                return $"{document.Declaration}{Environment.NewLine}{formatted}";
            }
            return formatted;
        }
        catch (XmlException)
        {
            return xml;
        }
    }
    private sealed class XmlDocumentBuilder
    {

        private readonly List<XAttribute> _rootAttributes = new();

        private readonly List<XNode> _children = new();

        private readonly List<string> _rawFragments = new();

        public XmlDocumentBuilder(string eventSpecKey)
        {
            EventSpecKey = eventSpecKey ?? string.Empty;
        }
        public string EventSpecKey { get; }
        public int RecordCount { get; private set; }
        private XName? RootName { get; set; }

        public void AddRoot(XElement root)
        {
            if (RootName == null)
            {
                RootName = root.Name;
                foreach (var attribute in root.Attributes())
                {
                    _rootAttributes.Add(new XAttribute(attribute));
                }
            }
            foreach (var node in root.Nodes())
            {
                _children.Add(CloneNode(node));
            }
            RecordCount++;
        }

        public void AddRaw(string xml)
        {
            if (!string.IsNullOrWhiteSpace(xml))
            {
                _rawFragments.Add(xml);
                RecordCount++;
            }
        }

        public string BuildXml()
        {
            if (RootName == null)
            {
                return string.Join(Environment.NewLine + Environment.NewLine, _rawFragments);
            }
            var root = new XElement(RootName);
            foreach (var attribute in _rootAttributes)
            {
                root.Add(new XAttribute(attribute));
            }
            foreach (var node in _children)
            {
                root.Add(node);
            }
            var document = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
            string formatted = document.ToString();
            return document.Declaration != null
                ? $"{document.Declaration}{Environment.NewLine}{formatted}"
                : formatted;
        }

        private static XNode CloneNode(XNode node)
        {
            return node switch
            {
                XElement element => new XElement(element),
                XCData cdata => new XCData(cdata.Value),
                XText text => new XText(text.Value),
                XComment comment => new XComment(comment.Value),
                XProcessingInstruction pi => new XProcessingInstruction(pi.Target, pi.Data),
                XDocumentType docType => new XDocumentType(docType.Name, docType.PublicId, docType.SystemId, docType.InternalSubset),
                _ => new XText(node.ToString())
            };
        }
    }

    /// <summary>
    /// Retrieve diagnostics for decoding a specific event spec key.
    /// </summary>
    public IReadOnlyList<JdeEventRulesDecodeDiagnostics> GetEventRulesDecodeDiagnostics(string eventSpecKey)
    {
        if (string.IsNullOrWhiteSpace(eventSpecKey))
        {
            return Array.Empty<JdeEventRulesDecodeDiagnostics>();
        }
        var results = new List<JdeEventRulesDecodeDiagnostics>();
        HREQUEST hRequest = OpenTable(F98741Structures.TableName, new ID(F98741Structures.IdEventRulesSpecsUuid));
        try
        {
            var key = new F98741Structures.Key2
            {
                EventSpecKey = eventSpecKey,
                EventSequence = 0
            };
            int keySize = Marshal.SizeOf<F98741Structures.Key2>();
            IntPtr keyPtr = Marshal.AllocHGlobal(keySize);
            try
            {
                Marshal.StructureToPtr(key, keyPtr, false);
                int keyResult = JDB_SelectKeyed(hRequest, new ID(F98741Structures.IdEventRulesSpecsUuid), keyPtr, 1);
                if (keyResult != JDEDB_PASSED)
                {
                    return Array.Empty<JdeEventRulesDecodeDiagnostics>();
                }
            }
            finally
            {
                Marshal.FreeHGlobal(keyPtr);
            }
            while (JDB_Fetch(hRequest, IntPtr.Zero, 0) == JDEDB_PASSED)
            {
                int sequence = ReadColumnInt32(hRequest, F98741Structures.TableName, F98741Structures.Columns.EventSequence);
                byte[] blob = ReadBlob(hRequest, F98741Structures.TableName, F98741Structures.Columns.EventBlob);
                if (blob.Length == 0)
                {
                    continue;
                }
                var diagnostic = new JdeEventRulesDecodeDiagnostics
                {
                    Sequence = sequence,
                    BlobSize = blob.Length,
                    HeadHex = ToHex(blob, 64),
                    RawLooksLikeGbrSpec = GbrSpecParser.LooksLikeGbrSpec(blob),
                    RawLittleEndian = CreateUnpackAttempt(blob, JdeByteOrder.LittleEndian),
                    RawBigEndian = CreateUnpackAttempt(blob, JdeByteOrder.BigEndian),
                    RawB733LittleEndian = CreateB733Attempt(blob, JdeByteOrder.LittleEndian),
                    RawB733BigEndian = CreateB733Attempt(blob, JdeByteOrder.BigEndian)
                };
                if (TryUncompress(blob, out byte[] uncompressed))
                {
                    diagnostic.Uncompressed = true;
                    diagnostic.UncompressedSize = uncompressed.Length;
                    diagnostic.UncompressedLooksLikeGbrSpec = GbrSpecParser.LooksLikeGbrSpec(uncompressed);
                    diagnostic.UncompressedLittleEndian = CreateUnpackAttempt(uncompressed, JdeByteOrder.LittleEndian);
                    diagnostic.UncompressedBigEndian = CreateUnpackAttempt(uncompressed, JdeByteOrder.BigEndian);
                    diagnostic.UncompressedB733LittleEndian = CreateB733Attempt(uncompressed, JdeByteOrder.LittleEndian);
                    diagnostic.UncompressedB733BigEndian = CreateB733Attempt(uncompressed, JdeByteOrder.BigEndian);
                }
                results.Add(diagnostic);
            }
        }
        finally
        {
            JDB_CloseTable(hRequest);
        }
        return results
            .OrderBy(result => result.Sequence)
            .ToList();
    }

    private static JdeEventRulesDecodeDiagnostics.UnpackAttempt CreateUnpackAttempt(byte[] blob, JdeByteOrder byteOrder)
    {
        if (blob.Length == 0)
        {
            return JdeEventRulesDecodeDiagnostics.UnpackAttempt.Empty;
        }
        if (!EnableNativeSpecUnpack)
        {
            return new JdeEventRulesDecodeDiagnostics.UnpackAttempt
            {
                Status = JdeUnpackSpecStatus.UnresolvedBlobFormat,
                Error = "Native unpack disabled"
            };
        }
        IntPtr packedPtr = IntPtr.Zero;
        IntPtr unpackedPtr = IntPtr.Zero;
        bool shouldFree = false;
        var attempt = new JdeEventRulesDecodeDiagnostics.UnpackAttempt();
        try
        {
            packedPtr = Marshal.AllocHGlobal(blob.Length);
            Marshal.Copy(blob, 0, packedPtr, blob.Length);
            uint codePage = GetLocalCodePage();
            int osType = GetLocalOsType();
            JdeKernelApi.jdeUnpackSpec(
                packedPtr,
                JdeSpecType.GbrSpec,
                codePage,
                osType,
                codePage,
                osType,
                byteOrder,
                out unpackedPtr,
                out JdeUnpackSpecStatus status);
            attempt.Status = status;
            if (status != JdeUnpackSpecStatus.Success || unpackedPtr == IntPtr.Zero)
            {
                return attempt;
            }
            shouldFree = true;
            if (!TryReadUnpackedLength(unpackedPtr, out int length))
            {
                return attempt;
            }
            byte[] buffer = new byte[length];
            Marshal.Copy(unpackedPtr, buffer, 0, length);
            attempt.UnpackedLength = length;
            attempt.LooksLikeGbrSpec = GbrSpecParser.LooksLikeGbrSpec(buffer);
            return attempt;
        }
        catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
        {
            attempt.Status = JdeUnpackSpecStatus.UnresolvedBlobFormat;
            attempt.Error = ex.Message;
            return attempt;
        }
        finally
        {
            if (packedPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(packedPtr);
            }
            if (shouldFree && unpackedPtr != IntPtr.Zero)
            {
                TryFreeUnpackedSpec(unpackedPtr);
            }
        }
    }

    private static JdeEventRulesDecodeDiagnostics.B733UnpackAttempt CreateB733Attempt(byte[] blob, JdeByteOrder byteOrder)
    {
        TryUnpackGbrSpecB733(blob, byteOrder, out _, out JdeEventRulesDecodeDiagnostics.B733UnpackAttempt attempt);
        return attempt;
    }

    private static string ToHex(byte[] data, int maxBytes)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }
        int count = Math.Min(data.Length, maxBytes);
        var builder = new StringBuilder(count * 3);
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }
            builder.Append(data[i].ToString("X2"));
        }
        return builder.ToString();
    }
    private const int MaxUnpackedSpecBytes = 8 * 1024 * 1024;
    private const bool EnableNativeSpecUnpack = false;
    private static readonly JdeSpecType[] GbrSpecTypes = { JdeSpecType.GbrSpec, JdeSpecType.GbrSpecLegacy };

    private static byte[] TryUnpackGbrSpec(byte[] blob)
    {
        if (blob.Length == 0)
        {
            return blob;
        }
        if (TryUnpackGbrSpec(blob, JdeByteOrder.LittleEndian, out byte[] unpacked))
        {
            return unpacked;
        }
        if (TryUnpackGbrSpec(blob, JdeByteOrder.BigEndian, out unpacked))
        {
            return unpacked;
        }
        if (TryUnpackGbrSpecB733(blob, JdeByteOrder.LittleEndian, out unpacked, out _))
        {
            return unpacked;
        }
        if (TryUnpackGbrSpecB733(blob, JdeByteOrder.BigEndian, out unpacked, out _))
        {
            return unpacked;
        }
        if (TryUncompress(blob, out byte[] uncompressed))
        {
            if (TryUnpackGbrSpec(uncompressed, JdeByteOrder.LittleEndian, out unpacked))
            {
                return unpacked;
            }
            if (TryUnpackGbrSpec(uncompressed, JdeByteOrder.BigEndian, out unpacked))
            {
                return unpacked;
            }
            if (TryUnpackGbrSpecB733(uncompressed, JdeByteOrder.LittleEndian, out unpacked, out _))
            {
                return unpacked;
            }
            if (TryUnpackGbrSpecB733(uncompressed, JdeByteOrder.BigEndian, out unpacked, out _))
            {
                return unpacked;
            }
            if (GbrSpecParser.LooksLikeGbrSpec(uncompressed))
            {
                return uncompressed;
            }
        }
        if (GbrSpecParser.LooksLikeGbrSpec(blob))
        {
            return blob;
        }
        return Array.Empty<byte>();
    }

    private static bool TryUnpackGbrSpec(byte[] blob, JdeByteOrder byteOrder, out byte[] unpacked)
    {
        if (!EnableNativeSpecUnpack)
        {
            unpacked = Array.Empty<byte>();
            return false;
        }
        foreach (var specType in GbrSpecTypes)
        {
            if (TryUnpackGbrSpec(blob, byteOrder, specType, out unpacked))
            {
                return true;
            }
        }
        unpacked = Array.Empty<byte>();
        return false;
    }

    private static bool TryUnpackGbrSpec(byte[] blob, JdeByteOrder byteOrder, JdeSpecType specType, out byte[] unpacked)
    {
        unpacked = Array.Empty<byte>();
        IntPtr packedPtr = IntPtr.Zero;
        IntPtr unpackedPtr = IntPtr.Zero;
        JdeUnpackSpecStatus status = JdeUnpackSpecStatus.Success;
        bool shouldFree = false;
        try
        {
            packedPtr = Marshal.AllocHGlobal(blob.Length);
            Marshal.Copy(blob, 0, packedPtr, blob.Length);
            uint codePage = GetLocalCodePage();
            int osType = GetLocalOsType();
            JdeKernelApi.jdeUnpackSpec(
                packedPtr,
                specType,
                codePage,
                osType,
                codePage,
                osType,
                byteOrder,
                out unpackedPtr,
                out status);
            if (status != JdeUnpackSpecStatus.Success || unpackedPtr == IntPtr.Zero)
            {
                return false;
            }
            shouldFree = true;
            if (!TryReadUnpackedLength(unpackedPtr, out int length))
            {
                return false;
            }
            unpacked = new byte[length];
            Marshal.Copy(unpackedPtr, unpacked, 0, length);
            return GbrSpecParser.LooksLikeGbrSpec(unpacked);
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            if (packedPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(packedPtr);
            }
            if (shouldFree && unpackedPtr != IntPtr.Zero)
            {
                TryFreeUnpackedSpec(unpackedPtr);
            }
        }
    }

    private static bool TryUnpackGbrSpecB733(byte[] blob, JdeByteOrder byteOrder, out byte[] unpacked, out JdeEventRulesDecodeDiagnostics.B733UnpackAttempt attempt)
    {
        if (TryUnpackGbrSpecB733(blob, byteOrder, JdeSpecType.GbrSpec, out unpacked, out attempt))
        {
            return true;
        }
        return TryUnpackGbrSpecB733(blob, byteOrder, JdeSpecType.GbrSpecLegacy, out unpacked, out attempt);
    }

    private static bool TryUnpackGbrSpecB733(byte[] blob, JdeByteOrder byteOrder, JdeSpecType specType, out byte[] unpacked, out JdeEventRulesDecodeDiagnostics.B733UnpackAttempt attempt)
    {
        unpacked = Array.Empty<byte>();
        attempt = new JdeEventRulesDecodeDiagnostics.B733UnpackAttempt();
        if (blob.Length == 0)
        {
            return false;
        }
        attempt.Status = JdeB733UnpackSpecStatus.UnknownSpecType;
        uint codePage = GetLocalCodePage();
        int osType = GetLocalOsType();
        attempt.CodePage = codePage;
        attempt.OsType = osType;
        IntPtr packedPtr = IntPtr.Zero;
        IntPtr unpackedPtr = IntPtr.Zero;
        bool shouldFree = false;
        try
        {
            packedPtr = Marshal.AllocHGlobal(blob.Length);
            Marshal.Copy(blob, 0, packedPtr, blob.Length);
            JdeKernelApi.jdeB733UnpackSpec(
                packedPtr,
                specType,
                codePage,
                osType,
                codePage,
                osType,
                byteOrder,
                out unpackedPtr,
                out JdeB733UnpackSpecStatus status);
            attempt.Status = status;
            if (status != JdeB733UnpackSpecStatus.Success || unpackedPtr == IntPtr.Zero)
            {
                return false;
            }
            shouldFree = true;
            if (!TryReadUnpackedLength(unpackedPtr, out int length))
            {
                return false;
            }
            unpacked = new byte[length];
            Marshal.Copy(unpackedPtr, unpacked, 0, length);
            attempt.UnpackedLength = length;
            attempt.LooksLikeGbrSpec = GbrSpecParser.LooksLikeGbrSpec(unpacked);
            return attempt.LooksLikeGbrSpec;
        }
        catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
        {
            attempt.Error = ex.Message;
            return false;
        }
        finally
        {
            if (packedPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(packedPtr);
            }
            if (shouldFree && unpackedPtr != IntPtr.Zero)
            {
                TryFreeUnpackedSpec(unpackedPtr);
            }
        }
    }

private static bool TryUncompress(byte[] blob, out byte[] uncompressed)
    {
        uncompressed = Array.Empty<byte>();
        if (blob.Length == 0)
        {
            return false;
        }
        if (TryUncompressNative(blob, out uncompressed))
        {
            return true;
        }
        return TryUncompressManaged(blob, out uncompressed);
    }

private static bool TryUncompressNative(byte[] blob, out byte[] uncompressed)
    {
        uncompressed = Array.Empty<byte>();
        IntPtr sourcePtr = IntPtr.Zero;
        IntPtr destPtr = IntPtr.Zero;
        UIntPtr destLength = UIntPtr.Zero;
        bool shouldFree = false;
        try
        {
            sourcePtr = Marshal.AllocHGlobal(blob.Length);
            Marshal.Copy(blob, 0, sourcePtr, blob.Length);
                        int result = JdeKernelApi.jdeBufferUncompress(ref destPtr, ref destLength, sourcePtr, (UIntPtr)blob.Length);
            bool success = result == 0 || result == 1;
            if (!success || destPtr == IntPtr.Zero)
            {
                return false;
            }
            ulong length = destLength.ToUInt64();
            if (destPtr == sourcePtr)
            {
                if (length == 0)
                {
                    length = (ulong)blob.Length;
                }
                if (length == 0 || length > MaxUnpackedSpecBytes || length > int.MaxValue)
                {
                    return false;
                }
                uncompressed = new byte[length];
                Marshal.Copy(destPtr, uncompressed, 0, (int)length);
                return true;
            }
            if (length == 0 || length > MaxUnpackedSpecBytes || length > int.MaxValue)
            {
                return false;
            }
            shouldFree = true;
            uncompressed = new byte[length];
            Marshal.Copy(destPtr, uncompressed, 0, (int)length);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            if (sourcePtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(sourcePtr);
            }
            if (shouldFree && destPtr != IntPtr.Zero)
            {
                TryFreeUnpackedSpec(destPtr);
            }
        }
    }

    private static bool TryUncompressManaged(byte[] blob, out byte[] uncompressed)
    {
        return TryUncompressManaged(blob, useZlib: false, out uncompressed)
            || TryUncompressManaged(blob, useZlib: true, out uncompressed);
    }

    private static bool TryUncompressManaged(byte[] blob, bool useZlib, out byte[] uncompressed)
    {
        uncompressed = Array.Empty<byte>();
        try
        {
            using var input = new MemoryStream(blob, writable: false);
            using var output = new MemoryStream();
            using Stream decoder = useZlib
                ? new ZLibStream(input, CompressionMode.Decompress, leaveOpen: true)
                : new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true);
            if (!TryCopyWithLimit(decoder, output, MaxUnpackedSpecBytes))
            {
                return false;
            }
            if (output.Length == 0)
            {
                return false;
            }
            uncompressed = output.ToArray();
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool TryCopyWithLimit(Stream source, Stream destination, int maxBytes)
    {
        byte[] buffer = new byte[8192];
        int total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                return false;
            }
            destination.Write(buffer, 0, read);
        }
        return total > 0;
    }

    private static uint GetLocalCodePage()
    {
        uint codePage = 0;
        try
        {
            codePage = JdeKernelApi.JDEGetProcessCodePage(null);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
        if (codePage == 0)
        {
            codePage = (uint)Encoding.Default.CodePage;
        }
        return codePage;
    }

    private static int GetLocalOsType()
    {
        try
        {
            return JdeKernelApi.JDEGetOSType();
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
        return 5;
    }

    private static bool TryReadUnpackedLength(IntPtr unpackedPtr, out int length)
    {
        length = 0;
        if (unpackedPtr == IntPtr.Zero)
        {
            return false;
        }
        int length32 = Marshal.ReadInt32(unpackedPtr);
        if (length32 > 0 && length32 <= MaxUnpackedSpecBytes)
        {
            length = length32;
            return true;
        }
        long length64 = Marshal.ReadInt64(unpackedPtr);
        if (length64 > 0 && length64 <= MaxUnpackedSpecBytes)
        {
            length = (int)length64;
            return true;
        }
        return false;
    }

    private static void TryFreeUnpackedSpec(IntPtr unpackedPtr)
    {
        try
        {
            JdeKernelApi.jdeFreeInternal(unpackedPtr);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private JdeEventRulesNode GetEventRulesLinkTree(string objectName, int productType)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Object name is required.", nameof(objectName));
        }
        var rows = GetEventRulesLinkRows(objectName, productType);
        if (rows.Count == 0)
        {
            return BuildRootNode(objectName, Array.Empty<JdeEventRulesNode>());
        }
        var children = BuildEventRulesLinkNodes(rows);
        return BuildRootNode(objectName, children);
    }

    private JdeEventRulesNode BuildRootNode(string objectName, IReadOnlyList<JdeEventRulesNode> children)
    {
        return new JdeEventRulesNode
        {
            Id = objectName,
            Name = objectName,
            NodeType = JdeEventRulesNodeType.Object,
            Children = children
        };
    }

    private IReadOnlyList<EventRulesLinkRow> GetEventRulesLinkRows(string objectName, int productType)
    {
        var rows = new List<EventRulesLinkRow>();
        HREQUEST hRequest = OpenTable(F98740Structures.TableName, new ID(0));
        TableLayout? layout = _options.UseRowLayoutTables
            ? TableLayoutLoader.Load(F98740Structures.TableName)
            : null;
        IntPtr rowBuffer = IntPtr.Zero;
        try
        {
            ApplyObjectNameSelection(hRequest, objectName);
            int selectResult = JDB_SelectKeyed(hRequest, new ID(0), IntPtr.Zero, 0);
            if (selectResult != JDEDB_PASSED)
            {
                return rows;
            }
            if (layout != null && layout.Size > 0)
            {
                rowBuffer = Marshal.AllocHGlobal(layout.Size + 64);
            }
            while (true)
            {
                int fetchResult = JDB_Fetch(hRequest, rowBuffer, 0);
                if (fetchResult == JDEDB_NO_MORE_DATA)
                {
                    break;
                }
                if (fetchResult == JDEDB_SKIPPED)
                {
                    continue;
                }
                if (fetchResult != JDEDB_PASSED)
                {
                    continue;
                }
                int rowProductType = ReadColumnInt32(layout, rowBuffer, hRequest, F98740Structures.TableName, F98740Structures.Columns.ProductType);
                if (rowProductType != productType)
                {
                    continue;
                }
                string eventSpecKey = ReadColumnString(layout, rowBuffer, hRequest, F98740Structures.TableName, F98740Structures.Columns.EventSpecKey, 37);
                if (string.IsNullOrWhiteSpace(eventSpecKey))
                {
                    continue;
                }
                string version = ReadColumnString(layout, rowBuffer, hRequest, F98740Structures.TableName, F98740Structures.Columns.Version, 11);
                string formName = ReadColumnString(layout, rowBuffer, hRequest, F98740Structures.TableName, F98740Structures.Columns.FormName, 11);
                int controlId = ReadColumnInt32(layout, rowBuffer, hRequest, F98740Structures.TableName, F98740Structures.Columns.ControlId);
                int id3 = ReadColumnInt32(layout, rowBuffer, hRequest, F98740Structures.TableName, F98740Structures.Columns.EventId3);
                string eventId = ReadColumnMathNumeric(layout, rowBuffer, hRequest, F98740Structures.TableName, F98740Structures.Columns.EventNumeric);
                int eventOrder = ParseEventOrder(eventId);
                rows.Add(new EventRulesLinkRow(version, formName, controlId, eventId, eventOrder, id3, eventSpecKey));
            }
        }
        finally
        {
            JDB_CloseTable(hRequest);
            if (rowBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(rowBuffer);
            }
        }
        return rows;
    }

    private static IReadOnlyList<JdeEventRulesNode> BuildEventRulesLinkNodes(IReadOnlyList<EventRulesLinkRow> rows)
    {
        var children = new List<JdeEventRulesNode>();
        if (rows.Count == 0)
        {
            return children;
        }
        bool hasVersion = rows.Any(row => !string.IsNullOrWhiteSpace(row.Version));
        bool hasForm = rows.Any(row => !string.IsNullOrWhiteSpace(row.FormName));
        if (hasVersion)
        {
            foreach (var versionGroup in rows
                .GroupBy(row => NormalizeGroupKey(row.Version))
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                var versionNode = new JdeEventRulesNode
                {
                    Id = $"version:{versionGroup.Key}",
                    Name = FormatVersionLabel(versionGroup.Key),
                    NodeType = JdeEventRulesNodeType.Section,
                    Children = BuildFormNodes(versionGroup.ToList(), hasForm)
                };
                children.Add(versionNode);
            }
            return children;
        }
        children.AddRange(BuildFormNodes(rows, hasForm));
        return children;
    }

    private static IReadOnlyList<JdeEventRulesNode> BuildFormNodes(IReadOnlyList<EventRulesLinkRow> rows, bool hasForm)
    {
        var children = new List<JdeEventRulesNode>();
        if (!hasForm)
        {
            children.AddRange(BuildControlNodes(rows));
            return children;
        }
        foreach (var formGroup in rows
            .GroupBy(row => NormalizeGroupKey(row.FormName))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var formNode = new JdeEventRulesNode
            {
                Id = $"form:{formGroup.Key}",
                Name = FormatFormLabel(formGroup.Key),
                NodeType = JdeEventRulesNodeType.Form,
                Children = BuildControlNodes(formGroup.ToList())
            };
            children.Add(formNode);
        }
        return children;
    }

    private static IReadOnlyList<JdeEventRulesNode> BuildControlNodes(IReadOnlyList<EventRulesLinkRow> rows)
    {
        var children = new List<JdeEventRulesNode>();
        foreach (var controlGroup in rows
            .GroupBy(row => row.ControlId)
            .OrderBy(group => group.Key))
        {
            string controlLabel = FormatControlLabel(controlGroup.Key);
            var controlNode = new JdeEventRulesNode
            {
                Id = $"control:{controlGroup.Key}",
                Name = controlLabel,
                NodeType = controlGroup.Key == 0 ? JdeEventRulesNodeType.Form : JdeEventRulesNodeType.Control,
                Children = BuildEventNodes(controlGroup.ToList())
            };
            children.Add(controlNode);
        }
        return children;
    }

    private static IReadOnlyList<JdeEventRulesNode> BuildEventNodes(IReadOnlyList<EventRulesLinkRow> rows)
    {
        var children = new List<JdeEventRulesNode>();
        foreach (var eventGroup in rows
            .GroupBy(row => new { row.EventSpecKey, row.EventId, row.EventOrder, row.Id3 })
            .OrderBy(group => group.Key.EventOrder)
            .ThenBy(group => group.Key.EventId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.Id3))
        {
            string label = FormatEventLabel(eventGroup.Key.EventId, eventGroup.Key.Id3);
            var eventNode = new JdeEventRulesNode
            {
                Id = eventGroup.Key.EventSpecKey,
                Name = label,
                NodeType = JdeEventRulesNodeType.Event,
                EventSpecKey = eventGroup.Key.EventSpecKey
            };
            children.Add(eventNode);
        }
        return children;
    }

    private static string NormalizeGroupKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string FormatVersionLabel(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? "Version: <default>" : $"Version: {key}";
    }

    private static string FormatFormLabel(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? "Form: <default>" : $"Form: {key}";
    }

    private static string FormatControlLabel(int controlId)
    {
        return controlId == 0 ? "Form Events" : $"Control {controlId}";
    }

    private static string FormatEventLabel(string eventId, int id3)
    {
        string label = string.IsNullOrWhiteSpace(eventId) ? "Event" : $"Event {eventId}";
        if (id3 != 0)
        {
            label += $" ({id3})";
        }
        return label;
    }

    private static int ParseEventOrder(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return 0;
        }
        string token = eventId.Trim();
        int dot = token.IndexOf('.');
        if (dot >= 0)
        {
            token = token.Substring(0, dot);
        }
        return int.TryParse(token, out int value) ? value : 0;
    }

    private HREQUEST OpenTable(string tableName, ID indexId)
    {
        string? resolved = DataSourceResolver.ResolveTableDataSource(_hUser, tableName);
        var table = new NID(tableName);
        int result = JDB_OpenTable(_hUser, table, indexId, IntPtr.Zero, 0, resolved, out var hRequest);
        if (result != JDEDB_PASSED && !string.IsNullOrWhiteSpace(resolved))
        {
            result = JDB_OpenTable(_hUser, table, indexId, IntPtr.Zero, 0, null, out hRequest);
        }
        if (result != JDEDB_PASSED)
        {
            throw new JdeConnectionException($"Failed to open {tableName} (result={result}).");
        }
        LogRequestDataSource(hRequest, tableName);
        return hRequest;
    }

    private void ProcessFetchedRecord(HREQUEST hRequest)
    {
        if (!_options.UseProcessFetchedRecord)
        {
            return;
        }
        int flags = RECORD_CONVERT | RECORD_PROCESS | RECORD_TRIGGERS;
        for (int i = 0; i < 3; i++)
        {
            JDB_ProcessFetchedRecord(hRequest, hRequest, flags);
        }
    }

    private static string ReadColumnString(HREQUEST hRequest, string tableName, string columnName, int maxLength)
    {
        var dbRef = new DBREF(tableName, columnName);
        IntPtr valuePtr = JDB_GetTableColValue(hRequest, dbRef);
        if (valuePtr == IntPtr.Zero)
        {
            return string.Empty;
        }
        string text = maxLength > 0
            ? Marshal.PtrToStringUni(valuePtr, maxLength) ?? string.Empty
            : Marshal.PtrToStringUni(valuePtr) ?? string.Empty;
        return NormalizeText(text);
    }

    private static string ReadColumnString(
        TableLayout? layout,
        IntPtr rowBuffer,
        HREQUEST hRequest,
        string tableName,
        string columnName,
        int maxLength)
    {
        if (layout != null && rowBuffer != IntPtr.Zero)
        {
            var value = layout.ReadValueByColumn(rowBuffer, columnName).Value;
            return NormalizeText(value switch
            {
                null => string.Empty,
                string text => text,
                _ => value.ToString() ?? string.Empty
            });
        }
        return ReadColumnString(hRequest, tableName, columnName, maxLength);
    }

    private static int ReadColumnInt32(HREQUEST hRequest, string tableName, string columnName)
    {
        var dbRef = new DBREF(tableName, columnName);
        IntPtr valuePtr = JDB_GetTableColValue(hRequest, dbRef);
        return valuePtr == IntPtr.Zero ? 0 : Marshal.ReadInt32(valuePtr);
    }

    private static int ReadColumnInt32(
        TableLayout? layout,
        IntPtr rowBuffer,
        HREQUEST hRequest,
        string tableName,
        string columnName)
    {
        if (layout != null && rowBuffer != IntPtr.Zero)
        {
            var value = layout.ReadValueByColumn(rowBuffer, columnName).Value;
            return value switch
            {
                null => 0,
                int intValue => intValue,
                short shortValue => shortValue,
                ushort ushortValue => ushortValue,
                uint uintValue => unchecked((int)uintValue),
                long longValue => unchecked((int)longValue),
                string text => int.TryParse(text, out int parsed) ? parsed : 0,
                _ => int.TryParse(value.ToString(), out int parsed) ? parsed : 0
            };
        }
        return ReadColumnInt32(hRequest, tableName, columnName);
    }

    private static string ReadColumnMathNumeric(HREQUEST hRequest, string tableName, string columnName)
    {
        var dbRef = new DBREF(tableName, columnName);
        IntPtr valuePtr = JDB_GetTableColValue(hRequest, dbRef);
        return valuePtr == IntPtr.Zero ? string.Empty : MathNumericParser.ToString(valuePtr);
    }

    private static string ReadColumnMathNumeric(
        TableLayout? layout,
        IntPtr rowBuffer,
        HREQUEST hRequest,
        string tableName,
        string columnName)
    {
        if (layout != null && rowBuffer != IntPtr.Zero)
        {
            var value = layout.ReadValueByColumn(rowBuffer, columnName).Value;
            return value switch
            {
                null => string.Empty,
                string text => text,
                _ => value.ToString() ?? string.Empty
            };
        }
        return ReadColumnMathNumeric(hRequest, tableName, columnName);
    }

    private static byte[] ReadBlob(HREQUEST hRequest, string tableName, string columnName)
    {
        var dbRef = new DBREF(tableName, columnName);
        IntPtr valuePtr = JDB_GetTableColValue(hRequest, dbRef);
        if (valuePtr == IntPtr.Zero)
        {
            return Array.Empty<byte>();
        }
        var blob = Marshal.PtrToStructure<JdeBlobValue>(valuePtr);
        if (blob.lpValue == IntPtr.Zero || blob.lSize == 0)
        {
            return Array.Empty<byte>();
        }
        int size = unchecked((int)blob.lSize);
        if (size <= 0)
        {
            return Array.Empty<byte>();
        }
        var data = new byte[size];
        Marshal.Copy(blob.lpValue, data, 0, size);
        return data;
    }

    private static void ApplyObjectNameSelection(HREQUEST hRequest, string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }
        IntPtr valuePtr = Marshal.StringToHGlobalUni(objectName);
        try
        {
            JDB_ClearSelection(hRequest);
            var select = new SELECTSTRUCT[1];
            select[0].Item1 = new DBREF(F98740Structures.TableName, F98740Structures.Columns.ObjectName, 0);
            select[0].Item2 = new DBREF(string.Empty, string.Empty, 0);
            select[0].lpValue = valuePtr;
            select[0].nValues = 1;
            select[0].nCmp = JDEDB_CMP_EQ;
            select[0].nAndOr = JDEDB_ANDOR_AND;
            int result = JDB_SetSelection(hRequest, select, 1, JDEDB_SET_REPLACE);
            if (result != JDEDB_PASSED)
            {
                ThrowSelectionError(hRequest, result);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(valuePtr);
        }
    }

    private static void ThrowSelectionError(HREQUEST hRequest, int result)
    {
        if (JDB_GetLastDBError(hRequest, out int errorNum) == JDEDB_PASSED)
        {
            throw new JdeApiException("JDB_SetSelection", $"Failed to apply event rules selection (error={errorNum})", result);
        }
        throw new JdeApiException("JDB_SetSelection", "Failed to apply event rules selection", result);
    }

    private void LogRequestDataSource(HREQUEST hRequest, string tableName)
    {
        if (!_options.EnableDebug)
        {
            return;
        }
        var buffer = new StringBuilder(128);
        int result = JDB_GetDSName(hRequest, buffer);
        if (result == JDEDB_PASSED)
        {
            _options.WriteLog($"[DEBUG] {tableName} resolved data source: {buffer}");
        }
        else
        {
            _options.WriteLog($"[DEBUG] {tableName} resolved data source: <unknown> (result={result})");
        }
    }

    private static string NormalizeText(string text)
    {
        return (text ?? string.Empty).TrimEnd('\0', ' ');
    }
    private static class GbrSpecParser
    {
        private const short GRT_EVENT = 1;
        private const short GRT_BF = 2;
        private const short GRT_WHILE = 3;
        private const short GRT_ENDWHILE = 4;
        private const short GRT_IF = 5;
        private const short GRT_ELSE = 6;
        private const short GRT_ENDIF = 7;
        private const short GRT_COMMENT = 9;
        private const short GRT_ASSIGN = 12;
        private const short GRT_SLBF = 14;
        private const short GRT_FI = 15;
        private const short GRT_OPTIONS = 16;
        private const short GRT_NER = 17;
        private const short GRT_VARIABLE = 18;
        private const short GRT_RI = 19;
        private const short GRT_FILEIO_OP = 20;
        private const short GRT_ELSEIF = 23;
        private const short GRT_NOP = 0x100;
        private const int EventSpecKeyChars = 37;
        private const int FormatSize = 4;
        private const int SequenceSize = 2;
        private const int RecordTypeSize = 2;
        private static readonly int[] SizeLengthCandidates = { 4, 8 };
        private static readonly int[] EventSpecKeyCharSizeCandidates = { 2, 1 };
        private readonly struct GbrHeader
        {
            public int SizeLengthBytes { get; init; }
            public int RecordLength { get; init; }
            public int EventSpecKeyOffset { get; init; }
            public int EventSpecKeyBytes { get; init; }
            public int CharSize { get; init; }
            public int SequenceOffset { get; init; }
            public int RecordTypeOffset { get; init; }
            public int UnionOffset { get; init; }
            public short RecordType { get; init; }
        }

        private static readonly int BfNameOffset = (int)Marshal.OffsetOf<GbrBfHeader>(nameof(GbrBfHeader.FunctionName));

        private static readonly int CommentTextOffset = (int)Marshal.OffsetOf<GbrCommentHeader>(nameof(GbrCommentHeader.Text));

        private static readonly int VariableNameOffset = (int)Marshal.OffsetOf<GbrVariableHeader>(nameof(GbrVariableHeader.Name));

        private static readonly int FiAppOffset = (int)Marshal.OffsetOf<GbrFiHeader>(nameof(GbrFiHeader.Application));

        private static readonly int FiFormOffset = (int)Marshal.OffsetOf<GbrFiHeader>(nameof(GbrFiHeader.Form));

        private static readonly int RiReportOffset = (int)Marshal.OffsetOf<GbrRiHeader>(nameof(GbrRiHeader.Report));

        private static readonly int FileIoOperationOffset = (int)Marshal.OffsetOf<GbrFileIoHeader>(nameof(GbrFileIoHeader.Operation));

        private static readonly int FileIoTypeOffset = (int)Marshal.OffsetOf<GbrFileIoHeader>(nameof(GbrFileIoHeader.FileType));

        private static readonly int FileIoNameOffset = (int)Marshal.OffsetOf<GbrFileIoHeader>(nameof(GbrFileIoHeader.FileName));
        private static readonly string[] FileIoLabels =
        {
            "OPEN",
            "CLOSE",
            "SELECT FROM",
            "FETCH_NEXT FROM",
            "INSERT INTO",
            "UPDATE",
            "DELETE FROM",
            "FETCH_SINGLE FROM",
            "SELECT ALL FROM"
        };

        public static bool LooksLikeGbrSpec(byte[] buffer)
        {
            if (TryGetHeader(buffer, null, out _))
            {
                return true;
            }
            return TryGetMultiHeader(buffer, null, out _, out _, out _);
        }

        public static IReadOnlyList<JdeEventRuleLine> Parse(byte[] buffer, int fallbackSequence, string? eventSpecKeyHint)
        {
            var lines = new List<JdeEventRuleLine>();
            if (TryParseMultiRecords(buffer, fallbackSequence, eventSpecKeyHint, lines))
            {
                return lines;
            }
            var line = ParseSingle(buffer, fallbackSequence, eventSpecKeyHint);
            if (line != null)
            {
                lines.Add(line);
            }
            return lines;
        }

        public static JdeEventRuleLine? ParseSingle(byte[] buffer, int fallbackSequence, string? eventSpecKeyHint)
        {
            if (!TryGetHeader(buffer, eventSpecKeyHint, out GbrHeader header))
            {
                return null;
            }
            int recordLength = header.RecordLength;
            if (recordLength <= 0 || recordLength > buffer.Length)
            {
                recordLength = buffer.Length;
            }
            short recordType = header.RecordType;
            short baseType = (short)(recordType & 0xFF);
            string text = ExtractLineText(baseType, buffer, 0, recordLength, header.UnionOffset, header.CharSize);
            ushort sequence = ReadUInt16(buffer, header.SequenceOffset);
            int sequenceValue = sequence == 0 ? fallbackSequence : sequence;
            return new JdeEventRuleLine
            {
                Sequence = sequenceValue,
                RecordType = baseType,
                Text = string.IsNullOrWhiteSpace(text) ? GetFallbackLabel(baseType) : text
            };
        }

        private static bool TryParseMultiRecords(byte[] buffer, int fallbackSequence, string? eventSpecKeyHint, List<JdeEventRuleLine> lines)
        {
            if (!TryGetMultiHeader(buffer, eventSpecKeyHint, out int recordCount, out int recordsOffset, out int sizeLength))
            {
                return false;
            }
            int cursor = recordsOffset;
            int added = 0;
            for (int i = 0; i < recordCount; i++)
            {
                if (cursor + sizeLength > buffer.Length)
                {
                    added = 0;
                    break;
                }
                long recordLength = ReadSize(buffer, cursor, sizeLength);
                if (recordLength <= 0 || recordLength > buffer.Length - cursor || recordLength > int.MaxValue)
                {
                    added = 0;
                    break;
                }
                int length = (int)recordLength;
                var record = new byte[length];
                Buffer.BlockCopy(buffer, cursor, record, 0, length);
                var line = ParseSingle(record, fallbackSequence, eventSpecKeyHint);
                if (line != null)
                {
                    lines.Add(line);
                    added++;
                }
                cursor += length;
            }
            return added > 0;
        }

        private static bool TryGetMultiHeader(byte[] buffer, string? eventSpecKeyHint, out int recordCount, out int recordsOffset, out int sizeLength)
        {
            if (!string.IsNullOrWhiteSpace(eventSpecKeyHint))
            {
                if (TryGetMultiHeaderFromHint(buffer, eventSpecKeyHint, out recordCount, out recordsOffset, out sizeLength))
                {
                    return true;
                }
            }
            foreach (int sizeLengthCandidate in SizeLengthCandidates)
            {
                foreach (int charSize in EventSpecKeyCharSizeCandidates)
                {
                    if (TryGetMultiHeader(buffer, sizeLengthCandidate, charSize, out recordCount, out recordsOffset))
                    {
                        sizeLength = sizeLengthCandidate;
                        return true;
                    }
                }
            }
            recordCount = 0;
            recordsOffset = 0;
            sizeLength = 0;
            return false;
        }

        private static bool TryGetMultiHeaderFromHint(byte[] buffer, string eventSpecKeyHint, out int recordCount, out int recordsOffset, out int sizeLength)
        {
            foreach (var match in FindEventSpecKeyOffsets(buffer, eventSpecKeyHint))
            {
                if (!IsSizeLengthCandidate(match.Offset))
                {
                    continue;
                }
                if (TryGetMultiHeader(buffer, match.Offset, match.CharSize, out recordCount, out recordsOffset))
                {
                    sizeLength = match.Offset;
                    return true;
                }
            }
            recordCount = 0;
            recordsOffset = 0;
            sizeLength = 0;
            return false;
        }

        private static bool TryGetMultiHeader(byte[] buffer, int sizeLength, int charSize, out int recordCount, out int recordsOffset)
        {
            recordCount = 0;
            recordsOffset = 0;
            int eventSpecKeyBytes = EventSpecKeyChars * charSize;
            int headerSize = sizeLength + eventSpecKeyBytes + 4;
            if (buffer.Length < headerSize)
            {
                return false;
            }
            long totalLength = ReadSize(buffer, 0, sizeLength);
            if (totalLength <= 0 || totalLength > buffer.Length)
            {
                return false;
            }
            string evsk = ReadFixedString(buffer, sizeLength, EventSpecKeyChars, charSize);
            if (!HasValidEventSpecKey(evsk))
            {
                return false;
            }
            recordCount = BitConverter.ToInt32(buffer, sizeLength + eventSpecKeyBytes);
            if (recordCount <= 0 || recordCount > 200000)
            {
                return false;
            }
            recordsOffset = headerSize;
            return true;
        }

        private static bool TryGetHeader(byte[] buffer, string? eventSpecKeyHint, out GbrHeader header)
        {
            if (!string.IsNullOrWhiteSpace(eventSpecKeyHint))
            {
                if (TryGetHeaderFromHint(buffer, eventSpecKeyHint, out header))
                {
                    return true;
                }
            }
            foreach (int sizeLength in SizeLengthCandidates)
            {
                foreach (int charSize in EventSpecKeyCharSizeCandidates)
                {
                    if (TryGetHeader(buffer, sizeLength, charSize, out header))
                    {
                        return true;
                    }
                }
            }
            header = default;
            return false;
        }

        private static bool TryGetHeaderFromHint(byte[] buffer, string eventSpecKeyHint, out GbrHeader header)
        {
            foreach (var match in FindEventSpecKeyOffsets(buffer, eventSpecKeyHint))
            {
                int sizeLength = match.Offset - FormatSize;
                if (!IsSizeLengthCandidate(sizeLength))
                {
                    continue;
                }
                if (TryGetHeader(buffer, sizeLength, match.CharSize, out header))
                {
                    return true;
                }
            }
            header = default;
            return false;
        }

        private static bool TryGetHeader(byte[] buffer, int sizeLength, int charSize, out GbrHeader header)
        {
            header = default;
            int eventSpecKeyBytes = EventSpecKeyChars * charSize;
            int headerSize = sizeLength + FormatSize + eventSpecKeyBytes + SequenceSize + RecordTypeSize;
            if (buffer.Length < headerSize)
            {
                return false;
            }
            long recordLength = ReadSize(buffer, 0, sizeLength);
            if (recordLength <= 0 || recordLength > buffer.Length)
            {
                return false;
            }
            int eventSpecKeyOffset = sizeLength + FormatSize;
            int sequenceOffset = eventSpecKeyOffset + eventSpecKeyBytes;
            int recordTypeOffset = sequenceOffset + SequenceSize;
            int unionOffset = recordTypeOffset + RecordTypeSize;
            short recordType = ReadInt16(buffer, recordTypeOffset);
            short baseType = (short)(recordType & 0xFF);
            if (baseType <= 0 || baseType > 40)
            {
                return false;
            }
            string evsk = ReadFixedString(buffer, eventSpecKeyOffset, EventSpecKeyChars, charSize);
            if (!HasValidEventSpecKey(evsk))
            {
                return false;
            }
            header = new GbrHeader
            {
                SizeLengthBytes = sizeLength,
                RecordLength = (int)recordLength,
                EventSpecKeyOffset = eventSpecKeyOffset,
                EventSpecKeyBytes = eventSpecKeyBytes,
                CharSize = charSize,
                SequenceOffset = sequenceOffset,
                RecordTypeOffset = recordTypeOffset,
                UnionOffset = unionOffset,
                RecordType = recordType
            };
            return true;
        }

        private static bool IsSizeLengthCandidate(int sizeLength)
        {
            foreach (int candidate in SizeLengthCandidates)
            {
                if (candidate == sizeLength)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasValidEventSpecKey(string evsk)
        {
            return !string.IsNullOrWhiteSpace(evsk) && evsk.Trim().Contains('-');
        }

        private readonly record struct EventSpecKeyMatch(int Offset, int CharSize);

        private static IEnumerable<EventSpecKeyMatch> FindEventSpecKeyOffsets(byte[] buffer, string eventSpecKey)
        {
            if (string.IsNullOrWhiteSpace(eventSpecKey))
            {
                yield break;
            }
            var unicode = Encoding.Unicode.GetBytes(eventSpecKey);
            int unicodeOffset = FindPattern(buffer, unicode);
            if (unicodeOffset >= 0)
            {
                yield return new EventSpecKeyMatch(unicodeOffset, 2);
            }
            var ascii = Encoding.ASCII.GetBytes(eventSpecKey);
            int asciiOffset = FindPattern(buffer, ascii);
            if (asciiOffset >= 0)
            {
                yield return new EventSpecKeyMatch(asciiOffset, 1);
            }
        }

        private static int FindPattern(byte[] buffer, byte[] pattern)
        {
            if (pattern.Length == 0 || pattern.Length > buffer.Length)
            {
                return -1;
            }
            for (int i = 0; i <= buffer.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (buffer[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return i;
                }
            }
            return -1;
        }

        private static string ExtractLineText(short recordType, byte[] buffer, int recordOffset, int recordLength, int unionOffset, int charSize)
        {
            int unionStart = recordOffset + unionOffset;
            if (unionStart >= buffer.Length)
            {
                return string.Empty;
            }
            if (charSize == 1)
            {
                return ExtractTrailingText(buffer, recordOffset, recordLength, charSize);
            }
            switch (recordType)
            {
                case GRT_BF:
                case GRT_NER:
                    return ReadFixedString(buffer, unionStart + BfNameOffset, 33, charSize);
                case GRT_COMMENT:
                    return ReadFixedString(buffer, unionStart + CommentTextOffset, 81, charSize);
                case GRT_VARIABLE:
                    return $"VARIABLE - {ReadFixedString(buffer, unionStart + VariableNameOffset, 31, charSize)}".TrimEnd();
                case GRT_FI:
                {
                    string app = ReadFixedString(buffer, unionStart + FiAppOffset, 11, charSize);
                    string form = ReadFixedString(buffer, unionStart + FiFormOffset, 11, charSize);
                    if (!string.IsNullOrWhiteSpace(app) || !string.IsNullOrWhiteSpace(form))
                    {
                        return $"FI : CALL(Application: {app} Form: {form})".TrimEnd();
                    }
                    break;
                }
                case GRT_RI:
                {
                    string report = ReadFixedString(buffer, unionStart + RiReportOffset, 11, charSize);
                    if (!string.IsNullOrWhiteSpace(report))
                    {
                        return $"RI : CALL(UBE: {report})".TrimEnd();
                    }
                    break;
                }
                case GRT_FILEIO_OP:
                {
                    ushort op = ReadUInt16(buffer, unionStart + FileIoOperationOffset);
                    short type = ReadInt16(buffer, unionStart + FileIoTypeOffset);
                    string name = ReadFixedString(buffer, unionStart + FileIoNameOffset, 11, charSize);
                    string label = op > 0 && op <= FileIoLabels.Length ? FileIoLabels[op - 1] : "FILEIO";
                    string kind = type == 0 ? "Table" : "View";
                    return $"{label} {kind} {name}".TrimEnd();
                }
                default:
                    break;
            }
            return ExtractTrailingText(buffer, recordOffset, recordLength, charSize);
        }

                private static string ExtractTrailingText(byte[] buffer, int recordOffset, int recordLength, int charSize)
        {
            int start = recordOffset;
            int end = Math.Min(recordOffset + recordLength, buffer.Length);
            if (charSize == 1)
            {
                while (end - 1 >= start && buffer[end - 1] == 0)
                {
                    end -= 1;
                }
                if (end <= start)
                {
                    return string.Empty;
                }
                int cursor = end;
                while (cursor - 1 >= start)
                {
                    if (buffer[cursor - 1] == 0)
                    {
                        break;
                    }
                    cursor -= 1;
                }
                int length = end - cursor;
                if (length <= 0)
                {
                    return string.Empty;
                }
                return ReadFixedString(buffer, cursor, length, charSize);
            }
            end = AlignDown(end);
            while (end - 2 >= start && buffer[end - 2] == 0 && buffer[end - 1] == 0)
            {
                end -= 2;
            }
            if (end <= start)
            {
                return string.Empty;
            }
            int cursorWide = end;
            while (cursorWide - 2 >= start)
            {
                if (buffer[cursorWide - 2] == 0 && buffer[cursorWide - 1] == 0)
                {
                    break;
                }
                cursorWide -= 2;
            }
            int lengthWide = end - cursorWide;
            if (lengthWide <= 0)
            {
                return string.Empty;
            }
            return ReadFixedString(buffer, cursorWide, lengthWide / 2, charSize);
        }

        private static string GetFallbackLabel(short recordType)
        {
            return recordType switch
            {
                GRT_EVENT => "EVENT",
                GRT_BF => "BUSINESS FUNCTION",
                GRT_NER => "NAMED EVENT RULE",
                GRT_IF => "IF",
                GRT_ELSEIF => "ELSEIF",
                GRT_ELSE => "ELSE",
                GRT_ENDIF => "ENDIF",
                GRT_WHILE => "WHILE",
                GRT_ENDWHILE => "ENDWHILE",
                GRT_ASSIGN => "ASSIGN",
                GRT_SLBF => "SLBF",
                GRT_FI => "FI",
                GRT_RI => "RI",
                GRT_FILEIO_OP => "FILEIO",
                GRT_OPTIONS => "OPTIONS",
                _ => $"TYPE {recordType}"
            };
        }

        private static string ReadFixedString(byte[] buffer, int offset, int charCount, int charSize = 2)
        {
            if (offset < 0 || offset >= buffer.Length || charCount <= 0)
            {
                return string.Empty;
            }
            if (charSize <= 1)
            {
                if (offset + charCount > buffer.Length)
                {
                    charCount = buffer.Length - offset;
                }
                if (charCount <= 0)
                {
                    return string.Empty;
                }
                string asciiText = Encoding.ASCII.GetString(buffer, offset, charCount);
                return asciiText.TrimEnd('\0', ' ');
            }
            int byteCount = charCount * 2;
            if (offset + byteCount > buffer.Length)
            {
                byteCount = buffer.Length - offset;
            }
            string textValue = Encoding.Unicode.GetString(buffer, offset, byteCount);
            return textValue.TrimEnd('\0', ' ');
        }

        private static int AlignDown(int value)
        {
            return (value & ~1);
        }

        private static short ReadInt16(byte[] buffer, int offset)
        {
            if (offset + 2 > buffer.Length)
            {
                return 0;
            }
            return BitConverter.ToInt16(buffer, offset);
        }

        private static ushort ReadUInt16(byte[] buffer, int offset)
        {
            if (offset + 2 > buffer.Length)
            {
                return 0;
            }
            return BitConverter.ToUInt16(buffer, offset);
        }

        private static int ReadInt32(byte[] buffer, int offset)
        {
            if (offset + 4 > buffer.Length)
            {
                return 0;
            }
            return BitConverter.ToInt32(buffer, offset);
        }

        private static long ReadSize(byte[] buffer, int offset, int sizeLength)
        {
            if (sizeLength == 8)
            {
                if (offset + 8 > buffer.Length)
                {
                    return 0;
                }
                return BitConverter.ToInt64(buffer, offset);
            }
            if (offset + 4 > buffer.Length)
            {
                return 0;
            }
            return BitConverter.ToInt32(buffer, offset);
        }

        private static int ReadRecordLength(byte[] buffer)
        {
            return ReadInt32(buffer, 0);
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct GbrSpecHeader
        {
            public uint Length;
            public int Format;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 37)]
            public string EventSpecKey;
            public ushort Sequence;
            public short RecordType;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct GbrBfHeader
        {
            public ushort ParamCount;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
            public string FunctionName;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct GbrCommentHeader
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 81)]
            public string Text;
            public short CommentType;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct GbrVariableHeader
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 31)]
            public string Name;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct GbrFiHeader
        {
            public ushort ParamCount;
            public IntPtr VersionPointer;
            public NID Application;
            public IntPtr DescPointer;
            public NID TemplateName;
            public NID Form;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct GbrRiHeader
        {
            public int ToSection;
            public ushort ParamCount;
            public IntPtr VersionPointer;
            public NID Report;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct GbrFileIoHeader
        {
            public ushort Operation;
            public short FileType;
            public NID FileName;
        }
    }

    private sealed record EventRulesLinkRow(
        string Version,
        string FormName,
        int ControlId,
        string EventId,
        int EventOrder,
        int Id3,
        string EventSpecKey
    );
}
