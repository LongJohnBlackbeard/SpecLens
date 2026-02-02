namespace JdeClient.Core.Models;

/// <summary>
/// Represents formatted event rules output and related metadata.
/// </summary>
public sealed class JdeEventRulesFormattedResult
{
    /// <summary>
    /// Formatted text payload.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Status message describing the formatting outcome.
    /// </summary>
    public string StatusMessage { get; init; } = string.Empty;

    /// <summary>
    /// Data structure template name used for formatting.
    /// </summary>
    public string? TemplateName { get; init; }

    /// <summary>
    /// Event spec key used for formatting.
    /// </summary>
    public string? EventSpecKey { get; init; }
}
