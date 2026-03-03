using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using static JdeClient.Core.Interop.JdeSpecEncapApi;
using static JdeClient.Core.Interop.JdeKernelApi;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Loads business view metadata from JDE spec structures.
/// </summary>
internal sealed class SpecBusinessViewMetadataService : IDisposable
{
    private const int SpecKeyBusinessViewByName = 1;
    private readonly HUSER _hUser;
    private readonly JdeClientOptions _options;
    private bool _disposed;
    private bool DebugEnabled => _options.EnableSpecDebug;

    public SpecBusinessViewMetadataService(HUSER hUser, JdeClientOptions options)
    {
        _hUser = hUser;
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Retrieve business view metadata or null when the view is not found.
    /// </summary>
    public JdeBusinessViewInfo? GetBusinessViewInfo(
        string viewName,
        string? specDataSourceOverride = null,
        bool allowSpecDataSourceFallback = true)
    {
        if (string.IsNullOrWhiteSpace(viewName))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(specDataSourceOverride))
        {
            JdeBusinessViewInfo? overrideInfo = TryGetBusinessViewInfoFromSpecOverride(viewName, specDataSourceOverride);
            if (overrideInfo != null)
            {
                return overrideInfo;
            }

            if (DebugEnabled)
            {
                _options.WriteLog(
                    $"[DEBUG] Business view override '{specDataSourceOverride}' unavailable for {viewName}; no runtime-default fallback is allowed when an explicit source is provided.");
            }

            return null;
        }

        int GetSpecs(NID nid, out IntPtr ptr) => JDBRS_GetBOBSpecs(_hUser, nid, out ptr, (char)1, IntPtr.Zero);

        return GetBusinessViewInfoCore(
            viewName,
            GetSpecs,
            ptr => JDBRS_FreeBOBSpecs(ptr),
            DebugEnabled ? _options.WriteLog : null);
    }

    private JdeBusinessViewInfo? TryGetBusinessViewInfoFromSpecOverride(string viewName, string specDataSourceOverride)
    {
        string source = specDataSourceOverride.Trim();
        if (!TryOpenBusinessViewSpecHandle(source, out IntPtr hSpec))
        {
            return null;
        }

        try
        {
            JdeBusinessViewInfo? info = TryReadBusinessViewInfoFromSpecHandle(hSpec, viewName);
            if (info != null)
            {
                if (DebugEnabled)
                {
                    _options.WriteLog($"[DEBUG] Loaded business view specs for {viewName} from '{source}'.");
                }
                return info;
            }
        }
        finally
        {
            jdeSpecClose(hSpec);
        }

        return null;
    }

    private bool TryOpenBusinessViewSpecHandle(string source, out IntPtr hSpec)
    {
        hSpec = IntPtr.Zero;

        string? pathCode = ExtractPathCodeCandidate(source);
        if (!string.IsNullOrWhiteSpace(pathCode))
        {
            int centralIndexedResult = jdeSpecOpenCentralIndexed(
                out hSpec,
                _hUser,
                JdeSpecFileType.BusView,
                new ID(SpecKeyBusinessViewByName),
                pathCode);
            if (DebugEnabled)
            {
                _options.WriteLog(
                    $"[DEBUG] jdeSpecOpenCentralIndexed BusView key=1 path='{pathCode}' result={centralIndexedResult}, handle=0x{hSpec.ToInt64():X}");
            }
            if (centralIndexedResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero)
            {
                return true;
            }

            int centralOpenResult = jdeSpecOpenCentral(
                out hSpec,
                _hUser,
                JdeSpecFileType.BusView,
                pathCode);
            if (DebugEnabled)
            {
                _options.WriteLog(
                    $"[DEBUG] jdeSpecOpenCentral BusView path='{pathCode}' result={centralOpenResult}, handle=0x{hSpec.ToInt64():X}");
            }
            if (centralOpenResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero)
            {
                return true;
            }
        }

        return TryOpenBusinessViewSpecHandleAtLocation(source, JdeSpecLocation.CentralObjects, out hSpec);
    }

