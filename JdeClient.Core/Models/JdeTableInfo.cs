namespace JdeClient.Core.Models;

/// <summary>
/// Represents JDE table metadata and structure information
/// </summary>
public class JdeTableInfo
{
    /// <summary>
    /// Table name (e.g., "F0101", "F4211")
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of columns in the table
    /// </summary>
    public List<JdeColumn> Columns { get; set; } = new();

    /// <summary>
    /// System code (e.g., "01" for Address Book)
    /// </summary>
    public string? SystemCode { get; set; }

    /// <summary>
    /// Product code
    /// </summary>
    public string? ProductCode { get; set; }

    public override string ToString() => $"{TableName} ({Columns.Count} columns)";
}