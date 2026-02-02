using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ViewportGrid.Core.Interfaces;
using ViewportGrid.Core.Models;

namespace ViewportGrid.Data.Providers;

public sealed class InMemoryGridDataProvider : IGridDataProvider
{
    private readonly object _sync = new();
    private readonly List<object?[]> _rows = new();
    private readonly Dictionary<string, int> _dataIndexByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<int> _displayToDataIndex = new();
    private IReadOnlyList<string> _columns = Array.Empty<string>();

    public int TotalRowCount
    {
        get
        {
            lock (_sync)
            {
                return _rows.Count;
            }
        }
    }

    public int TotalColumnCount
    {
        get
        {
            lock (_sync)
            {
                return _displayToDataIndex.Count;
            }
        }
    }

    public void Reset(IReadOnlyList<string> columns)
    {
        if (columns == null)
        {
            throw new ArgumentNullException(nameof(columns));
        }

        lock (_sync)
        {
            var snapshot = new string[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                snapshot[i] = columns[i];
            }

            _columns = snapshot;
            _rows.Clear();
            _dataIndexByName.Clear();
            _displayToDataIndex.Clear();
            for (int i = 0; i < snapshot.Length; i++)
            {
                _dataIndexByName[snapshot[i]] = i;
                _displayToDataIndex.Add(i);
            }
        }
    }

    public void SetDisplayOrder(IReadOnlyList<string> displayColumns)
    {
        if (displayColumns == null)
        {
            throw new ArgumentNullException(nameof(displayColumns));
        }

        lock (_sync)
        {
            _displayToDataIndex.Clear();
            var seen = new HashSet<int>();
            foreach (var name in displayColumns)
            {
                if (_dataIndexByName.TryGetValue(name, out var dataIndex) && seen.Add(dataIndex))
                {
                    _displayToDataIndex.Add(dataIndex);
                }
            }

            for (int i = 0; i < _columns.Count; i++)
            {
                if (seen.Add(i))
                {
                    _displayToDataIndex.Add(i);
                }
            }
        }
    }

    public void AppendRow(object?[] values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        lock (_sync)
        {
            _rows.Add(values);
        }
    }

    public Task<CellBlock> FetchBlockAsync(
        int startRow,
        int rowCount,
        int startColumn,
        int columnCount,
        CancellationToken ct = default)
    {
        if (rowCount <= 0 || columnCount <= 0)
        {
            return Task.FromResult(new CellBlock
            {
                StartRow = startRow,
                StartColumn = startColumn,
                RowCount = 0,
                ColumnCount = 0,
                Data = new object?[0, 0]
            });
        }

        var data = new object?[rowCount, columnCount];
        lock (_sync)
        {
            for (int rowOffset = 0; rowOffset < rowCount; rowOffset++)
            {
                int rowIndex = startRow + rowOffset;
                if (rowIndex < 0 || rowIndex >= _rows.Count)
                {
                    break;
                }

                var row = _rows[rowIndex];
                for (int columnOffset = 0; columnOffset < columnCount; columnOffset++)
                {
                    int displayIndex = startColumn + columnOffset;
                    if (displayIndex < 0 || displayIndex >= _displayToDataIndex.Count)
                    {
                        continue;
                    }

                    int dataIndex = _displayToDataIndex[displayIndex];
                    if (dataIndex < 0 || dataIndex >= row.Length)
                    {
                        continue;
                    }

                    data[rowOffset, columnOffset] = row[dataIndex];
                }
            }
        }

        return Task.FromResult(new CellBlock
        {
            StartRow = startRow,
            StartColumn = startColumn,
            RowCount = rowCount,
            ColumnCount = columnCount,
            Data = data
        });
    }
}
