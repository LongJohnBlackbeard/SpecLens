namespace JdeClient.Core.Models;

/// <summary>
/// Represents a JDE OMW project from F98220.
/// </summary>
public sealed class JdeProjectInfo
{
    /// <summary>
    /// Project name (OMWPRJID).
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Project description (OMWDESC).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Project status (OMWPS).
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Project type (OMWTYP).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Source release (SRCRLS).
    /// </summary>
    public string? SourceRelease { get; set; }

    /// <summary>
    /// Target release (TRGRLS).
    /// </summary>
    public string? TargetRelease { get; set; }

    /// <summary>
    /// Saved package name (DSAVNAME).
    /// </summary>
    public string? SaveName { get; set; }

    public override string ToString() => $"{ProjectName} ({Status})";
}
