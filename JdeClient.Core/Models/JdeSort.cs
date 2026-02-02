namespace JdeClient.Core.Models;

/// <summary>
/// Represents a sort directive for JDE table or view queries.
/// </summary>
public sealed class JdeSort
{
    /// <summary>
    /// Column name to sort by.
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Sort direction.
    /// </summary>
    public JdeSortDirection Direction { get; set; } = JdeSortDirection.Ascending;
}

/// <summary>
/// Sort direction for JDE queries.
/// </summary>
public enum JdeSortDirection
{
    Ascending,
    Descending
}
