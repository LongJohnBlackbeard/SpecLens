namespace JdeClient.Core.Models;

/// <summary>
/// Result of an OMW export to a .par repository.
/// </summary>
public sealed class JdeOmwExportResult
{
    /// <summary>
    /// Object ID that was exported (e.g., project name).
    /// </summary>
    public string ObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Object type (e.g., "PRJ", "UBE", "APPL").
    /// </summary>
    public string ObjectType { get; set; } = string.Empty;

    /// <summary>
    /// Project name associated with the export (required by OMW).
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Resolved output path when an external save location is provided.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// True when the output file already existed before export.
    /// </summary>
    public bool FileAlreadyExists { get; set; }
}
