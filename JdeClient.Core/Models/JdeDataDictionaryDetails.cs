namespace JdeClient.Core.Models;

/// <summary>
/// Represents the full DDDICT record for a data dictionary item, plus related text rows.
/// </summary>
public sealed class JdeDataDictionaryDetails
{
    // Raw DDDICT fields captured from spec data.
    public string DataItem { get; set; } = string.Empty;
    public uint VarLength { get; set; }
    public int FormatNumber { get; set; }
    public string? DictionaryName { get; set; }
    public string? SystemCode { get; set; }
    public char GlossaryGroup { get; set; }
    public char ErrorLevel { get; set; }
    public string? Alias { get; set; }
    public char TypeCode { get; set; }
    public int EverestType { get; set; }
    public string? As400Class { get; set; }
    public int Length { get; set; }
    public ushort Decimals { get; set; }
    public ushort DisplayDecimals { get; set; }
    public string? DefaultValue { get; set; }
    public ushort ControlType { get; set; }
    public string? As400EditRule { get; set; }
    public string? As400EditParm1 { get; set; }
    public string? As400EditParm2 { get; set; }
    public string? As400DispRule { get; set; }
    public string? As400DispParm { get; set; }
    public int EditBehavior { get; set; }
    public int DisplayBehavior { get; set; }
    public char SecurityFlag { get; set; }
    public ushort NextNumberIndex { get; set; }
    public string? NextNumberSystem { get; set; }
    public int Style { get; set; }
    public int Behavior { get; set; }
    public string? DataSourceTemplateName { get; set; }
    public string? DisplayRuleBfnName { get; set; }
    public string? EditRuleBfnName { get; set; }
    public string? SearchFormName { get; set; }

    /// <summary>
    /// Associated DDTEXT rows for this data item.
    /// </summary>
    public List<JdeDataDictionaryText> Texts { get; } = new();

    /// <summary>
    /// Friendly display name for the item.
    /// </summary>
    public string? Name => Alias;

    /// <summary>
    /// First line of column title text (DDTEXT type C).
    /// </summary>
    public string? ColumnTitle
    {
        get
        {
            var (title1, _) = SplitTitle(GetText('C'));
            return title1;
        }
    }

    /// <summary>
    /// Second line of column title text (DDTEXT type C).
    /// </summary>
    public string? ColumnTitle2
    {
        get
        {
            var (_, title2) = SplitTitle(GetText('C'));
            return title2;
        }
    }

    /// <summary>
    /// Combined column title text.
    /// </summary>
    public string? CombinedTitle
    {
        get
        {
            string? part1 = ColumnTitle;
            string? part2 = ColumnTitle2;
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

    /// <summary>
    /// Alpha description text (DDTEXT type A).
    /// </summary>
    public string? Description => GetText('A');

    /// <summary>
    /// Row description text (DDTEXT type R).
    /// </summary>
    public string? RowDescription => GetText('R');

    /// <summary>
    /// Glossary text (DDTEXT type H).
    /// </summary>
    public string? Glossary => GetText('H');

    /// <summary>
    /// Return the first non-empty text matching the supplied text types.
    /// </summary>
    public string? GetText(params char[] textTypes)
    {
        if (Texts.Count == 0 || textTypes == null || textTypes.Length == 0)
        {
            return null;
        }

        foreach (var textType in textTypes)
        {
            char target = char.ToUpperInvariant(textType);
            var match = Texts.FirstOrDefault(text => char.ToUpperInvariant(text.TextType) == target);
            if (!string.IsNullOrWhiteSpace(match?.Text))
            {
                return match.Text.Trim();
            }
        }

        return null;
    }

    private static (string? Title1, string? Title2) SplitTitle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, null);
        }

        var lines = text
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            return (null, null);
        }

        if (lines.Count == 1)
        {
            return (lines[0], null);
        }

        return (lines[0], lines[1]);
    }
}
