using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace JdeClient.Core.XmlEngine.Models;

/// <summary>
/// Represents a single item within a data structure template.
/// </summary>
public class DataStructureTemplateItem
{
    /// <summary>
    /// Template item identifier.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Display sequence as defined in the template.
    /// </summary>
    public required string DisplaySequence { get; set; }

    /// <summary>
    /// Copy word from the data dictionary.
    /// </summary>
    public required string CopyWork { get; set; }

    /// <summary>
    /// Data dictionary alias.
    /// </summary>
    public required string Alias { get; set; }

    /// <summary>
    /// Field name used in the template.
    /// </summary>
    public required string FieldName { get; set; }

    public DataStructureTemplateItem()
    {
    }

    /// <summary>
    /// Create a template item from an XML element.
    /// </summary>
    [SetsRequiredMembers]
    public DataStructureTemplateItem(XElement element)
    {
        Id = element.Attribute("ItemID")!.Value;
        DisplaySequence = element.Attribute("DisplaySequence")!.Value;
        CopyWork = element.Attribute("CopyWord")!.Value;
        Alias = element.Attribute("DDAlias")!.Value;
        FieldName = element.Attribute("FieldName")!.Value;
    }

    /// <summary>
    /// Get a formatted display name.
    /// </summary>
    public string GetFormattedName()
    {
        return $"{FieldName} [{Alias}]";
    }
}
