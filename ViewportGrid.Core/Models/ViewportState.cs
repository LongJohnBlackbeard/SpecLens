namespace ViewportGrid.Core.Models;

public sealed record ViewportState
{
    public required int FirstVisibleRow { get; init; }
    public required int VisibleRowCount { get; init; }
    public required int FirstVisibleColumn { get; init; }
    public required int VisibleColumnCount { get; init; }
    public required int FrozenColumnCount { get; init; }
    public required double HorizontalOffset { get; init; }
    public required double VerticalOffset { get; init; }
    public required double ViewportWidth { get; init; }
    public required double ViewportHeight { get; init; }
}
