namespace JdeClient.Core.Models;

/// <summary>
/// Structured interactive application (APPL) spec data.
/// </summary>
public sealed class JdeInteractiveApplicationSpec
{
    public string ObjectName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ProcessingOptionTemplateName { get; set; }

    public JdeInteractiveDataStructureSpec? ProcessingOptions { get; set; }

    public IReadOnlyList<JdeInteractiveFormSpec> Forms { get; set; } = Array.Empty<JdeInteractiveFormSpec>();

    public IReadOnlyList<JdeSpecMetadataSection> MetadataSections { get; set; } = Array.Empty<JdeSpecMetadataSection>();

    public IReadOnlyDictionary<string, string> Attributes { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Structured interactive application form metadata.
/// </summary>
public sealed class JdeInteractiveFormSpec
{
    public string ObjectName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public JdeInteractiveFormType FormType { get; set; } = JdeInteractiveFormType.Unknown;

    public string FormTypeLabel { get; set; } = "Unknown";

    public string? BusinessViewName { get; set; }

    public JdeBusinessViewInfo? BusinessView { get; set; }

    public bool? UpdateOnFormBusinessView { get; set; }

    public bool? FetchOnFormBusinessView { get; set; }

    public bool? UpdateOnGridBusinessView { get; set; }

    public bool? FetchOnGridBusinessView { get; set; }

    public string? TransactionType { get; set; }

    public int ControlCount { get; set; }

    public string? DataStructureName { get; set; }

    public JdeInteractiveDataStructureSpec? DataStructure { get; set; }

    public IReadOnlyList<JdeInteractiveEventSpec> Events { get; set; } = Array.Empty<JdeInteractiveEventSpec>();

    public IReadOnlyList<JdeInteractiveComponentSpec> Components { get; set; } = Array.Empty<JdeInteractiveComponentSpec>();

    public IReadOnlyList<JdeSpecMetadataSection> MetadataSections { get; set; } = Array.Empty<JdeSpecMetadataSection>();

    public IReadOnlyDictionary<string, string> Attributes { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Structured interactive application component metadata.
/// </summary>
public sealed class JdeInteractiveComponentSpec
{
    public int ControlId { get; set; }

    public int? ObjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public JdeInteractiveComponentType ComponentType { get; set; } = JdeInteractiveComponentType.Unknown;

    public string ComponentTypeLabel { get; set; } = "Unknown";

    public int? ParentControlId { get; set; }

    public int? DisplayOrder { get; set; }

    public string? DataStructureName { get; set; }

    public string? BusinessViewName { get; set; }

    public string? DataItem { get; set; }

    public string? TableName { get; set; }

    public bool? IsVisible { get; set; }

    public IReadOnlyList<JdeInteractiveEventSpec> Events { get; set; } = Array.Empty<JdeInteractiveEventSpec>();

    public IReadOnlyList<JdeInteractiveComponentSpec> Children { get; set; } = Array.Empty<JdeInteractiveComponentSpec>();

    public IReadOnlyList<JdeSpecMetadataSection> MetadataSections { get; set; } = Array.Empty<JdeSpecMetadataSection>();

    public IReadOnlyDictionary<string, string> Attributes { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Structured APPL event metadata.
/// </summary>
public sealed class JdeInteractiveEventSpec
{
    public string Name { get; set; } = string.Empty;

    public string EventSpecKey { get; set; } = string.Empty;

    public string? EventId { get; set; }

    public int? EventId3 { get; set; }

    public string? TemplateName { get; set; }

    public IReadOnlyList<JdeSpecMetadataSection> MetadataSections { get; set; } = Array.Empty<JdeSpecMetadataSection>();
}

/// <summary>
/// Structured APPL data structure metadata.
/// </summary>
public sealed class JdeInteractiveDataStructureSpec
{
    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IReadOnlyList<JdeInteractiveDataStructureItemSpec> Items { get; set; } =
        Array.Empty<JdeInteractiveDataStructureItemSpec>();
}

/// <summary>
/// Structured APPL data structure item metadata.
/// </summary>
public sealed class JdeInteractiveDataStructureItemSpec
{
    public int Sequence { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DataItem { get; set; } = string.Empty;
}

/// <summary>
/// Render-ready metadata section for a selected spec node.
/// </summary>
public sealed class JdeSpecMetadataSection
{
    public string Title { get; set; } = string.Empty;

    public IReadOnlyList<JdeSpecMetadataItem> Items { get; set; } = Array.Empty<JdeSpecMetadataItem>();
}

/// <summary>
/// Label/value pair within a metadata section.
/// </summary>
public sealed class JdeSpecMetadataItem
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public enum JdeInteractiveFormType
{
    Unknown,
    Form,
    Subform
}

public enum JdeInteractiveComponentType
{
    Unknown,
    Control,
    PushButton,
    Grid,
    GridColumn,
    TextBlock,
    Function,
    TabControl,
    Page,
    CheckBox,
    RadioButton,
    ComboBox,
    ListBox
}
