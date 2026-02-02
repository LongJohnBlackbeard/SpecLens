namespace JdeClient.Core.Models;

/// <summary>
/// Represents a node in the event rules navigation tree.
/// </summary>
public sealed class JdeEventRulesNode
{
    /// <summary>
    /// Stable identifier for the node.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Node type (object, function, section, etc.).
    /// </summary>
    public JdeEventRulesNodeType NodeType { get; set; }

    /// <summary>
    /// Event spec key (EVSK) associated with this node.
    /// </summary>
    public string? EventSpecKey { get; set; }

    /// <summary>
    /// Data structure name, when applicable.
    /// </summary>
    public string? DataStructureName { get; set; }

    /// <summary>
    /// Child nodes.
    /// </summary>
    public IReadOnlyList<JdeEventRulesNode> Children { get; set; } = Array.Empty<JdeEventRulesNode>();

    /// <summary>
    /// UI expansion hint.
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Whether this node has event rules.
    /// </summary>
    public bool HasEventRules => !string.IsNullOrWhiteSpace(EventSpecKey);
}

/// <summary>
/// Event rules tree node types.
/// </summary>
public enum JdeEventRulesNodeType
{
    Object,
    Function,
    Section,
    Form,
    Control,
    Event,
    Unknown
}
