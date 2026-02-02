namespace JdeClient.Core.Models;

/// <summary>
/// Represents an OMW project object from F98222.
/// </summary>
public sealed class JdeProjectObjectInfo
{
    /// <summary>
    /// Project name (OMWPRJID).
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Raw object id (OMWOBJID).
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Parsed object name portion of OMWOBJID.
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Parsed version name (when present in OMWOBJID).
    /// </summary>
    public string? VersionName { get; set; }

    /// <summary>
    /// Object type (OMWOT).
    /// </summary>
    public string ObjectType { get; set; } = string.Empty;

    /// <summary>
    /// Path code (PATHCD).
    /// </summary>
    public string? PathCode { get; set; }

    /// <summary>
    /// Source release (SRCRLS).
    /// </summary>
    public string? SourceRelease { get; set; }

    /// <summary>
    /// Object status (OMWOST).
    /// </summary>
    public string? ObjectStatus { get; set; }

    /// <summary>
    /// Object version status (OMWOVS).
    /// </summary>
    public string? VersionStatus { get; set; }

    /// <summary>
    /// User who last updated the object (OMWUSER).
    /// </summary>
    public string? User { get; set; }

    public override string ToString() => $"{ObjectId} ({ObjectType})";
}
