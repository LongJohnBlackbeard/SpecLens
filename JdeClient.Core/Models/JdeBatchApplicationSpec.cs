namespace JdeClient.Core.Models;

/// <summary>
/// Structured batch application/report (UBE) spec data.
/// </summary>
public sealed class JdeBatchApplicationSpec : JdeObjectSpec
{
    public IReadOnlyList<JdeBatchVersionSpec> Versions { get; set; } = Array.Empty<JdeBatchVersionSpec>();
}

/// <summary>
/// Structured UBE version metadata.
/// </summary>
public sealed class JdeBatchVersionSpec
{
    public string VersionName { get; set; } = string.Empty;

    public IReadOnlyList<JdeSpecMetadataSection> MetadataSections { get; set; } = Array.Empty<JdeSpecMetadataSection>();

    public IReadOnlyList<JdeBatchSectionSpec> Sections { get; set; } = Array.Empty<JdeBatchSectionSpec>();
}

/// <summary>
/// Structured UBE section metadata.
/// </summary>
public sealed class JdeBatchSectionSpec : JdeSectionSpec
{
    public string SectionKey { get; set; } = string.Empty;

    public int SectionId { get; set; }

    public string SectionType { get; set; } = string.Empty;

    public string? BusinessViewName { get; set; }

    public bool Visible { get; set; }

    public bool AbsolutePositional { get; set; }

    public bool PageBreakAfter { get; set; }

    public bool Conditional { get; set; }

    public bool ReprintAtPageBreak { get; set; }

    public int Left { get; set; }

    public int Top { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public IReadOnlyList<JdeBatchEventSpec> Events { get; set; } = Array.Empty<JdeBatchEventSpec>();

    public IReadOnlyList<JdeBatchControlSpec> Controls { get; set; } = Array.Empty<JdeBatchControlSpec>();
}

/// <summary>
/// Structured UBE control/object metadata within a report section.
/// </summary>
public sealed class JdeBatchControlSpec
{
    public int ControlId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? DataStructureName { get; set; }

    public int? TextId { get; set; }

    public string? ComponentTypeName { get; set; }

    public string? ComponentConfiguration { get; set; }

    public IReadOnlyList<JdeSpecMetadataSection> MetadataSections { get; set; } = Array.Empty<JdeSpecMetadataSection>();

    public IReadOnlyList<JdeBatchEventSpec> Events { get; set; } = Array.Empty<JdeBatchEventSpec>();
}

/// <summary>
/// Structured UBE event metadata.
/// </summary>
public sealed class JdeBatchEventSpec : JdeEventSpec
{
    public int? ControlId { get; set; }
}