    private bool TryOpenBusinessViewSpecHandleAtLocation(string source, JdeSpecLocation location, out IntPtr hSpec)
    {
        hSpec = IntPtr.Zero;
        int indexedResult = jdeSpecOpenIndexed(
            out hSpec,
            _hUser,
            JdeSpecFileType.BusView,
            location,
            new ID(SpecKeyBusinessViewByName),
            source);
        if (DebugEnabled)
        {
            _options.WriteLog(
                $"[DEBUG] jdeSpecOpenIndexed BusView {location} key=1 source='{source}' result={indexedResult}, handle=0x{hSpec.ToInt64():X}");
        }
        if (indexedResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero)
        {
            return true;
        }

        int openResult = jdeSpecOpen(
            out hSpec,
            _hUser,
            JdeSpecFileType.BusView,
            location,
            source);
        if (DebugEnabled)
        {
            _options.WriteLog(
                $"[DEBUG] jdeSpecOpen BusView {location} source='{source}' result={openResult}, handle=0x{hSpec.ToInt64():X}");
        }

        return openResult == JDESPEC_SUCCESS && hSpec != IntPtr.Zero;
    }

    private JdeBusinessViewInfo? TryReadBusinessViewInfoFromSpecHandle(IntPtr hSpec, string viewName)
    {
        IntPtr hConvert = IntPtr.Zero;
        IntPtr keyPtr = IntPtr.Zero;
        var specData = new JdeSpecData();
        try
        {
            int convertInit = jdeSpecInitXMLConvertHandle(out hConvert, JdeSpecFileType.BusView);
            if (convertInit != JDESPEC_SUCCESS || hConvert == IntPtr.Zero)
            {
                if (DebugEnabled)
                {
                    _options.WriteLog($"[DEBUG] jdeSpecInitXMLConvertHandle(BusView) failed: {convertInit}");
                }
                return null;
            }

            var key = new JdeSpecKeyBusView
            {
                ViewName = new NID(viewName)
            };
            int keySize = Marshal.SizeOf<JdeSpecKeyBusView>();
            keyPtr = Marshal.AllocHGlobal(keySize);
            Marshal.StructureToPtr(key, keyPtr, false);

            int fetchSingleResult = jdeSpecFetchSingle(hSpec, ref specData, keyPtr, 1);
            if (DebugEnabled)
            {
                _options.WriteLog($"[DEBUG] jdeSpecFetchSingle(BusView:{viewName}) result={fetchSingleResult}");
            }

            if (fetchSingleResult != JDESPEC_SUCCESS || specData.SpecData == IntPtr.Zero)
            {
                if (specData.SpecData != IntPtr.Zero)
                {
                    jdeSpecFreeData(ref specData);
                    specData = default;
                }

                int selectResult = jdeSpecSelectKeyed(hSpec, keyPtr, keySize, 1);
                if (DebugEnabled)
                {
                    _options.WriteLog($"[DEBUG] jdeSpecSelectKeyed(BusView:{viewName}) result={selectResult}");
                }
                if (selectResult != JDESPEC_SUCCESS)
                {
                    return null;
                }

                int fetchResult = jdeSpecFetch(hSpec, ref specData);
                if (DebugEnabled)
                {
                    _options.WriteLog($"[DEBUG] jdeSpecFetch(BusView:{viewName}) result={fetchResult}");
                }
                if (fetchResult != JDESPEC_SUCCESS || specData.SpecData == IntPtr.Zero)
                {
                    return TryReadBusinessViewInfoByScan(hSpec, hConvert, viewName);
                }
            }

            JdeBusinessViewInfo? packedInfo = TryParsePackedBusinessViewSpec(
                specData.SpecData,
                specData.DataLen,
                viewName);
            if (packedInfo != null)
            {
                if (DebugEnabled)
                {
                    _options.WriteLog($"[DEBUG] Parsed BusView packed BOB data for {viewName}.");
                }
                return packedInfo;
            }

            string? xml = TryConvertSpecDataToXml(hConvert, ref specData);
            if (string.IsNullOrWhiteSpace(xml))
            {
                return TryReadBusinessViewInfoByScan(hSpec, hConvert, viewName);
            }

            JdeBusinessViewInfo? parsedFromXml = ParseBusinessViewInfoFromXml(xml, viewName);
            if (parsedFromXml != null)
            {
                return parsedFromXml;
            }

            return TryReadBusinessViewInfoByScan(hSpec, hConvert, viewName);
        }
        finally
        {
            if (specData.SpecData != IntPtr.Zero)
            {
                jdeSpecFreeData(ref specData);
            }

            if (keyPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(keyPtr);
            }

            if (hConvert != IntPtr.Zero)
            {
                jdeSpecClose(hConvert);
            }
        }
    }

