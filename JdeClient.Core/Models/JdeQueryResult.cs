namespace JdeClient.Core.Models;

/// <summary>
/// Represents the result of a JDE table query
/// </summary>
public class                                                                                                                                                                                  JdeQueryResult
{
    /// <summary>
    /// Table name that was queried
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// List of rows returned
    /// Each row is a dictionary of column name → value
    /// </summary>
    public List<Dictionary<string, object>> Rows { get; set; } = new();

    /// <summary>
    /// Column names in result set
    /// </summary>
    public List<string> ColumnNames { get; set; } = new();

    /// <summary>
    /// Total number of rows returned
    /// </summary>
    public int RowCount => Rows.Count;

    /// <summary>
    /// Whether the query was truncated due to row limit
    /// </summary>
    public bool IsTruncated { get; set; }

    /// <summary>
    /// Maximum rows that were allowed
    /// </summary>
    public int? MaxRows { get; set; }

    public override string ToString() => $"{TableName}: {RowCount} rows";
}