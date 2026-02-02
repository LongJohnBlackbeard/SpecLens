namespace JdeClient.Core.Models;

/// <summary>
/// Represents a table index and its key columns.
/// </summary>
public sealed class JdeIndexInfo
{
    /// <summary>
    /// Index identifier from specs.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Index name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this index is the primary index.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Key column names in index order.
    /// </summary>
    public List<string> KeyColumns { get; set; } = new();

    /// <summary>
    /// Display name for UI or logging.
    /// </summary>
    public string DisplayName => IsPrimary ? $"{Name} (Primary)" : Name;
}
