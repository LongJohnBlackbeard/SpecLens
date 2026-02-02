namespace JdeClient.Core.Models;

/// <summary>
/// Represents a DDTEXT record for a data dictionary item.
/// </summary>
public sealed class JdeDataDictionaryText
{
    /// <summary>
    /// Data dictionary item (DTAI).
    /// </summary>
    public string DataItem { get; set; } = string.Empty;

    /// <summary>
    /// Text type (e.g., glossary, title).
    /// </summary>
    public char TextType { get; set; }

    /// <summary>
    /// Language code associated with the text.
    /// </summary>
    public string? Language { get; set; }
    public string? SystemCode { get; set; }
    public string? DictionaryName { get; set; }
    public uint VarLength { get; set; }
    public int FormatNumber { get; set; }
    public string? Text { get; set; }
}
