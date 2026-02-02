namespace JdeClient.Core.Models;

/// <summary>
/// Represents a decoded event rule line.
/// </summary>
public sealed class JdeEventRuleLine
{
    /// <summary>
    /// Sequence in the source spec stream.
    /// </summary>
    public int Sequence { get; set; }

    /// <summary>
    /// Raw record type identifier.
    /// </summary>
    public short RecordType { get; set; }

    /// <summary>
    /// Line text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Indentation level inferred for formatting.
    /// </summary>
    public int IndentLevel { get; set; }
}
