namespace JdeClient.Core.Models;

/// <summary>
/// Represents a logical event rules document composed of decoded lines.
/// </summary>
public sealed class JdeEventRulesDocument
{
    /// <summary>
    /// Display title for the document.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Data structure associated with the document.
    /// </summary>
    public string? DataStructureName { get; set; }

    /// <summary>
    /// Decoded event rule lines.
    /// </summary>
    public IReadOnlyList<JdeEventRuleLine> Lines { get; set; } = Array.Empty<JdeEventRuleLine>();
}
