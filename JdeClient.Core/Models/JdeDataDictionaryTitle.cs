namespace JdeClient.Core.Models;

/// <summary>
/// Represents title text for a data dictionary item.
/// </summary>
public sealed class JdeDataDictionaryTitle
{
    /// <summary>
    /// Data dictionary item (DTAI).
    /// </summary>
    public string DataItem { get; set; } = string.Empty;

    /// <summary>
    /// Primary title line.
    /// </summary>
    public string? Title1 { get; set; }

    /// <summary>
    /// Secondary title line.
    /// </summary>
    public string? Title2 { get; set; }

    /// <summary>
    /// Combined title text, if available.
    /// </summary>
    public string? CombinedTitle
    {
        get
        {
            string part1 = Title1?.Trim();
            string part2 = Title2?.Trim();
            if (string.IsNullOrWhiteSpace(part1))
            {
                return string.IsNullOrWhiteSpace(part2) ? null : part2;
            }
            if (string.IsNullOrWhiteSpace(part2))
            {
                return part1;
            }
            return $"{part1} {part2}";
        }
    }
}
