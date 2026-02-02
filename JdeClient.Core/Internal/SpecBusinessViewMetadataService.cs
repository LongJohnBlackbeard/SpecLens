using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using static JdeClient.Core.Interop.JdeKernelApi;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Loads business view metadata from JDE spec structures.
/// </summary>
internal sealed class SpecBusinessViewMetadataService : IDisposable
{
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
    public JdeBusinessViewInfo? GetBusinessViewInfo(string viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName))
        {
            return null;
        }

        IntPtr bobPtr = IntPtr.Zero;
        try
        {
            int result = JDBRS_GetBOBSpecs(_hUser, new NID(viewName), out bobPtr, (char)1, IntPtr.Zero);
            if (DebugEnabled)
            {
                _options.WriteLog($"[DEBUG] JDBRS_GetBOBSpecs({viewName}) result={result}, bobPtr=0x{bobPtr.ToInt64():X}");
            }

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
                JDBRS_FreeBOBSpecs(bobPtr);
            }
        }
    }

    private static void ReadTables(IntPtr tablesPtr, ushort tableCount, List<JdeBusinessViewTable> tables)
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

    private static void ReadColumns(IntPtr columnsPtr, ushort columnCount, List<JdeBusinessViewColumn> columns)
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

    private static void ReadJoins(IntPtr joinsPtr, ushort joinCount, List<JdeBusinessViewJoin> joins)
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
