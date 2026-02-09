using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JdeClient.Core.Exceptions;
using JdeClient.Core;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using static JdeClient.Core.Interop.JdeKernelApi;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Loads table metadata (columns, indexes, dictionary) from JDE spec structures.
/// </summary>
internal sealed class SpecTableMetadataService : IDisposable
{
    private readonly HUSER _hUser;
    private readonly JdeClientOptions _options;
    private bool _disposed;
    private bool DebugEnabled => _options.EnableSpecDebug;

    public SpecTableMetadataService(HUSER hUser, JdeClientOptions options)
    {
        _hUser = hUser;
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Retrieve column metadata for a table.
    /// </summary>
    public List<JdeColumn> GetColumns(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return new List<JdeColumn>();
        }

        IntPtr tablePtr = IntPtr.Zero;
        try
        {
            int result = JDBRS_GetTableSpecsByName(_hUser, tableName, out tablePtr, (char)1, IntPtr.Zero);
            if (DebugEnabled)
            {
                _options.WriteLog($"[DEBUG] JDBRS_GetTableSpecsByName({tableName}) result={result}, tablePtr=0x{tablePtr.ToInt64():X}");
            }
            if (result != JDEDB_PASSED || tablePtr == IntPtr.Zero)
            {
                return new List<JdeColumn>();
            }

            if (!TryReadHeader(tablePtr, tableName, out var header))
            {
                if (DebugEnabled)
                {
                    _options.WriteLog($"[DEBUG] TableSpecs {tableName}: no columns available (count=0)");
                }
                return new List<JdeColumn>();
            }
            return ReadColumns(header, tableName);
        }
        finally
        {
            if (tablePtr != IntPtr.Zero)
            {
                JDBRS_FreeTableSpecs(tablePtr);
            }
        }
    }

    /// <summary>
    /// Try to resolve the primary index and its key columns for a table.
    /// </summary>
    public bool TryGetPrimaryIndex(string tableName, out int indexId, out List<string> keyColumns)
    {
        indexId = 0;
        keyColumns = new List<string>();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            return false;
        }

