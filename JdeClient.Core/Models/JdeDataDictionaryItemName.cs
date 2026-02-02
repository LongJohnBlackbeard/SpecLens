namespace JdeClient.Core.Models;

/// <summary>
/// Represents the friendly name for a data dictionary item.
/// </summary>
public sealed class JdeDataDictionaryItemName
{
    /// <summary>
    /// Data dictionary item (DTAI).
    /// </summary>
    public string DataItem { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the data item.
    /// </summary>
    public string? Name { get; set; }
}
