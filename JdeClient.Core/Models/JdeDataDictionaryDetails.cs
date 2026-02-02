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
}
