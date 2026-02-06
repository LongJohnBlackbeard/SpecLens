using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;
using JdeClient.Core.Exceptions;
using JdeClient.Core;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using static JdeClient.Core.Interop.JdeKernelApi;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Query engine for retrieving table rows using JDB_* APIs.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class JdeTableQueryEngine : IJdeTableQueryEngine
{
    private readonly JdeClientOptions _options;
    private HENV _hEnv;
    private HUSER _hUser;
    private bool _ownsEnv;
    private bool _ownsUser;
    private bool _isInitialized;
    private bool _disposed;

    public JdeTableQueryEngine(JdeClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Query a table with no filters.
    /// </summary>
    public JdeQueryResult QueryTable(string tableName, int maxRows)
    {
        return QueryTable(tableName, maxRows, Array.Empty<JdeFilter>(), null);
    }

    /// <summary>
    /// Query a table with the specified filters.
    /// </summary>
    public JdeQueryResult QueryTable(string tableName, int maxRows, IReadOnlyList<JdeFilter> filters)
    {
        return QueryTable(tableName, maxRows, filters, null);
    }

    /// <inheritdoc />
    public JdeQueryResult QueryTable(
        string tableName,
        int maxRows,
        IReadOnlyList<JdeFilter> filters,
        string? dataSourceOverride,
        IReadOnlyList<JdeSort>? sorts = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        EnsureInitialized();

        var columns = GetTableColumns(tableName);
        if (columns.Count == 0)
        {
            throw new JdeTableException(tableName, "No columns found in table specs.");
        }

        var columnsByName = BuildColumnMap(columns);
        var result = new JdeQueryResult
        {
            TableName = tableName,
            ColumnNames = columns.Select(column => column.Name).ToList(),
            MaxRows = maxRows > 0 ? maxRows : null
        };

        HREQUEST hRequest = new HREQUEST();
        try
        {
            string? resolvedDataSource = string.IsNullOrWhiteSpace(dataSourceOverride)
                ? DataSourceResolver.ResolveTableDataSource(_hUser, tableName)
                : dataSourceOverride;
            int openResult = JDB_OpenTable(
                _hUser,
                new NID(tableName),
                new ID(0),
                IntPtr.Zero,
                0,
                resolvedDataSource,
                out hRequest);

            if (openResult != JDEDB_PASSED || !hRequest.IsValid)
            {
                if (!string.IsNullOrWhiteSpace(resolvedDataSource))
                {
                    openResult = JDB_OpenTable(
                        _hUser,
                        new NID(tableName),
                        new ID(0),
                        IntPtr.Zero,
                        0,
                        null,
                        out hRequest);
                }
            }

            if (openResult != JDEDB_PASSED || !hRequest.IsValid)
            {
                throw new JdeTableException(tableName, "JDB_OpenTable failed", openResult);
            }

            LogRequestDataSource(hRequest, tableName);

            if (filters.Count > 0)
            {
                ApplyFilters(hRequest, tableName, filters, columnsByName);
            }

            ApplySequencing(hRequest, tableName, columnsByName, sorts);

            int selectResult = JDB_SelectKeyed(hRequest, new ID(0), IntPtr.Zero, 0);
            if (selectResult != JDEDB_PASSED)
            {
                throw new JdeTableException(tableName, "JDB_SelectKeyed failed", selectResult);
            }

            int rowCount = 0;
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
                    throw new JdeTableException(tableName, "JDB_Fetch failed", fetchResult);
                }

                // Skip ProcessFetchedRecord; it is unstable in this query path.

                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var column in columns)
                {
                    object? value = ReadValueFromColumns(hRequest, tableName, column);
                    row[column.Name] = value ?? string.Empty;
                }

                result.Rows.Add(row);
                rowCount++;

                if (maxRows > 0 && rowCount >= maxRows)
                {
                    result.IsTruncated = true;
                    break;
                }
            }

            return result;
        }
        finally
        {
            if (hRequest.IsValid)
            {
                JDB_CloseTable(hRequest);
            }
        }
    }

    /// <inheritdoc />
    public int CountTable(
        string tableName,
        IReadOnlyList<JdeFilter> filters,
        string? dataSourceOverride)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        EnsureInitialized();

        var safeFilters = filters ?? Array.Empty<JdeFilter>();
        IReadOnlyDictionary<string, JdeColumn> columnsByName;
        if (safeFilters.Count > 0)
        {
            var resolvedColumns = GetTableColumns(tableName);
            if (resolvedColumns.Count == 0)
            {
                throw new JdeTableException(tableName, "No columns found in table specs.");
            }

            columnsByName = BuildColumnMap(resolvedColumns);
        }
        else
        {
            columnsByName = new Dictionary<string, JdeColumn>(StringComparer.OrdinalIgnoreCase);
        }

        HREQUEST hRequest = new HREQUEST();
        try
        {
            string? resolvedDataSource = string.IsNullOrWhiteSpace(dataSourceOverride)
                ? DataSourceResolver.ResolveTableDataSource(_hUser, tableName)
                : dataSourceOverride;

            int openResult = JDB_OpenTable(
                _hUser,
                new NID(tableName),
                new ID(0),
                IntPtr.Zero,
                0,
                resolvedDataSource,
                out hRequest);

            if (openResult != JDEDB_PASSED || !hRequest.IsValid)
            {
                if (!string.IsNullOrWhiteSpace(resolvedDataSource))
                {
                    openResult = JDB_OpenTable(
                        _hUser,
                        new NID(tableName),
                        new ID(0),
                        IntPtr.Zero,
                        0,
                        null,
                        out hRequest);
                }
            }

            if (openResult != JDEDB_PASSED || !hRequest.IsValid)
            {
                throw new JdeTableException(tableName, "JDB_OpenTable failed", openResult);
            }

            LogRequestDataSource(hRequest, tableName);

            if (safeFilters.Count > 0)
            {
                ApplyFilters(hRequest, tableName, safeFilters, columnsByName);
            }

            uint count = JDB_SelectKeyedGetCount(hRequest, new ID(0), IntPtr.Zero, 0);
            return checked((int)count);
        }
        finally
        {
            if (hRequest.IsValid)
            {
                JDB_CloseTable(hRequest);
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<Dictionary<string, object>> StreamTableRows(
        string tableName,
        int maxRows,
        IReadOnlyList<JdeFilter> filters,
        IReadOnlyList<JdeColumn>? columns = null,
        string? dataSourceOverride = null,
        IReadOnlyList<JdeSort>? sorts = null,
        int? indexId = null,
        bool allowDataSourceFallback = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();

        var resolvedColumns = columns?.ToList() ?? GetTableColumns(tableName);
        if (resolvedColumns.Count == 0)
        {
            throw new JdeTableException(tableName, "No columns found in table specs.");
        }

        var columnsByName = BuildColumnMap(resolvedColumns);

        HREQUEST hRequest = new HREQUEST();
        try
        {
            string? resolvedDataSource = string.IsNullOrWhiteSpace(dataSourceOverride)
                ? DataSourceResolver.ResolveTableDataSource(_hUser, tableName)
                : dataSourceOverride;
            int openResult = JDB_OpenTable(
                _hUser,
                new NID(tableName),
                new ID(indexId ?? 0),
                IntPtr.Zero,
                0,
                resolvedDataSource,
                out hRequest);

            if (openResult != JDEDB_PASSED || !hRequest.IsValid)
            {
                if (allowDataSourceFallback && !string.IsNullOrWhiteSpace(resolvedDataSource))
                {
                    openResult = JDB_OpenTable(
                        _hUser,
                        new NID(tableName),
                        new ID(0),
                        IntPtr.Zero,
                        0,
                        null,
                        out hRequest);
                }
            }

            if (openResult != JDEDB_PASSED || !hRequest.IsValid)
            {
                throw new JdeTableException(tableName, "JDB_OpenTable failed", openResult);
            }

            LogRequestDataSource(hRequest, tableName);
            if (filters.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ApplyFilters(hRequest, tableName, filters, columnsByName);
            }

            ApplySequencing(hRequest, tableName, columnsByName, sorts);

            int selectResult = JDB_SelectKeyed(hRequest, new ID(0), IntPtr.Zero, 0);
            if (selectResult != JDEDB_PASSED)
            {
                throw new JdeTableException(tableName, "JDB_SelectKeyed failed", selectResult);
            }

            int rowCount = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                    throw new JdeTableException(tableName, "JDB_Fetch failed", fetchResult);
                }

                // Skip ProcessFetchedRecord for streaming; layout-based reads avoid it.

                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var column in resolvedColumns)
                {
                    object? value = ReadValueFromColumns(hRequest, tableName, column);
                    row[column.Name] = value ?? string.Empty;
                }

                yield return row;
                rowCount++;

                if (maxRows > 0 && rowCount >= maxRows)
                {
                    break;
                }
            }
        }
        finally
        {
            if (hRequest.IsValid)
            {
                JDB_CloseTable(hRequest);
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<Dictionary<string, object>> StreamViewRows(
        string viewName,
        int maxRows,
        IReadOnlyList<JdeFilter> filters,
        IReadOnlyList<JdeColumn>? columns = null,
        string? dataSourceOverride = null,
        IReadOnlyList<JdeSort>? sorts = null,
        bool allowDataSourceFallback = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(viewName))
        {
            throw new ArgumentException("View name is required.", nameof(viewName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();

        var resolvedColumns = columns?.ToList() ?? GetViewColumns(viewName);
        if (resolvedColumns.Count == 0)
        {
            throw new JdeTableException(viewName, "No columns found in view specs.");
        }

        var columnsByName = BuildColumnMap(resolvedColumns);

        HREQUEST hRequest = new HREQUEST();
        try
        {
            string? resolvedDataSource = string.IsNullOrWhiteSpace(dataSourceOverride)
                ? DataSourceResolver.ResolveTableDataSource(_hUser, viewName)
                : dataSourceOverride;
            int openResult = JDB_OpenView(
                _hUser,
                new NID(viewName),
                resolvedDataSource,
                out hRequest);

            if (openResult != JDEDB_PASSED || !hRequest.IsValid)
            {
                if (allowDataSourceFallback && !string.IsNullOrWhiteSpace(resolvedDataSource))
                {
                    openResult = JDB_OpenView(
                        _hUser,
                        new NID(viewName),
                        null,
                        out hRequest);
                }
            }

            if (openResult != JDEDB_PASSED || !hRequest.IsValid)
            {
                throw new JdeTableException(viewName, "JDB_OpenView failed", openResult);
            }

            LogRequestDataSource(hRequest, viewName);
            if (filters.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ApplyFilters(hRequest, viewName, filters, columnsByName);
            }

            ApplySequencing(hRequest, viewName, columnsByName, sorts, requirePrimaryIndex: false);

            int selectResult = JDB_SelectKeyed(hRequest, new ID(0), IntPtr.Zero, 0);
            if (selectResult != JDEDB_PASSED)
            {
                throw new JdeTableException(viewName, "JDB_SelectKeyed failed", selectResult);
            }

            int rowCount = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
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
                    throw new JdeTableException(viewName, "JDB_Fetch failed", fetchResult);
                }

                var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var column in resolvedColumns)
                {
                    object? value = ReadValueFromColumns(hRequest, viewName, column);
                    row[column.Name] = value ?? string.Empty;
                }

                yield return row;
                rowCount++;

                if (maxRows > 0 && rowCount >= maxRows)
                {
                    break;
                }
            }
        }
        finally
        {
            if (hRequest.IsValid)
            {
                JDB_CloseView(hRequest);
            }
        }
    }

    /// <inheritdoc />
    public JdeTableInfo GetTableInfo(string tableName, string? description, string? systemCode)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name is required.", nameof(tableName));
        }

        EnsureInitialized();

        var columns = GetTableColumns(tableName);
        if (columns.Count == 0)
        {
            throw new JdeTableException(tableName, "No columns found in table specs.");
        }

        return new JdeTableInfo
        {
            TableName = tableName,
            Description = description,
            SystemCode = systemCode,
            Columns = columns
        };
    }

    /// <inheritdoc />
    public JdeBusinessViewInfo? GetBusinessViewInfo(string viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName))
        {
            return null;
        }

        EnsureInitialized();

        using var specProvider = new SpecBusinessViewMetadataService(_hUser, _options);
        return specProvider.GetBusinessViewInfo(viewName);
    }

    /// <inheritdoc />
    public List<JdeColumn> GetViewColumns(string viewName)
    {
        var viewInfo = GetBusinessViewInfo(viewName);
        if (viewInfo == null || viewInfo.Columns.Count == 0)
        {
            return new List<JdeColumn>();
        }

        var columns = new List<JdeColumn>(viewInfo.Columns.Count);
        foreach (var column in viewInfo.Columns)
        {
            if (string.IsNullOrWhiteSpace(column.DataItem))
            {
                continue;
            }

            string columnName = BuildQualifiedViewColumnName(column.TableName, column.DataItem, column.InstanceId);
            columns.Add(new JdeColumn
            {
                Name = columnName,
                DataDictionaryItem = column.DataItem,
                SqlName = column.DataItem,
                DataType = column.DataType,
                Length = column.Length,
                Decimals = column.Decimals,
                SourceTable = column.TableName,
                InstanceId = column.InstanceId
            });
        }

        return columns;
    }

    private List<JdeColumn> GetTableColumns(string tableName)
    {
        using var specProvider = new SpecTableMetadataService(_hUser, _options);
        return specProvider.GetColumns(tableName);
    }

    /// <inheritdoc />
    public List<JdeIndexInfo> GetTableIndexes(string tableName)
    {
        using var specProvider = new SpecTableMetadataService(_hUser, _options);
        return specProvider.GetIndexes(tableName);
    }

    /// <inheritdoc />
    public List<JdeDataDictionaryTitle> GetDataDictionaryTitles(IEnumerable<string> dataItems, IReadOnlyList<int>? textTypes = null)
    {
        var results = new List<JdeDataDictionaryTitle>();
        var uniqueItems = dataItems
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniqueItems.Count == 0)
        {
            return results;
        }

        EnsureInitialized();
        textTypes ??= new[] { JdeStructures.DDT_COL_TITLE };
        IntPtr dictionary = JdeSpecApi.jdeOpenDictionaryX(_hUser);
        if (dictionary == IntPtr.Zero)
        {
            return results;
        }

        try
        {
            foreach (var item in uniqueItems)
            {
                string? text = FetchDictionaryText(dictionary, item, textTypes);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var (title1, title2) = SplitDictionaryText(text);
                if (string.IsNullOrWhiteSpace(title1) && string.IsNullOrWhiteSpace(title2))
                {
                    continue;
                }

                results.Add(new JdeDataDictionaryTitle
                {
                    DataItem = item,
                    Title1 = title1,
                    Title2 = title2
                });
            }
        }
        finally
        {
            JdeSpecApi.jdeCloseDictionary(dictionary);
        }

        return results;
    }

    /// <inheritdoc />
    public List<JdeDataDictionaryItemName> GetDataDictionaryItemNames(IEnumerable<string> dataItems)
    {
        var results = new List<JdeDataDictionaryItemName>();
        var uniqueItems = dataItems
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniqueItems.Count == 0)
        {
            return results;
        }

        EnsureInitialized();
        IntPtr dictionary = JdeSpecApi.jdeOpenDictionaryX(_hUser);
        if (dictionary == IntPtr.Zero)
        {
            return results;
        }

        try
        {
            foreach (var item in uniqueItems)
            {
                IntPtr ddItemPtr = IntPtr.Zero;
                try
                {
                    ddItemPtr = JdeSpecApi.jdeAllocFetchDDItemFromDDItemName(dictionary, item);
                    if (ddItemPtr == IntPtr.Zero)
                    {
                        continue;
                    }

                    var ddItem = Marshal.PtrToStructure<JdeStructures.DDDICT>(ddItemPtr);
                    string name = NormalizeDictionaryText(ddItem.szAlias);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    results.Add(new JdeDataDictionaryItemName
                    {
                        DataItem = item,
                        Name = name
                    });
                }
                finally
                {
                    if (ddItemPtr != IntPtr.Zero)
                    {
                        JdeSpecApi.jdeDDDictFree(ddItemPtr);
                    }
                }
            }
        }
        finally
        {
            JdeSpecApi.jdeCloseDictionary(dictionary);
        }

        return results;
    }

    /// <inheritdoc />
    public List<JdeDataDictionaryDetails> GetDataDictionaryDetails(IEnumerable<string> dataItems)
    {
        var results = new List<JdeDataDictionaryDetails>();
        var uniqueItems = dataItems
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (uniqueItems.Count == 0)
        {
            return results;
        }

        EnsureInitialized();
        IntPtr dictionary = JdeSpecApi.jdeOpenDictionaryX(_hUser);
        if (dictionary == IntPtr.Zero)
        {
            return results;
        }

        try
        {
            foreach (var item in uniqueItems)
            {
                IntPtr ddItemPtr = IntPtr.Zero;
                try
                {
                    ddItemPtr = JdeSpecApi.jdeAllocFetchDDItemFromDDItemName(dictionary, item);
                    if (ddItemPtr == IntPtr.Zero)
                    {
                        continue;
                    }

                    var ddItem = Marshal.PtrToStructure<JdeStructures.DDDICT>(ddItemPtr);
                    var details = new JdeDataDictionaryDetails
                    {
                        DataItem = item,
                        VarLength = ddItem.lVarLen,
                        FormatNumber = ddItem.idFormatNum,
                        DictionaryName = NormalizeDictionaryText(ddItem.szDict.Value),
                        SystemCode = NormalizeDictionaryText(ddItem.szSystemCode),
                        GlossaryGroup = ddItem.cGlossaryGroup,
                        ErrorLevel = ddItem.cErrorLevel,
                        Alias = NormalizeDictionaryText(ddItem.szAlias),
                        TypeCode = ddItem.cType,
                        EverestType = ddItem.idEverestType,
                        As400Class = NormalizeDictionaryText(ddItem.szAS400Class.Value),
                        Length = ddItem.idLength,
                        Decimals = ddItem.nDecimals,
                        DisplayDecimals = ddItem.nDispDecimals,
                        DefaultValue = NormalizeDictionaryText(ddItem.szDfltValue),
                        ControlType = ddItem.nControlType,
                        As400EditRule = NormalizeDictionaryText(ddItem.szAS400EditRule),
                        As400EditParm1 = NormalizeDictionaryText(ddItem.szAS400EditParm1),
                        As400EditParm2 = NormalizeDictionaryText(ddItem.szAS400EditParm2),
                        As400DispRule = NormalizeDictionaryText(ddItem.szAS400DispRule),
                        As400DispParm = NormalizeDictionaryText(ddItem.szAS400DispParm),
                        EditBehavior = ddItem.idEditBhvr,
                        DisplayBehavior = ddItem.idDispBhvr,
                        SecurityFlag = ddItem.cSecurityFlag,
                        NextNumberIndex = ddItem.nNextNumberIndex,
                        NextNumberSystem = NormalizeDictionaryText(ddItem.szNextNumberSystem),
                        Style = ddItem.idStyle,
                        Behavior = ddItem.idBehavior,
                        DataSourceTemplateName = NormalizeDictionaryText(ddItem.szDsTmplName.Value),
                        DisplayRuleBfnName = NormalizeDictionaryText(ddItem.szDispRuleBFName),
                        EditRuleBfnName = NormalizeDictionaryText(ddItem.szEditRuleBFName),
                        SearchFormName = NormalizeDictionaryText(ddItem.szSearchFormName.Value)
                    };

                    var textTypes = new[]
                    {
                        JdeStructures.DDT_ALPHA_DESC,
                        JdeStructures.DDT_ROW_DESC,
                        JdeStructures.DDT_COL_TITLE,
                        JdeStructures.DDT_GLOSSARY
                    };

                    foreach (var textType in textTypes)
                    {
                        var textDetails = FetchDictionaryTextDetails(dictionary, item, textType);
                        if (textDetails != null)
                        {
                            details.Texts.Add(textDetails);
                        }
                    }

                    results.Add(details);
                }
                finally
                {
                    if (ddItemPtr != IntPtr.Zero)
                    {
                        JdeSpecApi.jdeDDDictFree(ddItemPtr);
                    }
                }
            }
        }
        finally
        {
            JdeSpecApi.jdeCloseDictionary(dictionary);
        }

        return results;
    }

    private static string NormalizeDictionaryText(string? value)
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

    private static JdeDataDictionaryText? FetchDictionaryTextDetails(IntPtr dictionary, string item, int textType)
    {
        IntPtr textPtr = IntPtr.Zero;
        try
        {
            textPtr = JdeSpecApi.jdeAllocFetchDDTextFromDDItemNameOvr(
                dictionary,
                item,
                textType,
                "  ",
                null);

            if (textPtr == IntPtr.Zero)
            {
                return null;
            }

            var ddText = Marshal.PtrToStructure<JdeStructures.DDTEXT>(textPtr);
            int textOffset = Marshal.OffsetOf<JdeStructures.DDTEXT>(nameof(JdeStructures.DDTEXT.szText)).ToInt32();
            int bytesAvailable = (int)ddText.lVarLen - textOffset;
            if (bytesAvailable < 0)
            {
                bytesAvailable = 0;
            }

            int charCount = bytesAvailable / 2;
            string? text = charCount > 0
                ? Marshal.PtrToStringUni(IntPtr.Add(textPtr, textOffset), charCount)
                : null;

            if (!string.IsNullOrEmpty(text))
            {
                int nullIndex = text.IndexOf('\0');
                if (nullIndex >= 0)
                {
                    text = text.Substring(0, nullIndex);
                }
            }

            char textTypeChar = ddText.cTextType == '\0' ? (char)textType : ddText.cTextType;
            return new JdeDataDictionaryText
            {
                DataItem = item,
                TextType = textTypeChar,
                Language = NormalizeDictionaryText(ddText.szLanguage),
                SystemCode = NormalizeDictionaryText(ddText.szSystemCode),
                DictionaryName = NormalizeDictionaryText(ddText.szDict.Value),
                VarLength = ddText.lVarLen,
                FormatNumber = ddText.idFormatNum,
                Text = NormalizeDictionaryText(text)
            };
        }
        finally
        {
            if (textPtr != IntPtr.Zero)
            {
                JdeSpecApi.jdeTextFree(textPtr);
            }
        }
    }

    private static (string? Title1, string? Title2) SplitDictionaryText(string text)
    {
        string cleaned = NormalizeDictionaryText(text);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return (null, null);
        }

        var lines = cleaned
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();

        if (lines.Count == 0)
        {
            return (null, null);
        }

        string title1 = lines[0];
        string? title2 = lines.Count > 1 ? lines[1] : null;
        return (title1, title2);
    }

    private static string? FetchDictionaryText(IntPtr dictionary, string item, IReadOnlyList<int> textTypes)
    {
        foreach (var textType in textTypes)
        {
            IntPtr textPtr = IntPtr.Zero;
            try
            {
                textPtr = JdeSpecApi.jdeAllocFetchTextFromDDItemNameOvr(
                    dictionary,
                    item,
                    textType,
                    "  ",
                    null);

                if (textPtr == IntPtr.Zero)
                {
                    continue;
                }

                string text = Marshal.PtrToStringUni(textPtr) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
            finally
            {
                if (textPtr != IntPtr.Zero)
                {
                    JdeSpecApi.jdeTextFree(textPtr);
                }
            }
        }

        return null;
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        _hEnv = new HENV();
        int result = JDB_GetEnv(ref _hEnv);
        if (result == JDEDB_PASSED && _hEnv.IsValid)
        {
            _ownsEnv = false;
        }
        else
        {
            result = JDB_InitEnv(ref _hEnv);
            if (result != JDEDB_PASSED || !_hEnv.IsValid)
            {
                throw new JdeConnectionException($"Failed to initialize JDE environment. Error code: {result}");
            }
            _ownsEnv = true;
        }

        result = JDB_InitUser(_hEnv, out _hUser, "", JDEDB_COMMIT_AUTO);
        if (result != JDEDB_PASSED)
        {
            if (_ownsEnv)
            {
                JDB_FreeEnv(_hEnv);
            }
            throw new JdeConnectionException($"Failed to initialize JDE user context. Error code: {result}");
        }
        _ownsUser = true;

        if (!_hUser.IsValid)
        {
            if (_ownsEnv)
            {
                JDB_FreeEnv(_hEnv);
            }
            throw new JdeConnectionException("Failed to initialize JDE user context (invalid handle).");
        }

        _isInitialized = true;
    }

    private void ProcessFetchedRecord(HREQUEST hRequest, bool force = false)
    {
        if (!force && !_options.UseProcessFetchedRecord)
        {
            return;
        }

        int flags = RECORD_CONVERT | RECORD_PROCESS | RECORD_TRIGGERS;
        HREQUEST driverRequest = ResolveDriverRequest(hRequest);
        for (int i = 0; i < 3; i++)
        {
            JDB_ProcessFetchedRecord(hRequest, driverRequest, flags);
        }
    }

    private static HREQUEST ResolveDriverRequest(HREQUEST hRequest)
    {
        return hRequest;
    }

    private static string ReadStringValue(IntPtr valuePtr)
    {
        if (valuePtr == IntPtr.Zero)
        {
            return string.Empty;
        }

        string ansiValue = Marshal.PtrToStringAnsi(valuePtr) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(ansiValue))
        {
            return NormalizeText(ansiValue);
        }

        return NormalizeText(Marshal.PtrToStringUni(valuePtr) ?? string.Empty);
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

    private static object? ReadValueFromLayout(
        TableLayout layout,
        IntPtr rowBuffer,
        JdeColumn column)
    {
        var raw = layout.ReadValueByColumn(rowBuffer, column.Name);
        if (raw.Value == null || (raw.Value is string text && string.IsNullOrWhiteSpace(text)))
        {
            if (!string.IsNullOrWhiteSpace(column.SqlName) && !string.Equals(column.SqlName, column.Name, StringComparison.OrdinalIgnoreCase))
            {
                raw = layout.ReadValueByColumn(rowBuffer, column.SqlName);
            }

            if (!string.IsNullOrWhiteSpace(column.DataDictionaryItem) &&
                !string.Equals(column.DataDictionaryItem, column.Name, StringComparison.OrdinalIgnoreCase))
            {
                raw = layout.ReadValueByColumn(rowBuffer, column.DataDictionaryItem);
            }
        }
        return FormatValue(raw, column);
    }

    private static object? ReadValueFromColumns(
        HREQUEST hRequest,
        string tableName,
        JdeColumn column)
    {
        string?[] candidates =
        {
            column.DataDictionaryItem,
            column.Name,
            column.SqlName
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            string columnName = candidate;
            string? tableOverride = null;
            if (TrySplitQualifiedColumnName(candidate, out var tableNameOverride, out var unqualified))
            {
                tableOverride = tableNameOverride;
                columnName = unqualified;
            }

            var dbRef = BuildFilterDbRef(tableName, columnName, column, tableOverride);
            IntPtr valuePtr = JDB_GetTableColValue(hRequest, dbRef);
            if (valuePtr == IntPtr.Zero)
            {
                continue;
            }

            var rawValue = ReadValueFromPointer(valuePtr, column);
            return FormatValue(rawValue, column);
        }

        return string.Empty;
    }

    private static TableValue ReadValueFromPointer(IntPtr valuePtr, JdeColumn column)
    {
        if (valuePtr == IntPtr.Zero)
        {
            return new TableValue(TableFieldType.JCharArray, string.Empty);
        }

        switch (column.DataType)
        {
            case EVDT_MATH_NUMERIC:
            {
                string text = MathNumericParser.ToString(valuePtr);
                return new TableValue(TableFieldType.MathNumeric, text);
            }
            case EVDT_JDEDATE:
            {
                short year = Marshal.ReadInt16(valuePtr, 0);
                short month = Marshal.ReadInt16(valuePtr, 2);
                short day = Marshal.ReadInt16(valuePtr, 4);
                if (year > 0 && month > 0 && day > 0)
                {
                    try
                    {
                        return new TableValue(TableFieldType.JdeDate, new DateTime(year, month, day));
                    }
                    catch
                    {
                        return new TableValue(TableFieldType.JdeDate, string.Empty);
                    }
                }
                return new TableValue(TableFieldType.JdeDate, string.Empty);
            }
            case EVDT_CHAR:
            case EVDT_STRING:
            case EVDT_VARSTRING:
            case EVDT_TEXT:
            case EVDT_LONGVARCHAR:
            {
                int length = column.Length;
                if (length > 0 && length < 4096)
                {
                    return new TableValue(TableFieldType.JCharArray, ReadJCharString(valuePtr, length));
                }
                return new TableValue(TableFieldType.JCharArray, ReadStringValue(valuePtr));
            }
            case EVDT_SHORT:
                return new TableValue(TableFieldType.Id, Marshal.ReadInt16(valuePtr, 0));
            case EVDT_USHORT:
                return new TableValue(TableFieldType.Id, (ushort)Marshal.ReadInt16(valuePtr, 0));
            case EVDT_LONG:
            case EVDT_INT:
            case EVDT_ID:
            case EVDT_ID2:
                return new TableValue(TableFieldType.Id, Marshal.ReadInt32(valuePtr, 0));
            case EVDT_ULONG:
            case EVDT_UINT:
                return new TableValue(TableFieldType.Id, unchecked((uint)Marshal.ReadInt32(valuePtr, 0)));
            case EVDT_BYTE:
            case EVDT_BOOL:
                return new TableValue(TableFieldType.Id, Marshal.ReadByte(valuePtr, 0));
            default:
                return new TableValue(TableFieldType.JCharArray, ReadStringValue(valuePtr));
        }
    }

    private static string ReadJCharString(IntPtr valuePtr, int length)
    {
        if (length <= 0)
        {
            return string.Empty;
        }

        int byteCount = length * 2;
        var bytes = new byte[byteCount];
        Marshal.Copy(valuePtr, bytes, 0, byteCount);
        return NormalizeText(Encoding.Unicode.GetString(bytes));
    }

    private static object? FormatValue(TableValue raw, JdeColumn column)
    {
        switch (column.DataType)
        {
            case EVDT_JDEDATE:
                if (raw.Value is DateTime dateTime)
                {
                    return dateTime.ToString("yyyy-MM-dd");
                }
                if (raw.Value is string dateText && int.TryParse(dateText, out int julian))
                {
                    return JdeJulianDateConverter.ToDateString(julian);
                }
                return raw.Value?.ToString() ?? string.Empty;
            case EVDT_MATH_NUMERIC:
                if (raw.Value is string numericText)
                {
                    string trimmed = numericText.Trim();
                    if (trimmed.Contains('.', StringComparison.Ordinal))
                    {
                        return trimmed;
                    }
                    return FormatNumeric(trimmed, column.Decimals);
                }
                return FormatNumeric(raw.Value?.ToString(), column.Decimals);
            case EVDT_SHORT:
            case EVDT_USHORT:
            case EVDT_LONG:
            case EVDT_ULONG:
            case EVDT_INT:
            case EVDT_UINT:
            case EVDT_ID:
            case EVDT_ID2:
                return FormatNumeric(raw.Value?.ToString(), 0);
            default:
                return raw.Value is string text ? NormalizeText(text) : raw.Value ?? string.Empty;
        }
    }

    private static void ProcessFetchedRecordConvertOnly(HREQUEST hRequest)
    {
        int flags = RECORD_CONVERT;
        JDB_ProcessFetchedRecord(hRequest, hRequest, flags);
    }

    private static string FormatNumeric(string? raw, int decimals)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        bool negative = raw.StartsWith("-", StringComparison.Ordinal);
        if (negative)
        {
            raw = raw.Substring(1);
        }

        raw = raw.Replace(".", "", StringComparison.Ordinal);
        raw = raw.TrimStart('0');
        if (raw.Length == 0)
        {
            raw = "0";
        }
        if (decimals <= 0)
        {
            return negative ? "-" + raw : raw;
        }

        if (raw.Length <= decimals)
        {
            raw = raw.PadLeft(decimals + 1, '0');
        }

        int index = raw.Length - decimals;
        string formatted = raw.Insert(index, ".");
        formatted = TrimTrailingZeros(formatted);
        return negative ? "-" + formatted : formatted;
    }

    private static string TrimTrailingZeros(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('.'))
        {
            return value;
        }

        value = value.TrimEnd('0');
        if (value.EndsWith(".", StringComparison.Ordinal))
        {
            value = value.TrimEnd('.');
        }

        return value;
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (ch == '\0')
            {
                continue;
            }

            if (char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t')
            {
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static NID[] BuildFetchColumns(IReadOnlyList<JdeColumn> columns)
    {
        var list = new NID[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            string name = column.DataDictionaryItem ?? column.Name ?? column.SqlName;
            list[i] = new NID(name);
        }
        return list;
    }

    private void ApplyFilters(HREQUEST hRequest, string tableName, IReadOnlyList<JdeFilter> filters, IReadOnlyDictionary<string, JdeColumn> columnsByName)
    {
        if (filters.Count == 0)
        {
            return;
        }

        if (TryApplyTypedSelection(hRequest, tableName, filters, columnsByName))
        {
            return;
        }

        if (TryApplySqlNameSelection(hRequest, tableName, filters, columnsByName))
        {
            return;
        }

        if (TryApplyLegacySelection(hRequest, tableName, filters, columnsByName, useSqlName: false))
        {
            return;
        }

        if (TryApplyLegacySelection(hRequest, tableName, filters, columnsByName, useSqlName: true))
        {
            return;
        }

        throw new JdeTableException(tableName, "JDB_SetSelection failed");
    }

    private void ApplySequencing(
        HREQUEST hRequest,
        string tableName,
        IReadOnlyDictionary<string, JdeColumn> columnsByName,
        IReadOnlyList<JdeSort>? sorts,
        bool requirePrimaryIndex = true)
    {
        if (tableName.Equals("F9860", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SORTSTRUCT[] sort;
        if (sorts != null && sorts.Count > 0)
        {
            sort = new SORTSTRUCT[sorts.Count];
            for (int i = 0; i < sorts.Count; i++)
            {
                var sortItem = sorts[i];
                var target = ResolveFilterTarget(columnsByName, sortItem.ColumnName);
                string columnName = target.Column?.DataDictionaryItem ?? target.ColumnName;

                if (string.IsNullOrWhiteSpace(columnName))
                {
                    throw new JdeTableException(tableName, $"Sort column '{sortItem.ColumnName}' was not resolved.");
                }

                sort[i].Item = BuildFilterDbRef(tableName, columnName, target.Column, target.TableOverride);
                sort[i].nSort = sortItem.Direction == JdeSortDirection.Descending
                    ? JDEDB_SORT_DESC
                    : JDEDB_SORT_ASC;
            }
        }
        else
        {
            if (!requirePrimaryIndex)
            {
                return;
            }

            using var specProvider = new SpecTableMetadataService(_hUser, _options);
            if (!specProvider.TryGetPrimaryIndex(tableName, out _, out var keyColumns) || keyColumns.Count == 0)
            {
                throw new JdeTableException(tableName, "Primary index not found; cannot set sequencing.");
            }

            sort = new SORTSTRUCT[keyColumns.Count];
            for (int i = 0; i < keyColumns.Count; i++)
            {
                string keyColumn = keyColumns[i];
                if (columnsByName.TryGetValue(keyColumn, out var column))
                {
                    keyColumn = column.DataDictionaryItem ?? column.Name ?? keyColumn;
                }

                if (string.IsNullOrWhiteSpace(keyColumn))
                {
                    throw new JdeTableException(tableName, "Primary index contains an unresolved column.");
                }

                sort[i].Item = new DBREF(tableName, keyColumn, 0);
                sort[i].nSort = JDEDB_SORT_ASC;
            }
        }

        int result = JDB_SetSequencing(hRequest, sort, (ushort)sort.Length, JDEDB_SET_REPLACE);
        if (result != JDEDB_PASSED)
        {
            throw new JdeTableException(tableName, "JDB_SetSequencing failed", result);
        }
    }

    private static IntPtr AllocateFilterValue(IReadOnlyDictionary<string, JdeColumn> columnsByName, JdeFilter filter)
    {
        if (!columnsByName.TryGetValue(filter.ColumnName, out var column))
        {
            return AllocateFilterValue(column, filter.Value);
        }

        return AllocateFilterValue(column, filter.Value);
    }

    private static IntPtr AllocateFilterValue(JdeColumn? column, string value)
    {
        if (column == null)
        {
            return Marshal.StringToHGlobalUni(value);
        }

        switch (column.DataType)
        {
            case EVDT_MATH_NUMERIC:
            case EVDT_SHORT:
            case EVDT_USHORT:
            case EVDT_LONG:
            case EVDT_ULONG:
            case EVDT_INT:
            case EVDT_UINT:
            case EVDT_ID:
            case EVDT_ID2:
                return AllocateMathNumeric(value);
            default:
                return Marshal.StringToHGlobalUni(value);
        }
    }

    private bool TryApplySqlNameSelection(
        HREQUEST hRequest,
        string tableName,
        IReadOnlyList<JdeFilter> filters,
        IReadOnlyDictionary<string, JdeColumn> columnsByName)
    {
        var select = new NEWSELECTSTRUCT[filters.Count];
        var valuePtrs = new IntPtr[filters.Count];

        try
        {
            JDB_ClearSelection(hRequest);
            for (int i = 0; i < filters.Count; i++)
            {
                var filter = filters[i];
                var target = ResolveFilterTarget(columnsByName, filter.ColumnName);
                valuePtrs[i] = AllocateFilterValue(target.Column, filter.Value);
                string columnName = target.Column?.SqlName ?? target.ColumnName;
                select[i].Item1 = BuildFilterDbRef(tableName, columnName, target.Column, target.TableOverride);
                select[i].Item2 = new DBREF(string.Empty, string.Empty, 0);
                select[i].lpValue = valuePtrs[i];
                select[i].nValues = 1;
                select[i].nAndOr = JDEDB_ANDOR_AND;
                select[i].nCmp = filter.Operator switch
                {
                    JdeFilterOperator.Equals => JDEDB_CMP_EQ,
                    JdeFilterOperator.NotEquals => JDEDB_CMP_NE,
                    JdeFilterOperator.LessThan => JDEDB_CMP_LT,
                    JdeFilterOperator.GreaterThan => JDEDB_CMP_GT,
                    JdeFilterOperator.LessThanOrEqual => JDEDB_CMP_LE,
                    JdeFilterOperator.GreaterThanOrEqual => JDEDB_CMP_GE,
                    JdeFilterOperator.Like => JDEDB_CMP_LK,
                    _ => JDEDB_CMP_EQ
                };
                select[i].nParen = 0;
                select[i].cFuture1 = 0;
                select[i].cFuture2 = '\0';
            }

            int setResult = JDB_SetSelectionX(hRequest, select, (ushort)select.Length, JDEDB_SET_REPLACE);
            if (setResult != JDEDB_PASSED)
            {
                LogSelectionError(hRequest, tableName, "JDB_SetSelectionX(sql)", setResult);
            }
            return setResult == JDEDB_PASSED;
        }
        finally
        {
            for (int i = 0; i < valuePtrs.Length; i++)
            {
                if (valuePtrs[i] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(valuePtrs[i]);
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, JdeColumn> BuildColumnMap(IEnumerable<JdeColumn> columns)
    {
        var map = new Dictionary<string, JdeColumn>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            if (!string.IsNullOrWhiteSpace(column.Name))
            {
                map[column.Name] = column;
            }

            if (!string.IsNullOrWhiteSpace(column.DataDictionaryItem))
            {
                map[column.DataDictionaryItem] = column;
            }

            if (!string.IsNullOrWhiteSpace(column.SqlName))
            {
                map[column.SqlName] = column;
            }

            if (!string.IsNullOrWhiteSpace(column.SourceTable))
            {
                string qualified = BuildQualifiedViewColumnName(column.SourceTable, column.DataDictionaryItem ?? column.Name ?? column.SqlName, column.InstanceId ?? 0);
                if (!string.IsNullOrWhiteSpace(qualified))
                {
                    map[qualified] = column;
                }
            }
        }

        return map;
    }

    private bool TryApplyLegacySelection(
        HREQUEST hRequest,
        string tableName,
        IReadOnlyList<JdeFilter> filters,
        IReadOnlyDictionary<string, JdeColumn> columnsByName,
        bool useSqlName)
    {
        var select = new SELECTSTRUCT[filters.Count];
        var valuePtrs = new IntPtr[filters.Count];

        try
        {
            JDB_ClearSelection(hRequest);
            for (int i = 0; i < filters.Count; i++)
            {
                var filter = filters[i];
                var target = ResolveFilterTarget(columnsByName, filter.ColumnName);
                string columnName = useSqlName ? (target.Column?.SqlName ?? target.ColumnName) : (target.Column?.DataDictionaryItem ?? target.ColumnName);
                valuePtrs[i] = Marshal.StringToHGlobalUni(filter.Value);
                select[i].Item1 = BuildFilterDbRef(tableName, columnName, target.Column, target.TableOverride);
                select[i].Item2 = new DBREF(string.Empty, string.Empty, 0);
                select[i].lpValue = valuePtrs[i];
                select[i].nValues = 1;
                select[i].nAndOr = JDEDB_ANDOR_AND;
                select[i].nCmp = filter.Operator switch
                {
                    JdeFilterOperator.Equals => JDEDB_CMP_EQ,
                    JdeFilterOperator.NotEquals => JDEDB_CMP_NE,
                    JdeFilterOperator.LessThan => JDEDB_CMP_LT,
                    JdeFilterOperator.GreaterThan => JDEDB_CMP_GT,
                    JdeFilterOperator.LessThanOrEqual => JDEDB_CMP_LE,
                    JdeFilterOperator.GreaterThanOrEqual => JDEDB_CMP_GE,
                    JdeFilterOperator.Like => JDEDB_CMP_LK,
                    _ => JDEDB_CMP_EQ
                };
            }

            int setResult = JDB_SetSelection(hRequest, select, (ushort)select.Length, JDEDB_SET_REPLACE);
            if (setResult != JDEDB_PASSED)
            {
                string label = useSqlName ? "JDB_SetSelection(sql)" : "JDB_SetSelection(dict)";
                LogSelectionError(hRequest, tableName, label, setResult);
            }
            return setResult == JDEDB_PASSED;
        }
        finally
        {
            for (int i = 0; i < valuePtrs.Length; i++)
            {
                if (valuePtrs[i] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(valuePtrs[i]);
                }
            }
        }
    }

    private void LogSelectionError(HREQUEST hRequest, string tableName, string apiName, int result)
    {
        if (!_options.EnableQueryDebug)
        {
            return;
        }

        if (JDB_GetLastDBError(hRequest, out int errorNum) == JDEDB_PASSED)
        {
            _options.WriteLog($"[DEBUG] {apiName} {tableName} failed (result={result}, error={errorNum})");
        }
        else
        {
            _options.WriteLog($"[DEBUG] {apiName} {tableName} failed (result={result})");
        }
    }

    private static bool RowMatchesFilters(
        IReadOnlyDictionary<string, object> row,
        IReadOnlyList<JdeFilter> filters,
        IReadOnlyDictionary<string, JdeColumn> columnsByName)
    {
        foreach (var filter in filters)
        {
            if (!row.TryGetValue(filter.ColumnName, out var rawValue))
            {
                return false;
            }

            string rawText = rawValue?.ToString() ?? string.Empty;
            if (!columnsByName.TryGetValue(filter.ColumnName, out var column))
            {
                column = null;
            }

            if (column != null && IsNumericType(column.DataType))
            {
                if (!decimal.TryParse(rawText, out var actual) || !decimal.TryParse(filter.Value, out var expected))
                {
                    return false;
                }

                if (!CompareNumeric(actual, expected, filter.Operator))
                {
                    return false;
                }

                continue;
            }

            if (!CompareString(rawText, filter.Value, filter.Operator))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNumericType(int evdType)
    {
        return evdType switch
        {
            EVDT_MATH_NUMERIC => true,
            EVDT_SHORT => true,
            EVDT_USHORT => true,
            EVDT_LONG => true,
            EVDT_ULONG => true,
            EVDT_INT => true,
            EVDT_UINT => true,
            EVDT_ID => true,
            EVDT_ID2 => true,
            _ => false
        };
    }

    private static bool CompareNumeric(decimal actual, decimal expected, JdeFilterOperator op)
    {
        return op switch
        {
            JdeFilterOperator.Equals => actual == expected,
            JdeFilterOperator.NotEquals => actual != expected,
            JdeFilterOperator.LessThan => actual < expected,
            JdeFilterOperator.GreaterThan => actual > expected,
            JdeFilterOperator.LessThanOrEqual => actual <= expected,
            JdeFilterOperator.GreaterThanOrEqual => actual >= expected,
            _ => actual == expected
        };
    }

    private bool TryApplyTypedSelection(
        HREQUEST hRequest,
        string tableName,
        IReadOnlyList<JdeFilter> filters,
        IReadOnlyDictionary<string, JdeColumn> columnsByName)
    {
        var select = new SELECTSTRUCT[filters.Count];
        var valuePtrs = new IntPtr[filters.Count];

        try
        {
            JDB_ClearSelection(hRequest);
            for (int i = 0; i < filters.Count; i++)
            {
                var filter = filters[i];
                var target = ResolveFilterTarget(columnsByName, filter.ColumnName);
                valuePtrs[i] = AllocateFilterValue(target.Column, filter.Value);
                string dictName = target.Column?.DataDictionaryItem ?? target.ColumnName;
                if (string.IsNullOrWhiteSpace(dictName))
                {
                    throw new JdeTableException(tableName, $"Filter column '{filter.ColumnName}' was not resolved.");
                }

                select[i].Item1 = BuildFilterDbRef(tableName, dictName, target.Column, target.TableOverride);
                select[i].Item2 = new DBREF(string.Empty, string.Empty, 0);
                select[i].lpValue = valuePtrs[i];
                select[i].nValues = 1;
                select[i].nAndOr = JDEDB_ANDOR_AND;
                select[i].nCmp = filter.Operator switch
                {
                    JdeFilterOperator.Equals => JDEDB_CMP_EQ,
                    JdeFilterOperator.NotEquals => JDEDB_CMP_NE,
                    JdeFilterOperator.LessThan => JDEDB_CMP_LT,
                    JdeFilterOperator.GreaterThan => JDEDB_CMP_GT,
                    JdeFilterOperator.LessThanOrEqual => JDEDB_CMP_LE,
                    JdeFilterOperator.GreaterThanOrEqual => JDEDB_CMP_GE,
                    JdeFilterOperator.Like => JDEDB_CMP_LK,
                    _ => JDEDB_CMP_EQ
                };
            }

            int setResult = JDB_SetSelection(hRequest, select, (ushort)select.Length, JDEDB_SET_REPLACE);
            if (setResult != JDEDB_PASSED)
            {
                LogSelectionError(hRequest, tableName, "JDB_SetSelection", setResult);
            }
            return setResult == JDEDB_PASSED;
        }
        finally
        {
            for (int i = 0; i < valuePtrs.Length; i++)
            {
                if (valuePtrs[i] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(valuePtrs[i]);
                }
            }
        }
    }

    private static DBREF BuildFilterDbRef(string defaultTableName, string columnName, JdeColumn? column, string? tableOverride = null)
    {
        string tableName = defaultTableName;
        int instanceId = 0;
        if (!string.IsNullOrWhiteSpace(tableOverride))
        {
            tableName = tableOverride!;
            if (column != null && !string.IsNullOrWhiteSpace(column.SourceTable) &&
                tableOverride.Equals(column.SourceTable, StringComparison.OrdinalIgnoreCase))
            {
                instanceId = column.InstanceId ?? 0;
            }
        }
        else if (!string.IsNullOrWhiteSpace(column?.SourceTable))
        {
            tableName = column.SourceTable!;
            instanceId = column.InstanceId ?? 0;
        }

        return new DBREF(tableName, columnName, instanceId);
    }

    private static FilterTarget ResolveFilterTarget(IReadOnlyDictionary<string, JdeColumn> columnsByName, string columnName)
    {
        JdeColumn? column = null;
        if (columnsByName.TryGetValue(columnName, out var match))
        {
            column = match;
        }

        string? tableOverride = null;
        string resolvedColumnName = columnName;
        if (TrySplitQualifiedColumnName(columnName, out var tableName, out var unqualified))
        {
            tableOverride = tableName;
            resolvedColumnName = unqualified;
            if (column == null && columnsByName.TryGetValue(unqualified, out match))
            {
                column = match;
            }
        }

        return new FilterTarget(resolvedColumnName, tableOverride, column);
    }

    private static bool TrySplitQualifiedColumnName(string columnName, out string tableName, out string itemName)
    {
        tableName = string.Empty;
        itemName = string.Empty;
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return false;
        }

        int index = columnName.IndexOf('.', StringComparison.Ordinal);
        if (index <= 0 || index >= columnName.Length - 1)
        {
            return false;
        }

        tableName = columnName.Substring(0, index).Trim();
        itemName = columnName.Substring(index + 1).Trim();

        // Strip instance suffix e.g. "F5530021(1)" -> "F5530021"
        int parenIndex = tableName.IndexOf('(');
        if (parenIndex > 0)
        {
            tableName = tableName.Substring(0, parenIndex);
        }

        return tableName.Length > 0 && itemName.Length > 0;
    }

    private static string BuildQualifiedViewColumnName(string? tableName, string? dataItem, int instanceId = 0)
    {
        if (string.IsNullOrWhiteSpace(dataItem))
        {
            return string.Empty;
        }

        if (instanceId > 0)
        {
            return string.IsNullOrWhiteSpace(tableName)
                ? $"{dataItem}({instanceId})"
                : $"{tableName}({instanceId}).{dataItem}";
        }

        return string.IsNullOrWhiteSpace(tableName)
            ? dataItem
            : $"{tableName}.{dataItem}";
    }

    private readonly record struct FilterTarget(string ColumnName, string? TableOverride, JdeColumn? Column);

    private static bool CompareString(string actual, string expected, JdeFilterOperator op)
    {
        return op switch
        {
            JdeFilterOperator.Equals => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            JdeFilterOperator.NotEquals => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            JdeFilterOperator.LessThan => string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase) < 0,
            JdeFilterOperator.GreaterThan => string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase) > 0,
            JdeFilterOperator.LessThanOrEqual => string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase) <= 0,
            JdeFilterOperator.GreaterThanOrEqual => string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase) >= 0,
            JdeFilterOperator.Like => LikeMatch(actual, expected),
            _ => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool LikeMatch(string actual, string expected)
    {
        if (string.IsNullOrEmpty(expected))
        {
            return false;
        }

        if (!expected.Contains('%', StringComparison.Ordinal) && !expected.Contains('*', StringComparison.Ordinal))
        {
            return actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
        }

        string pattern = expected.Replace('*', '%');
        bool startsWithWildcard = pattern.StartsWith('%');
        bool endsWithWildcard = pattern.EndsWith('%');
        string[] parts = pattern.Split('%', StringSplitOptions.None);

        int index = 0;
        int startPart = 0;
        int endPart = parts.Length - 1;

        if (!startsWithWildcard)
        {
            string first = parts[0];
            if (!actual.StartsWith(first, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            index = first.Length;
            startPart = 1;
        }

        if (!endsWithWildcard)
        {
            endPart--;
        }

        for (int i = startPart; i <= endPart; i++)
        {
            string part = parts[i];
            if (part.Length == 0)
            {
                continue;
            }

            int next = actual.IndexOf(part, index, StringComparison.OrdinalIgnoreCase);
            if (next < 0)
            {
                return false;
            }

            index = next + part.Length;
        }

        if (!endsWithWildcard)
        {
            string last = parts[^1];
            if (last.Length == 0)
            {
                return actual.Length == index;
            }

            int next = actual.IndexOf(last, index, StringComparison.OrdinalIgnoreCase);
            return next >= 0 && next + last.Length == actual.Length;
        }

        return true;
    }

    private static IntPtr AllocateMathNumeric(string value)
    {
        var math = MATH_NUMERIC.Create();
        int result = ParseNumericString(ref math, value);
        if (result != 0)
        {
            return Marshal.StringToHGlobalUni(value);
        }

        IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<MATH_NUMERIC>());
        Marshal.StructureToPtr(math, ptr, false);
        return ptr;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_isInitialized)
        {
            if (_ownsUser && _hUser.IsValid)
            {
                JDB_FreeUser(_hUser);
            }

            if (_ownsEnv && _hEnv.IsValid)
            {
                JDB_FreeEnv(_hEnv);
            }
        }

        _disposed = true;
    }

    private bool TryFetchByPrimaryKey(
        HREQUEST hRequest,
        string tableName,
        IReadOnlyList<JdeFilter> filters,
        IReadOnlyDictionary<string, JdeColumn> columnsByName,
        IReadOnlyList<JdeColumn> columns,
        JdeQueryResult result,
        out IntPtr keyBuffer)
    {
        keyBuffer = IntPtr.Zero;
        if (!_options.UseKeyedFetch)
        {
            return false;
        }

        if (filters.Count == 0)
        {
            return false;
        }

        if (filters.Any(filter => filter.Operator != JdeFilterOperator.Equals))
        {
            return false;
        }

        using var specProvider = new SpecTableMetadataService(_hUser, _options);
        if (!specProvider.TryGetPrimaryIndex(tableName, out int indexId, out var keyColumns))
        {
            return false;
        }

        if (keyColumns.Count == 0 || keyColumns.Count != filters.Count)
        {
            return false;
        }

        if (!TryResolveKeyFilters(filters, columnsByName, keyColumns, out var orderedFilters, out var orderedColumns))
        {
            return false;
        }

        IntPtr rowBuffer = IntPtr.Zero;
        TableLayout? layout = TableLayoutLoader.Load(tableName);
        TableLayout? keyLayout = TableLayoutLoader.LoadKeyLayout(tableName, indexId);
        try
        {
            if (layout == null || keyLayout == null)
            {
                if (_options.EnableQueryDebug)
                {
                    _options.WriteLog($"[DEBUG] KeyedFetch {tableName}: missing table or key layout; skipping keyed fetch");
                }
                return false;
            }

            keyBuffer = BuildKeyStructBuffer(tableName, orderedFilters, columnsByName, layout, keyLayout);
            if (keyBuffer == IntPtr.Zero)
            {
                return false;
            }

            int rowBufferSize = layout.Size + 64;
            if (_options.EnableQueryDebug)
            {
                _options.WriteLog($"[DEBUG] KeyedFetch {tableName}: indexId={indexId}, keys={keyColumns.Count}, rowSize={layout.Size}, keySize={keyLayout.Size}, rowBuffer={rowBufferSize}");
            }

            int selectResult = JDB_SelectKeyed(hRequest, new ID(indexId), keyBuffer, (short)keyColumns.Count);
            if (selectResult != JDEDB_PASSED)
            {
                if (_options.EnableQueryDebug)
                {
                    _options.WriteLog($"[DEBUG] KeyedFetch {tableName}: JDB_SelectKeyed failed (result={selectResult})");
                    if (JDB_GetLastDBError(hRequest, out int errorNum) == JDEDB_PASSED)
                    {
                        _options.WriteLog($"[DEBUG] KeyedFetch {tableName}: JDB_SelectKeyed error={errorNum}");
                    }
                }
                return false;
            }

            rowBuffer = Marshal.AllocHGlobal(rowBufferSize);
            int fetchResult = JDB_Fetch(hRequest, rowBuffer, 0);
            if (fetchResult == JDEDB_NO_MORE_DATA)
            {
                if (_options.EnableQueryDebug)
                {
                    _options.WriteLog($"[DEBUG] KeyedFetch {tableName}: no data");
                }
                return true;
            }

            if (fetchResult != JDEDB_PASSED)
            {
                if (_options.EnableQueryDebug)
                {
                    _options.WriteLog($"[DEBUG] KeyedFetch {tableName}: JDB_Fetch failed (result={fetchResult})");
                    if (JDB_GetLastDBError(hRequest, out int errorNum) == JDEDB_PASSED)
                    {
                        _options.WriteLog($"[DEBUG] KeyedFetch {tableName}: JDB_Fetch error={errorNum}");
                    }
                }
                return false;
            }

            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns)
            {
                object? value = ReadValueFromLayout(layout, rowBuffer, column);
                row[column.Name] = value ?? string.Empty;
            }

            result.Rows.Add(row);
            return true;
        }
        finally
        {
            if (rowBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(rowBuffer);
            }

        }
    }

    private static bool TryResolveKeyFilters(
        IReadOnlyList<JdeFilter> filters,
        IReadOnlyDictionary<string, JdeColumn> columnsByName,
        IReadOnlyList<string> keyColumns,
        out List<JdeFilter> orderedFilters,
        out List<JdeColumn?> orderedColumns)
    {
        orderedFilters = new List<JdeFilter>(keyColumns.Count);
        orderedColumns = new List<JdeColumn?>(keyColumns.Count);

        var filtersByKey = new Dictionary<string, JdeFilter>(StringComparer.OrdinalIgnoreCase);
        var columnsByKey = new Dictionary<string, JdeColumn?>(StringComparer.OrdinalIgnoreCase);

        foreach (var filter in filters)
        {
            if (!columnsByName.TryGetValue(filter.ColumnName, out var column))
            {
                column = null;
            }

            string key = column?.DataDictionaryItem ?? column?.Name ?? filter.ColumnName;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (!filtersByKey.TryAdd(key, filter))
            {
                return false;
            }

            columnsByKey[key] = column;
        }

        foreach (string keyColumn in keyColumns)
        {
            if (!filtersByKey.TryGetValue(keyColumn, out var filter))
            {
                return false;
            }

            columnsByKey.TryGetValue(keyColumn, out var column);
            orderedFilters.Add(filter);
            orderedColumns.Add(column);
        }

        return true;
    }

    private IntPtr BuildKeyStructBuffer(
        string tableName,
        IReadOnlyList<JdeFilter> filters,
        IReadOnlyDictionary<string, JdeColumn> columnsByName,
        TableLayout tableLayout,
        TableLayout keyLayout)
    {
        IntPtr buffer = Marshal.AllocHGlobal(keyLayout.Size);
        var zero = new byte[keyLayout.Size];
        Marshal.Copy(zero, 0, buffer, zero.Length);

        for (int i = 0; i < filters.Count; i++)
        {
            var filter = filters[i];
            if (!tableLayout.TryGetFieldByColumn(filter.ColumnName, out var tableField))
            {
                if (_options.EnableQueryDebug)
                {
                    _options.WriteLog($"[DEBUG] KeyedFetch {tableName}: table field not found for column {filter.ColumnName}");
                }
                Marshal.FreeHGlobal(buffer);
                return IntPtr.Zero;
            }

            if (!keyLayout.TryGetField(tableField.Name, out var keyField))
            {
                if (_options.EnableQueryDebug)
                {
                    _options.WriteLog($"[DEBUG] KeyedFetch {tableName}: key field not found for {tableField.Name}");
                }
                Marshal.FreeHGlobal(buffer);
                return IntPtr.Zero;
            }

            columnsByName.TryGetValue(filter.ColumnName, out var column);
            WriteKeyValue(buffer, keyField, column, filter.Value);
        }

        return buffer;
    }

    private static void WriteKeyValue(IntPtr buffer, TableField keyField, JdeColumn? column, string value)
    {
        switch (keyField.Type)
        {
            case TableFieldType.MathNumeric:
            {
                IntPtr mathPtr = AllocateMathNumeric(value);
                try
                {
                    var bytes = new byte[keyField.Length];
                    Marshal.Copy(mathPtr, bytes, 0, bytes.Length);
                    Marshal.Copy(bytes, 0, IntPtr.Add(buffer, keyField.Offset), bytes.Length);
                }
                finally
                {
                    Marshal.FreeHGlobal(mathPtr);
                }
                break;
            }
            case TableFieldType.JCharArray:
            case TableFieldType.JCharSingle:
            {
                int length = keyField.Type == TableFieldType.JCharSingle ? 1 : keyField.Length;
                string trimmed = value ?? string.Empty;
                if (trimmed.Length > length)
                {
                    trimmed = trimmed.Substring(0, length);
                }

                byte[] bytes = new byte[length * 2];
                int written = Encoding.Unicode.GetBytes(trimmed, 0, trimmed.Length, bytes, 0);
                if (written < bytes.Length)
                {
                    Array.Clear(bytes, written, bytes.Length - written);
                }
                Marshal.Copy(bytes, 0, IntPtr.Add(buffer, keyField.Offset), bytes.Length);
                break;
            }
            case TableFieldType.Id:
            {
                if (!int.TryParse(value, out int idValue))
                {
                    idValue = 0;
                }
                Marshal.WriteInt32(IntPtr.Add(buffer, keyField.Offset), idValue);
                break;
            }
            case TableFieldType.JdeDate:
            {
                if (DateTime.TryParse(value, out var date))
                {
                    Marshal.WriteInt16(IntPtr.Add(buffer, keyField.Offset), (short)date.Year);
                    Marshal.WriteInt16(IntPtr.Add(buffer, keyField.Offset + 2), (short)date.Month);
                    Marshal.WriteInt16(IntPtr.Add(buffer, keyField.Offset + 4), (short)date.Day);
                }
                break;
            }
        }
    }
}
