namespace JdeClient.Core.Models;

/// <summary>
/// Represents a JDE data source definition (from F98611).
/// </summary>
public sealed class JdeDataSourceInfo
{
    /// <summary>
    /// Data source name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Database path (if available).
    /// </summary>
    public string? DatabasePath { get; set; }

    /// <summary>
    /// Server name (if available).
    /// </summary>
    public string? ServerName { get; set; }

    /// <summary>
    /// Database name (if available).
    /// </summary>
    public string? DatabaseName { get; set; }
}
