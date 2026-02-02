namespace JdeClient.Core.Models;

/// <summary>
/// Represents an XML fragment for event rules (GBRSPEC).
/// </summary>
public sealed class JdeEventRulesXmlDocument
{
    /// <summary>
    /// Event spec key that the XML was derived from.
    /// </summary>
    public string EventSpecKey { get; set; } = string.Empty;

    /// <summary>
    /// XML payload.
    /// </summary>
    public string Xml { get; set; } = string.Empty;

    /// <summary>
    /// Source record count used to build the XML.
    /// </summary>
    public int RecordCount { get; set; }
}
