namespace JdeClient.Core.Models;

/// <summary>
/// Represents a JDE table column with metadata
/// </summary>
public class JdeColumn
{
    /// <summary>
    /// Column name (e.g., "ABAN8", "ABALPH")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Data type code
    /// </summary>
    public int DataType { get; set; }

    /// <summary>
    /// Column length
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Number of decimal places (for numeric types)
    /// </summary>
    public int Decimals { get; set; }

    /// <summary>
    /// User-friendly description of column
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Data dictionary item reference
    /// </summary>
    public string? DataDictionaryItem { get; set; }

    /// <summary>
    /// Physical SQL column name when it differs from the data dictionary item
    /// </summary>
    public string? SqlName { get; set; }

    /// <summary>
    /// Source table name when a column originates from a business view.
    /// </summary>
    public string? SourceTable { get; set; }

    /// <summary>
    /// Instance id for business view columns (used to disambiguate joins).
    /// </summary>
    public int? InstanceId { get; set; }

    public override string ToString() => $"{Name} ({DataType}, {Length})";
}
