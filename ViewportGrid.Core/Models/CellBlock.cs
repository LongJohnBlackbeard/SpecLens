using System;

namespace ViewportGrid.Core.Models;

public sealed class CellBlock
{
    public required int StartRow { get; init; }
    public required int StartColumn { get; init; }
    public required int RowCount { get; init; }
    public required int ColumnCount { get; init; }
    public required object?[,] Data { get; init; }

    public object? GetCell(int row, int column)
    {
        int rowOffset = row - StartRow;
        int columnOffset = column - StartColumn;
        if (rowOffset < 0 || rowOffset >= RowCount)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        if (columnOffset < 0 || columnOffset >= ColumnCount)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        return Data[rowOffset, columnOffset];
    }
}