    private JdeBusinessViewInfo? TryReadBusinessViewInfoByScan(
        IntPtr hSpec,
        IntPtr hConvert,
        string viewName)
    {
        for (int i = 0; i < 10000; i++)
        {
            var candidate = new JdeSpecData();
            try
            {
                int fetchResult = jdeSpecFetch(hSpec, ref candidate);
                if (DebugEnabled && i < 5)
                {
                    _options.WriteLog($"[DEBUG] jdeSpecFetch(BusView scan:{viewName}) result={fetchResult}");
                }

                if (fetchResult != JDESPEC_SUCCESS || candidate.SpecData == IntPtr.Zero)
                {
                    break;
                }

                JdeBusinessViewInfo? packed = TryParsePackedBusinessViewSpec(candidate.SpecData, candidate.DataLen, viewName);
                if (packed != null &&
                    string.Equals(packed.ViewName, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    return packed;
                }

                string? xml = TryConvertSpecDataToXml(hConvert, ref candidate);
                if (string.IsNullOrWhiteSpace(xml))
                {
                    continue;
                }

                JdeBusinessViewInfo? parsed = ParseBusinessViewInfoFromXml(xml, viewName);
                if (parsed != null &&
                    string.Equals(parsed.ViewName, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    return parsed;
                }
            }
            finally
            {
                if (candidate.SpecData != IntPtr.Zero)
                {
                    jdeSpecFreeData(ref candidate);
                }
            }
        }

        return null;
    }

    internal static JdeBusinessViewInfo? TryParsePackedBusinessViewSpec(
        IntPtr specDataPtr,
        uint dataLength,
        string fallbackViewName)
    {
        if (specDataPtr == IntPtr.Zero)
        {
            return null;
        }

        int headerSize = Marshal.SizeOf<BOB_HEADER>();
        if (dataLength > 0 && dataLength < headerSize)
        {
            return null;
        }

        BOB_HEADER header;
        try
        {
            header = Marshal.PtrToStructure<BOB_HEADER>(specDataPtr);
        }
        catch
        {
            return null;
        }

        int tableCount = header.nTableCount;
        int primaryKeyCount = header.nPrimaryKeyColumnCount;
        int columnCount = header.nColumnCount;
        int joinCount = header.nJoinCount;
        if (tableCount > 4096 || primaryKeyCount > 16384 || columnCount > 16384 || joinCount > 16384)
        {
            return null;
        }

        long computedSize;
        try
        {
            computedSize = checked(
                (long)headerSize +
                (long)tableCount * Marshal.SizeOf<BOB_TABLE>() +
                (long)primaryKeyCount * Marshal.SizeOf<DBREF>() +
                (long)columnCount * Marshal.SizeOf<BOB_COLUMN>() +
                (long)joinCount * Marshal.SizeOf<BOB_JOIN>());
        }
        catch (OverflowException)
        {
            return null;
        }

        uint declaredLength = header.lVarLen.Value;
        long available = dataLength > 0
            ? dataLength
            : declaredLength;
        if (available > 0 && computedSize > available)
        {
            return null;
        }

        int offset = headerSize;
        IntPtr tablesPtr = tableCount > 0 ? IntPtr.Add(specDataPtr, offset) : IntPtr.Zero;
        offset += tableCount * Marshal.SizeOf<BOB_TABLE>();
        offset += primaryKeyCount * Marshal.SizeOf<DBREF>();
        IntPtr columnsPtr = columnCount > 0 ? IntPtr.Add(specDataPtr, offset) : IntPtr.Zero;
        offset += columnCount * Marshal.SizeOf<BOB_COLUMN>();
        IntPtr joinsPtr = joinCount > 0 ? IntPtr.Add(specDataPtr, offset) : IntPtr.Zero;

        var info = new JdeBusinessViewInfo
        {
            ViewName = NormalizeNid(header.szView, fallbackViewName),
            Description = NormalizeText(header.szDescription),
            SystemCode = NormalizeText(header.szSystemCode)
        };

        ReadTables(tablesPtr, header.nTableCount, info.Tables);
        ReadColumns(columnsPtr, header.nColumnCount, info.Columns);
        ReadJoins(joinsPtr, header.nJoinCount, info.Joins);
        return info;
    }

    /// <summary>
    /// Core extraction logic for business view metadata, testable with mock delegates.
    /// </summary>
    internal static JdeBusinessViewInfo? GetBusinessViewInfoCore(
        string viewName,
        GetBobSpecsDelegate getBobSpecs,
        Action<IntPtr> freeBobSpecs,
        Action<string>? log)
    {
        IntPtr bobPtr = IntPtr.Zero;
        try
        {
            int result = getBobSpecs(new NID(viewName), out bobPtr);
            log?.Invoke($"[DEBUG] JDBRS_GetBOBSpecs({viewName}) result={result}, bobPtr=0x{bobPtr.ToInt64():X}");

            if (result != JDEDB_PASSED || bobPtr == IntPtr.Zero)
            {
                return null;
            }

            var bob = Marshal.PtrToStructure<BOB>(bobPtr);
            if (bob.lpHeader == IntPtr.Zero)
            {
                return null;
            }

            var header = Marshal.PtrToStructure<BOB_HEADER>(bob.lpHeader);
            var info = new JdeBusinessViewInfo
            {
                ViewName = NormalizeNid(header.szView, viewName),
                Description = NormalizeText(header.szDescription),
                SystemCode = NormalizeText(header.szSystemCode)
            };

            // Decode table, column, and join lists from the native BOB buffers.
            ReadTables(bob.lpTables, header.nTableCount, info.Tables);
            ReadColumns(bob.lpColumns, header.nColumnCount, info.Columns);
            ReadJoins(bob.lpJoins, header.nJoinCount, info.Joins);

            return info;
        }
        finally
        {
            if (bobPtr != IntPtr.Zero)
            {
                freeBobSpecs(bobPtr);
            }
        }
    }

    internal delegate int GetBobSpecsDelegate(NID viewName, out IntPtr bobPtr);

    internal static void ReadTables(IntPtr tablesPtr, ushort tableCount, List<JdeBusinessViewTable> tables)
    {
        tables.Clear();
        if (tablesPtr == IntPtr.Zero || tableCount == 0)
        {
            return;
        }

        int tableSize = Marshal.SizeOf<BOB_TABLE>();
        for (int i = 0; i < tableCount; i++)
        {
            var table = Marshal.PtrToStructure<BOB_TABLE>(IntPtr.Add(tablesPtr, i * tableSize));
            string name = NormalizeNid(table.szTable, string.Empty);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            tables.Add(new JdeBusinessViewTable
            {
                TableName = name,
                InstanceCount = table.nNumInstances,
                PrimaryIndexId = table.idPrimaryIndex.Value
            });
        }
    }

    internal static void ReadColumns(IntPtr columnsPtr, ushort columnCount, List<JdeBusinessViewColumn> columns)
    {
        columns.Clear();
        if (columnsPtr == IntPtr.Zero || columnCount == 0)
        {
            return;
        }

        int columnSize = Marshal.SizeOf<BOB_COLUMN>();
        for (int i = 0; i < columnCount; i++)
        {
            var column = Marshal.PtrToStructure<BOB_COLUMN>(IntPtr.Add(columnsPtr, i * columnSize));
            string dataItem = NormalizeNid(column.szDict, string.Empty);
            if (string.IsNullOrWhiteSpace(dataItem))
            {
                continue;
            }

            columns.Add(new JdeBusinessViewColumn
            {
                Sequence = column.nSeq,
                DataItem = dataItem,
                TableName = NormalizeNid(column.szTable, string.Empty),
                InstanceId = column.idInstance.Value,
                DataType = column.idEvType.Value,
                Length = column.idLength.Value,
                Decimals = column.nDecimals,
                DisplayDecimals = column.nDispDecimals,
                TypeCode = column.cType,
                ClassCode = column.cClass
            });
        }
    }

    internal static void ReadJoins(IntPtr joinsPtr, ushort joinCount, List<JdeBusinessViewJoin> joins)
    {
        joins.Clear();
        if (joinsPtr == IntPtr.Zero || joinCount == 0)
        {
            return;
        }

        int joinSize = Marshal.SizeOf<BOB_JOIN>();
        for (int i = 0; i < joinCount; i++)
        {
            var join = Marshal.PtrToStructure<BOB_JOIN>(IntPtr.Add(joinsPtr, i * joinSize));
            joins.Add(new JdeBusinessViewJoin
            {
                ForeignTable = NormalizeNid(join.szFTable, string.Empty),
                ForeignColumn = NormalizeNid(join.szFDict, string.Empty),
                ForeignInstanceId = join.idFInstance.Value,
                PrimaryTable = NormalizeNid(join.szPTable, string.Empty),
                PrimaryColumn = NormalizeNid(join.szPDict, string.Empty),
                PrimaryInstanceId = join.idPInstance.Value,
                JoinOperator = FormatJoinOperator(join.chOperator),
                JoinType = FormatJoinType(join.chType)
            });
        }
    }

    internal static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        int nullIndex = value.IndexOf('\0');
        if (nullIndex >= 0)
        {
            value = value.Substring(0, nullIndex);
        }

        return value.Trim();
    }

    internal static string NormalizeNid(NID nid, string fallback)
    {
        string value = NormalizeText(nid.Value);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    internal static string FormatJoinOperator(byte op)
    {
        return op switch
        {
            0 => "=",
            1 => "<",
            2 => ">",
            3 => "<=",
            4 => ">=",
            5 => "!=",
            _ => $"op {op}"
        };
    }

    internal static string FormatJoinType(byte type)
    {
        return type switch
        {
            0 => "Inner",
            1 => "Left Outer",
            2 => "Right Outer",
            3 => "Outer",
            4 => "Left Outer (SQL92)",
            _ => $"Type {type}"
        };
    }

    internal static JdeBusinessViewInfo? ParseBusinessViewInfoFromXml(string xml, string fallbackViewName)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException)
        {
            return null;
        }

        XElement? root = document.Root;
        if (root == null)
        {
            return null;
        }

        string viewName = FirstNonBlank(
            FindValue(root, "szView", "viewName", "ViewName", "name"),
            fallbackViewName);
        var info = new JdeBusinessViewInfo
        {
            ViewName = viewName,
            Description = FindValue(root, "szDescription", "description", "Description"),
            SystemCode = FindValue(root, "szSystemCode", "systemCode", "SystemCode")
        };

        var tableKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var columnKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var joinKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in root.Descendants())
        {
            if (TryParseJoin(element, out var join))
            {
                string joinKey = $"{join.ForeignTable}|{join.ForeignColumn}|{join.ForeignInstanceId}|{join.PrimaryTable}|{join.PrimaryColumn}|{join.PrimaryInstanceId}";
                if (joinKeys.Add(joinKey))
                {
                    info.Joins.Add(join);
                }
                continue;
            }

            if (TryParseColumn(element, out var column))
            {
                string columnKey = $"{column.TableName}|{column.DataItem}|{column.InstanceId}|{column.Sequence}";
                if (columnKeys.Add(columnKey))
                {
                    info.Columns.Add(column);
                }
                continue;
            }

            if (TryParseTable(element, out var table))
            {
                string tableKey = $"{table.TableName}|{table.InstanceCount}|{table.PrimaryIndexId}";
                if (tableKeys.Add(tableKey))
                {
                    info.Tables.Add(table);
                }
            }
        }