        IntPtr tablePtr = IntPtr.Zero;
        try
        {
            int result = JDBRS_GetTableSpecsByName(_hUser, tableName, out tablePtr, (char)1, IntPtr.Zero);
            if (result != JDEDB_PASSED || tablePtr == IntPtr.Zero)
            {
                return false;
            }

            if (!TryReadHeader(tablePtr, tableName, out var header))
            {
                return false;
            }

            int indexCount = header.NumIndex;
            if (header.IndexPtr == IntPtr.Zero || indexCount <= 0)
            {
                return false;
            }

            var indexes = ReadIndexes(header.IndexPtr, indexCount);
            return TryGetPrimaryIndexFromIndexes(indexes, out indexId, out keyColumns);
        }
        finally
        {
            if (tablePtr != IntPtr.Zero)
            {
                JDBRS_FreeTableSpecs(tablePtr);
            }
        }
    }

    /// <summary>
    /// Retrieve index metadata for a table.
    /// </summary>
    public List<JdeIndexInfo> GetIndexes(string tableName)
    {
        var indexes = new List<JdeIndexInfo>();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            return indexes;
        }

        IntPtr tablePtr = IntPtr.Zero;
        try
        {
            int result = JDBRS_GetTableSpecsByName(_hUser, tableName, out tablePtr, (char)1, IntPtr.Zero);
            if (result != JDEDB_PASSED || tablePtr == IntPtr.Zero)
            {
                return indexes;
            }

            if (!TryReadHeader(tablePtr, tableName, out var header))
            {
                return indexes;
            }

            int indexCount = header.NumIndex;
            if (header.IndexPtr == IntPtr.Zero || indexCount <= 0)
            {
                return indexes;
            }

            var indexEntries = ReadIndexes(header.IndexPtr, indexCount);
            return BuildIndexInfos(indexEntries);
        }
        finally
        {
            if (tablePtr != IntPtr.Zero)
            {
                JDBRS_FreeTableSpecs(tablePtr);
            }
        }
    }

    private List<JdeColumn> ReadColumns(TableSpecHeader header, string tableName)
    {
        int columnCount = header.NumCols;
        var columns = new List<JdeColumn>(columnCount);
        int globalColSize = header.UseNativeLayout ? Marshal.SizeOf<GLOBALCOLS_NATIVE>() : Marshal.SizeOf<GLOBALCOLS>();

        for (int i = 0; i < columnCount; i++)
        {
            var column = ReadColumn(header, tableName, globalColSize, i);
            if (column != null)
            {
                columns.Add(column);
            }
        }

        return columns;
    }

    private JdeColumn? ReadColumn(TableSpecHeader header, string tableName, int globalColSize, int index)
    {
        IntPtr colPtr = IntPtr.Add(header.ColumnsPtr, index * globalColSize);
        string sqlName;
        IntPtr colCachePtr;

        if (header.UseNativeLayout)
        {
            var globalCol = Marshal.PtrToStructure<GLOBALCOLS_NATIVE>(colPtr);
            sqlName = Normalize(globalCol.szSQLName);
            colCachePtr = globalCol.lpColumn;
        }
        else
        {
            var globalCol = Marshal.PtrToStructure<GLOBALCOLS>(colPtr);
            sqlName = Normalize(globalCol.szSQLName);
            colCachePtr = globalCol.lpColumn;
        }

        if (DebugEnabled && index < 3)
        {
            _options.WriteLog($"[DEBUG] TableSpecs {tableName}: column[{index}] sqlName={sqlName}, lpColumn=0x{colCachePtr.ToInt64():X}");
        }

        string dictItem = string.Empty;
        int evdType = 0;
        int length = 0;
        int decimals = 0;

        if (colCachePtr != IntPtr.Zero)
        {
            var cache = ReadColumnCache(colCachePtr, header.UseNativeLayout);
            dictItem = cache.DictItem;
            evdType = cache.EvdType;
            length = cache.Length;
            decimals = cache.Decimals;
        }
        else if (DebugEnabled && index < 3)
        {
            _options.WriteLog($"[DEBUG] TableSpecs {tableName}: column[{index}] has null lpColumn, sqlName={sqlName}");
        }

        return CreateColumn(sqlName, dictItem, evdType, length, decimals);
    }

    internal static ColumnCache ReadColumnCache(IntPtr colCachePtr, bool useNativeLayout)
    {
        if (useNativeLayout)
        {
            var colCache = Marshal.PtrToStructure<COLUMNCACHE_HEADER_NATIVE>(colCachePtr);
            return new ColumnCache(
                Normalize(colCache.szDict.Value),
                colCache.idEverestType,
                (int)colCache.nLength,
                colCache.nDecimals);
        }

        var packed = Marshal.PtrToStructure<COLUMNCACHE_HEADER>(colCachePtr);
        return new ColumnCache(
            Normalize(packed.szDict.Value),
            packed.idEverestType,
            (int)packed.nLength,
            packed.nDecimals);
    }

    internal readonly struct ColumnCache
    {
        public ColumnCache(string dictItem, int evdType, int length, int decimals)
        {
            DictItem = dictItem;
            EvdType = evdType;
            Length = length;
            Decimals = decimals;
        }

        public string DictItem { get; }
        public int EvdType { get; }
        public int Length { get; }
        public int Decimals { get; }
    }

    internal readonly struct TableSpecHeader
    {
        public TableSpecHeader(ushort numCols, ushort numIndex, IntPtr columnsPtr, IntPtr indexPtr, bool useNativeLayout)
        {
            NumCols = numCols;
            NumIndex = numIndex;
            ColumnsPtr = columnsPtr;
            IndexPtr = indexPtr;
            UseNativeLayout = useNativeLayout;
        }

        public ushort NumCols { get; }
        public ushort NumIndex { get; }
        public IntPtr ColumnsPtr { get; }
        public IntPtr IndexPtr { get; }
        public bool UseNativeLayout { get; }
    }

    internal bool TryReadHeader(IntPtr tablePtr, string tableName, out TableSpecHeader header)
    {
        var packed = Marshal.PtrToStructure<TABLECACHE_HEADER>(tablePtr);
        if (DebugEnabled)
        {
            _options.WriteLog($"[DEBUG] TableSpecs {tableName} (pack1): nNumCols={packed.nNumCols}, nNumIndex={packed.nNumIndex}, lpColumns=0x{packed.lpColumns.ToInt64():X}, lpGlobalIndex=0x{packed.lpGlobalIndex.ToInt64():X}");
        }

        if (IsHeaderValid(packed))
        {
            header = new TableSpecHeader(packed.nNumCols, packed.nNumIndex, packed.lpColumns, packed.lpGlobalIndex, false);
            return true;
        }

        var native = Marshal.PtrToStructure<TABLECACHE_HEADER_NATIVE>(tablePtr);
        if (DebugEnabled)
        {
            _options.WriteLog($"[DEBUG] TableSpecs {tableName} (pack4): nNumCols={native.nNumCols}, nNumIndex={native.nNumIndex}, lpColumns=0x{native.lpColumns.ToInt64():X}, lpGlobalIndex=0x{native.lpGlobalIndex.ToInt64():X}");
        }

        if (IsHeaderValid(native))
        {
            header = new TableSpecHeader(native.nNumCols, native.nNumIndex, native.lpColumns, native.lpGlobalIndex, true);
            return true;
        }

        header = default;
        return false;
    }

    internal static bool IsHeaderValid(TABLECACHE_HEADER header)
    {
        return header.nNumCols > 0 && header.lpColumns != IntPtr.Zero;
    }

    internal static bool IsHeaderValid(TABLECACHE_HEADER_NATIVE header)
    {
        return header.nNumCols > 0 && header.lpColumns != IntPtr.Zero;
    }

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.TrimEnd('\0', ' ').Trim();
    }

    internal static JdeColumn? CreateColumn(string sqlName, string dictItem, int evdType, int length, int decimals)
    {
        string columnName = !string.IsNullOrWhiteSpace(dictItem) ? dictItem : sqlName;

        if (string.IsNullOrWhiteSpace(columnName))
        {
            return null;
        }

        return new JdeColumn
        {
            Name = columnName,
            DataDictionaryItem = dictItem,
            SqlName = sqlName,
            DataType = evdType,
            Length = length,
            Decimals = decimals
        };
    }

    internal static bool TryGetPrimaryIndexFromIndexes(
        IReadOnlyList<GLOBALINDEX> indexes,
        out int indexId,
        out List<string> keyColumns)
    {
        indexId = 0;
        keyColumns = new List<string>();

        foreach (var index in indexes)
        {
            if (index.nPrimary == 0 || index.nNumCols == 0)
            {
                continue;
            }

            indexId = index.idIndex.Value;
            if (index.lpGlobalIndexDetail == null || index.lpGlobalIndexDetail.Length == 0)
            {
                return false;
            }

            int keyCount = index.nNumCols;
            for (int keyIndex = 0; keyIndex < keyCount && keyIndex < index.lpGlobalIndexDetail.Length; keyIndex++)
            {
                string keyName = Normalize(index.lpGlobalIndexDetail[keyIndex].szDict.Value);
                if (!string.IsNullOrWhiteSpace(keyName))
                {
                    keyColumns.Add(keyName);
                }
            }

            return keyColumns.Count > 0;
        }

        return false;
    }

    internal static List<JdeIndexInfo> BuildIndexInfos(IReadOnlyList<GLOBALINDEX> indexes)
    {
        var results = new List<JdeIndexInfo>();

        foreach (var index in indexes)
        {
            int keyCount = index.nNumCols;
            if (keyCount <= 0)
            {
                continue;
            }

            var keyColumns = new List<string>();
            if (index.lpGlobalIndexDetail != null && index.lpGlobalIndexDetail.Length > 0)
            {
                for (int keyIndex = 0; keyIndex < keyCount && keyIndex < index.lpGlobalIndexDetail.Length; keyIndex++)
                {
                    string keyName = Normalize(index.lpGlobalIndexDetail[keyIndex].szDict.Value);
                    if (!string.IsNullOrWhiteSpace(keyName))
                    {
                        keyColumns.Add(keyName);
                    }
                }
            }

            if (keyColumns.Count == 0)
            {
                continue;
            }

            string name = Normalize(index.szIndexName);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Index {index.idIndex.Value}";
            }

            results.Add(new JdeIndexInfo
            {
                Id = index.idIndex.Value,
                Name = name,
                IsPrimary = index.nPrimary != 0,
                KeyColumns = keyColumns
            });
        }

        return results;
    }

    private static List<GLOBALINDEX> ReadIndexes(IntPtr indexPtr, int indexCount)
    {
        var indexes = new List<GLOBALINDEX>(indexCount);
        int indexSize = Marshal.SizeOf<GLOBALINDEX>();
        for (int i = 0; i < indexCount; i++)
        {
            IntPtr currentPtr = IntPtr.Add(indexPtr, i * indexSize);
            indexes.Add(Marshal.PtrToStructure<GLOBALINDEX>(currentPtr));
        }

        return indexes;
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
