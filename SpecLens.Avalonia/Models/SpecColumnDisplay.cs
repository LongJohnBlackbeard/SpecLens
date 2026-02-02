namespace SpecLens.Avalonia.Models;

public sealed class SpecColumnDisplay
{
    public int Sequence { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string SourceTable { get; set; } = string.Empty;
    public string SqlColumnName { get; set; } = string.Empty;
}
