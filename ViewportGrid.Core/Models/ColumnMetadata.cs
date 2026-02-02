namespace ViewportGrid.Core.Models;

public sealed record ColumnMetadata
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required double Width { get; init; }
    public required int DisplayIndex { get; init; }
    public required bool IsFrozen { get; init; }
}
