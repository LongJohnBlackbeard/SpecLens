namespace JdeClient.Core.Models;

/// <summary>
/// Base domain model for a JDE object spec exposed by the client library.
/// </summary>
public abstract class JdeObjectSpec
{
    public string ObjectName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public IReadOnlyList<JdeSpecMetadataSection> MetadataSections { get; set; } = Array.Empty<JdeSpecMetadataSection>();

    public IReadOnlyDictionary<string, string> Attributes { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Shared base type for form-like object members.
/// </summary>
public abstract class JdeFormSpec
{
    public string ObjectName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? DataStructureName { get; set; }

    public IReadOnlyList<JdeSpecMetadataSection> MetadataSections { get; set; } = Array.Empty<JdeSpecMetadataSection>();

    public IReadOnlyDictionary<string, string> Attributes { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Shared base type for section-like object members.
/// </summary>
public abstract class JdeSectionSpec
{
    public string Name { get; set; } = string.Empty;

    public IReadOnlyList<JdeSpecMetadataSection> MetadataSections { get; set; } = Array.Empty<JdeSpecMetadataSection>();

    public IReadOnlyDictionary<string, string> Attributes { get; set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Shared base type for event rule bearing members.
/// </summary>
public abstract class JdeEventSpec
{
    public string Name { get; set; } = string.Empty;

    public string EventSpecKey { get; set; } = string.Empty;

    public string? EventId { get; set; }

    public int? EventId3 { get; set; }

    public string? TemplateName { get; set; }

    public IReadOnlyList<JdeSpecMetadataSection> MetadataSections { get; set; } = Array.Empty<JdeSpecMetadataSection>();
}
