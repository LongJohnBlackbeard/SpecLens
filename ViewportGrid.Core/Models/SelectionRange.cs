namespace ViewportGrid.Core.Models;

public sealed record SelectionRange
{
    public required int StartRow { get; init; }
    public required int EndRow { get; init; }
    public required int StartColumn { get; init; }
    public required int EndColumn { get; init; }

    public bool Contains(int row, int column)
    {
        return row >= StartRow && row <= EndRow
            && column >= StartColumn && column <= EndColumn;
    }

    public bool IntersectsViewport(ViewportState viewport)
    {
        int lastVisibleRow = viewport.FirstVisibleRow + viewport.VisibleRowCount - 1;
        int lastVisibleColumn = viewport.FirstVisibleColumn + viewport.VisibleColumnCount - 1;

        bool rowOverlap = StartRow <= lastVisibleRow && EndRow >= viewport.FirstVisibleRow;
        bool columnOverlap = StartColumn <= lastVisibleColumn && EndColumn >= viewport.FirstVisibleColumn;
        return rowOverlap && columnOverlap;
    }
}
