namespace JdeClient.Core.XmlEngine.Models;

/// <summary>
/// Represents an event-level variable declared in event rule XML.
/// </summary>
public class EventLevelVariable
{
    /// <summary>
    /// Variable alias (e.g., data dictionary alias).
    /// </summary>
    public required string Alias { get; set; }

    /// <summary>
    /// Variable name.
    /// </summary>
    public required string VariableName { get; set; }

    /// <summary>
    /// Variable identifier.
    /// </summary>
    public required string VariableId { get; set; }
}