        return info;
    }

    internal static List<string> BuildSpecSourceCandidates(string source)
    {
        var candidates = new List<string>();
        if (string.IsNullOrWhiteSpace(source))
        {
            return candidates;
        }

        string trimmed = source.Trim();
        AddDistinct(candidates, trimmed);

        string? extracted = ExtractPathCodeCandidate(trimmed);
        if (!string.IsNullOrWhiteSpace(extracted))
        {
            AddDistinct(candidates, extracted);
        }

        return candidates;
    }

    internal static IEnumerable<string> BuildSpecHandleOpenCandidates(string source)
    {
        var candidates = new List<string>();
        if (string.IsNullOrWhiteSpace(source))
        {
            return candidates;
        }

        string trimmed = source.Trim();
        AddDistinct(candidates, trimmed);

        string? pathCode = ExtractPathCodeCandidate(trimmed);
        if (!string.IsNullOrWhiteSpace(pathCode))
        {
            AddDistinct(candidates, pathCode);
            AddDistinct(candidates, $"Central Objects - {pathCode}");
            AddDistinct(candidates, $"Object Librarian - {pathCode}");
        }

        return candidates;
    }

    internal static string? ExtractPathCodeCandidate(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        string trimmed = source.Trim();
        const string objectLibrarianPrefix = "Object Librarian - ";
        const string centralObjectsPrefix = "Central Objects - ";
        if (trimmed.StartsWith(objectLibrarianPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string token = trimmed.Substring(objectLibrarianPrefix.Length).Trim();
            return LooksLikePathCode(token) ? token : null;
        }

        if (trimmed.StartsWith(centralObjectsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string token = trimmed.Substring(centralObjectsPrefix.Length).Trim();
            return LooksLikePathCode(token) ? token : null;
        }

        if (LooksLikePathCode(trimmed))
        {
            return trimmed;
        }

        int separatorIndex = trimmed.LastIndexOf(" - ", StringComparison.Ordinal);
        if (separatorIndex >= 0 && separatorIndex + 3 < trimmed.Length)
        {
            string suffix = trimmed.Substring(separatorIndex + 3).Trim();
            if (LooksLikePathCode(suffix))
            {
                return suffix;
            }
        }

        return null;
    }

    private static void AddDistinct(List<string> values, string candidate)
    {
        if (values.Any(value => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        values.Add(candidate);
    }

    private static bool LooksLikePathCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        bool hasLetter = false;
        bool hasDigit = false;
        foreach (char ch in value.Trim())
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

        string token = value.Trim();
        bool isLocal = string.Equals(token, "LOCAL", StringComparison.OrdinalIgnoreCase);
        return hasLetter && (hasDigit || isLocal);
    }

    private string? TryConvertSpecDataToXml(IntPtr hConvert, ref JdeSpecData specData)
    {
        string? direct = TryConvertSpecDataToXmlDirect(hConvert, ref specData);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        int insertResult = jdeSpecInsertRecordToConsolidatedBuffer(hConvert, ref specData);
        if (insertResult != JDESPEC_SUCCESS)
        {
            if (DebugEnabled)
            {
                _options.WriteLog($"[DEBUG] jdeSpecInsertRecordToConsolidatedBuffer failed: {insertResult} ({GetSpecResultText(insertResult)})");
            }
            return null;
        }

        var xmlData = new JdeSpecData();
        try
        {
            int convertResult = jdeSpecConvertConsolidatedToXML(hConvert, ref xmlData);
            if (convertResult != JDESPEC_SUCCESS || xmlData.SpecData == IntPtr.Zero)
            {
                LogSpecConvertFailure(hConvert, convertResult, specData.DataType);
                return null;
            }

            return ReadUtf8Xml(xmlData);
        }
        finally
        {
            if (xmlData.SpecData != IntPtr.Zero)
            {
                jdeSpecFreeData(ref xmlData);
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
            int convertResult = jdeSpecConvertToXML_UTF16(hConvert, ref specData, ref xmlData);
            if (convertResult != JDESPEC_SUCCESS || xmlData.SpecData == IntPtr.Zero)
            {
                LogSpecConvertFailure(hConvert, convertResult, specData.DataType);
                return null;
            }

            return ReadUnicodeXml(xmlData);
        }
        finally
        {
            if (xmlData.SpecData != IntPtr.Zero)
            {
                jdeSpecFreeData(ref xmlData);
            }
        }
    }

    private void LogSpecConvertFailure(IntPtr hConvert, int result, JdeSpecDataType attemptType)
    {
        if (!DebugEnabled)
        {
            return;
        }

        string resultText = GetSpecResultText(result);
        var lastError = new JdeSpecLastError();
        int lastResult = jdeSpecGetLastErrorInfo(hConvert, ref lastError);
        if (lastResult == JDESPEC_SUCCESS)
        {
            _options.WriteLog(
                $"[DEBUG] BusView XML convert failed: result={result} ({resultText}), type={attemptType}, last={lastError.Result}, db={lastError.DbType}, extra={lastError.ExtraInfo}");
        }
        else
        {
            _options.WriteLog(
                $"[DEBUG] BusView XML convert failed: result={result} ({resultText}), type={attemptType}, lastErrorResult={lastResult}");
        }
    }

    private static string GetSpecResultText(int result)
    {
        var buffer = new StringBuilder(256);
        int status = jdeSpecGetResultText(buffer, buffer.Capacity, result);
        return status == JDESPEC_SUCCESS ? buffer.ToString() : "Unknown";
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

    private static string FindValue(XElement element, params string[] names)
    {
        foreach (string name in names)
        {
            XAttribute? attribute = element.Attributes().FirstOrDefault(
                candidate => string.Equals(candidate.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
            if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Value))
            {
                return NormalizeText(attribute.Value);
            }

            XElement? child = element.Elements().FirstOrDefault(
                candidate => string.Equals(candidate.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
            if (child != null && !string.IsNullOrWhiteSpace(child.Value))
            {
                return NormalizeText(child.Value);
            }
        }

        return string.Empty;
    }

    private static bool TryParseTable(XElement element, out JdeBusinessViewTable table)
    {
        table = new JdeBusinessViewTable();
        if (ContainsJoinMarkers(element))
        {
            return false;
        }

        string tableName = FindValue(element, "szTable", "tableName", "TableName");
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return false;
        }

        if (!ElementNameContains(element, "table"))
        {
            return false;
        }

        table.TableName = tableName;
        table.InstanceCount = ParseInt(FindValue(element, "nNumInstances", "instanceCount", "InstanceCount"));
        table.PrimaryIndexId = ParseInt(FindValue(element, "idPrimaryIndex", "primaryIndexId", "PrimaryIndexId"));
        return true;
    }

    private static bool TryParseColumn(XElement element, out JdeBusinessViewColumn column)
    {
        column = new JdeBusinessViewColumn();
        string dataItem = FindValue(element, "szDict", "dictItem", "DataItem", "dataItem");
        if (string.IsNullOrWhiteSpace(dataItem))
        {
            return false;
        }

        if (!ElementNameContains(element, "column"))
        {
            return false;
        }

        column.DataItem = dataItem;
        column.TableName = FindValue(element, "szTable", "tableName", "TableName");
        column.Sequence = ParseInt(FindValue(element, "nSeq", "sequence", "Sequence"));
        column.InstanceId = ParseInt(FindValue(element, "idInstance", "instanceId", "InstanceId"));
        column.DataType = ParseInt(FindValue(element, "idEvType", "dataType", "DataType"));
        column.Length = ParseInt(FindValue(element, "idLength", "length", "Length"));
        column.Decimals = ParseInt(FindValue(element, "nDecimals", "decimals", "Decimals"));
        column.DisplayDecimals = ParseInt(FindValue(element, "nDispDecimals", "displayDecimals", "DisplayDecimals"));
        column.TypeCode = ParseChar(FindValue(element, "cType", "typeCode", "TypeCode"));
        column.ClassCode = ParseChar(FindValue(element, "cClass", "classCode", "ClassCode"));
        return true;
    }

    private static bool TryParseJoin(XElement element, out JdeBusinessViewJoin join)
    {
        join = new JdeBusinessViewJoin();
        string foreignTable = FindValue(element, "szFTable", "foreignTable", "ForeignTable");
        string primaryTable = FindValue(element, "szPTable", "primaryTable", "PrimaryTable");
        string foreignColumn = FindValue(element, "szFDict", "foreignColumn", "ForeignColumn");
        string primaryColumn = FindValue(element, "szPDict", "primaryColumn", "PrimaryColumn");
        if (string.IsNullOrWhiteSpace(foreignTable) &&
            string.IsNullOrWhiteSpace(primaryTable) &&
            string.IsNullOrWhiteSpace(foreignColumn) &&
            string.IsNullOrWhiteSpace(primaryColumn))
        {
            return false;
        }

        if (!ElementNameContains(element, "join"))
        {
            return false;
        }

        join.ForeignTable = foreignTable;
        join.ForeignColumn = foreignColumn;
        join.ForeignInstanceId = ParseInt(FindValue(element, "idFInstance", "foreignInstanceId", "ForeignInstanceId"));
        join.PrimaryTable = primaryTable;
        join.PrimaryColumn = primaryColumn;
        join.PrimaryInstanceId = ParseInt(FindValue(element, "idPInstance", "primaryInstanceId", "PrimaryInstanceId"));

        string operatorValue = FindValue(element, "chOperator", "joinOperator", "JoinOperator", "operator");
        if (byte.TryParse(operatorValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte op))
        {
            join.JoinOperator = FormatJoinOperator(op);
        }
        else
        {
            join.JoinOperator = string.IsNullOrWhiteSpace(operatorValue) ? "=" : operatorValue;
        }

        string typeValue = FindValue(element, "chType", "joinType", "JoinType", "type");
        if (byte.TryParse(typeValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte type))
        {
            join.JoinType = FormatJoinType(type);
        }
        else
        {
            join.JoinType = string.IsNullOrWhiteSpace(typeValue) ? "Inner" : typeValue;
        }

        return true;
    }

    private static bool ContainsJoinMarkers(XElement element)
    {
        return !string.IsNullOrWhiteSpace(FindValue(element, "szFTable", "foreignTable", "szPTable", "primaryTable"));
    }

    private static bool ElementNameContains(XElement element, string token)
    {
        return element.Name.LocalName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : 0;
    }

    private static char ParseChar(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? '\0' : value.Trim()[0];
    }

    private static string FirstNonBlank(string primary, string fallback)
    {
        return string.IsNullOrWhiteSpace(primary) ? fallback : primary;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
