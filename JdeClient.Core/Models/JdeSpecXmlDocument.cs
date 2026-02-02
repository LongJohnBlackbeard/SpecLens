namespace JdeClient.Core.Models;

/// <summary>
/// Represents an XML fragment for a spec record (e.g., DSTMPL).
/// </summary>
public sealed class JdeSpecXmlDocument
{
    /// <summary>
    /// Spec key that the XML was derived from.
    /// </summary>
    public string SpecKey { get; set; } = string.Empty;

    /// <summary>
    /// XML payload.
    /// </summary>
    public string Xml { get; set; } = string.Empty;

    /// <summary>
    /// Source record count used to build the XML.
    /// </summary>
    public int RecordCount { get; set; }
}
