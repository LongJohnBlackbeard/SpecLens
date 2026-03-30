namespace JdeClient.Core.Models;

/// <summary>
/// Structured interactive application (APPL) spec data.
/// </summary>
public sealed class JdeInteractiveApplicationSpec : JdeObjectSpec
{
    public string? ProcessingOptionTemplateName { get; set; }

    public JdeInteractiveDataStructureSpec? ProcessingOptions { get; set; }

    public IReadOnlyList<JdeInteractiveFormSpec> Forms { get; set; } = Array.Empty<JdeInteractiveFormSpec>();
}

/// <summary>
/// Structured interactive application form metadata.
/// </summary>
public abstract class JdeInteractiveFormSpec : JdeFormSpec
{
    protected JdeInteractiveFormSpec(
        JdeInteractiveFormType formType,
        string formTypeLabel)
    {
        FormType = formType;
        FormTypeLabel = formTypeLabel;
    }

    public JdeInteractiveFormType FormType { get; set; }

    public string FormTypeLabel { get; set; }

    public string? BusinessViewName { get; set; }

    public JdeBusinessViewInfo? BusinessView { get; set; }

    public bool? UpdateOnFormBusinessView { get; set; }

    public bool? FetchOnFormBusinessView { get; set; }

    public bool? UpdateOnGridBusinessView { get; set; }

    public bool? FetchOnGridBusinessView { get; set; }

    public string? TransactionType { get; set; }

    public int ControlCount { get; set; }

    public JdeInteractiveDataStructureSpec? DataStructure { get; set; }

    public IReadOnlyList<JdeInteractiveEventSpec> Events { get; set; } = Array.Empty<JdeInteractiveEventSpec>();

    public IReadOnlyList<JdeInteractiveComponentSpec> Components { get; set; } = Array.Empty<JdeInteractiveComponentSpec>();
}

/// <summary>
/// Structured main form metadata.
/// </summary>
public sealed class JdeInteractiveStandardFormSpec : JdeInteractiveFormSpec
{
    public JdeInteractiveStandardFormSpec()
        : base(JdeInteractiveFormType.Form, "Form")
    {
    }
}

/// <summary>
/// Structured subform metadata.
/// </summary>
public sealed class JdeInteractiveSubformSpec : JdeInteractiveFormSpec
{
    public JdeInteractiveSubformSpec()
        : base(JdeInteractiveFormType.Subform, "Subform")
    {
    }
}

/// <summary>
/// Structured fallback form metadata when the type cannot be resolved.
/// </summary>
public sealed class JdeInteractiveUnknownFormSpec : JdeInteractiveFormSpec
{
    public JdeInteractiveUnknownFormSpec()
        : base(JdeInteractiveFormType.Unknown, "Unknown")
    {
    }
}

/// <summary>
/// Structured interactive application component metadata.
/// </summary>
public abstract class JdeInteractiveComponentSpec
{
    protected JdeInteractiveComponentSpec(
        JdeInteractiveComponentType componentType,
        string componentTypeLabel)
    {
        ComponentType = componentType;
        ComponentTypeLabel = componentTypeLabel;
    }

    public int ControlId { get; set; }

    public int? ObjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public JdeInteractiveComponentType ComponentType { get; set; }

    public string ComponentTypeLabel { get; set; }

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
/// Structured fallback control metadata when the type cannot be resolved.
/// </summary>
public sealed class JdeInteractiveUnknownComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractiveUnknownComponentSpec()
        : base(JdeInteractiveComponentType.Unknown, "Unknown")
    {
    }
}

/// <summary>
/// Structured generic control metadata.
/// </summary>
public sealed class JdeInteractiveControlComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractiveControlComponentSpec()
        : base(JdeInteractiveComponentType.Control, "Control")
    {
    }
}

/// <summary>
/// Structured push button metadata.
/// </summary>
public sealed class JdeInteractivePushButtonComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractivePushButtonComponentSpec()
        : base(JdeInteractiveComponentType.PushButton, "Push Button")
    {
    }
}

/// <summary>
/// Structured grid metadata.
/// </summary>
public sealed class JdeInteractiveGridComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractiveGridComponentSpec()
        : base(JdeInteractiveComponentType.Grid, "Grid")
    {
    }
}

/// <summary>
/// Structured grid column metadata.
/// </summary>
public sealed class JdeInteractiveGridColumnComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractiveGridColumnComponentSpec()
        : base(JdeInteractiveComponentType.GridColumn, "Grid Column")
    {
    }
}

/// <summary>
/// Structured text block metadata.
/// </summary>
public sealed class JdeInteractiveTextBlockComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractiveTextBlockComponentSpec()
        : base(JdeInteractiveComponentType.TextBlock, "Text Block")
    {
    }
}

/// <summary>
/// Structured function control metadata.
/// </summary>
public sealed class JdeInteractiveFunctionComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractiveFunctionComponentSpec()
        : base(JdeInteractiveComponentType.Function, "Function")
    {
    }
}

/// <summary>
/// Structured tab control metadata.
/// </summary>
public sealed class JdeInteractiveTabControlComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractiveTabControlComponentSpec()
        : base(JdeInteractiveComponentType.TabControl, "Tab Control")
    {
    }
}

/// <summary>
/// Structured page metadata.
/// </summary>
public sealed class JdeInteractivePageComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractivePageComponentSpec()
        : base(JdeInteractiveComponentType.Page, "Page")
    {
    }
}

/// <summary>
/// Structured checkbox metadata.
/// </summary>
public sealed class JdeInteractiveCheckBoxComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractiveCheckBoxComponentSpec()
        : base(JdeInteractiveComponentType.CheckBox, "Check Box")
    {
    }
}

/// <summary>
/// Structured radio button metadata.
/// </summary>
public sealed class JdeInteractiveRadioButtonComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractiveRadioButtonComponentSpec()
        : base(JdeInteractiveComponentType.RadioButton, "Radio Button")
    {
    }
}

/// <summary>
/// Structured combo box metadata.
/// </summary>
public sealed class JdeInteractiveComboBoxComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractiveComboBoxComponentSpec()
        : base(JdeInteractiveComponentType.ComboBox, "Combo Box")
    {
    }
}

/// <summary>
/// Structured list box metadata.
/// </summary>
public sealed class JdeInteractiveListBoxComponentSpec : JdeInteractiveComponentSpec
{
    public JdeInteractiveListBoxComponentSpec()
        : base(JdeInteractiveComponentType.ListBox, "List Box")
    {
    }
}

/// <summary>
/// Structured APPL event metadata.
/// </summary>
public sealed class JdeInteractiveEventSpec : JdeEventSpec
{
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
