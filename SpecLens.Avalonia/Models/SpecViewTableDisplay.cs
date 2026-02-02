namespace SpecLens.Avalonia.Models;

public sealed class SpecViewTableDisplay
{
    public string TableName { get; set; } = string.Empty;
    public int InstanceCount { get; set; }
    public int PrimaryIndexId { get; set; }
}
