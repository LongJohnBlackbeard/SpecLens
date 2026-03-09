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
    /// Version key associated with this node for APPL/UBE/TBLE event rules.
    /// </summary>
    public string? VersionName { get; set; }

    /// <summary>
    /// Form or section key associated with this node for APPL/UBE/TBLE event rules.
    /// </summary>
    public string? FormOrSectionName { get; set; }

    /// <summary>
    /// Control/component identifier associated with this node for APPL/UBE/TBLE event rules.
    /// </summary>
    public int? ControlId { get; set; }

    /// <summary>
    /// Numeric event identifier associated with this node for APPL/UBE/TBLE event rules.
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// Secondary event identifier when applicable.
    /// </summary>
    public int? EventId3 { get; set; }

    /// <summary>
    /// Resolved component text identifier when available (FDA/RDA).
    /// </summary>
    public int? TextId { get; set; }

    /// <summary>
    /// Resolved component type label when available (for example "Push Button").
    /// </summary>
    public string? ComponentTypeName { get; set; }

    /// <summary>
    /// Component configuration summary text for display in the UI metadata pane.
    /// </summary>
    public string? ComponentConfiguration { get; set; }

    /// <summary>
    /// Structured metadata sections for display in the UI metadata pane.
    /// </summary>
    public IReadOnlyList<JdeSpecMetadataSection> MetadataSections { get; set; } = Array.Empty<JdeSpecMetadataSection>();

    /// <summary>
    /// Child nodes.
    /// </summary>
    public IReadOnlyList<JdeEventRulesNode> Children { get; set; } = Array.Empty<JdeEventRulesNode>();

    /// <summary>
    /// UI expansion hint.
    /// </summary>
    public bool IsExpanded { get; set; }

    /// <summary>
    /// Whether the node has child nodes.
    /// </summary>
    public bool HasChildren => Children.Count > 0;

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
