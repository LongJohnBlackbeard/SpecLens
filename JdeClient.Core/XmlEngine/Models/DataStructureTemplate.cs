using System.Xml.Linq;

namespace JdeClient.Core.XmlEngine.Models;

/// <summary>
/// Parsed data structure template (DSTMPL) with indexed template items.
/// </summary>
public sealed class DataStructureTemplate
{
    public DataStructureTemplate(
        string templateName,
        string? description,
        IReadOnlyDictionary<string, DataStructureTemplateItem> itemsById)
    {
        TemplateName = templateName;
        Description = description;
        ItemsById = itemsById;
    }

    public string TemplateName { get; }
    public string? Description { get; }
    public IReadOnlyDictionary<string, DataStructureTemplateItem> ItemsById { get; }

    /// <summary>
    /// Resolve a template item by its ItemID.
    /// </summary>
    public DataStructureTemplateItem? TryGetItem(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return ItemsById.TryGetValue(id, out var item) ? item : null;
    }

    public static DataStructureTemplate Parse(string templateName, string xml)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            throw new ArgumentException("Template name is required.", nameof(templateName));
        }

        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException("Template XML is required.", nameof(xml));
        }

        var document = XDocument.Parse(NormalizeXmlPayload(xml));
        var root = document.Root
                   ?? throw new InvalidOperationException("Data structure XML root not found.");
        var description = TryGetDescription(root);

        var templateRoot = root.Descendants().FirstOrDefault();
        var items = new Dictionary<string, DataStructureTemplateItem>(StringComparer.Ordinal);
        if (templateRoot != null)
        {
            foreach (var element in templateRoot.Descendants())
            {
                if (!TryCreateTemplateItem(element, out var item))
                {
                    continue;
                }

                if (!items.ContainsKey(item.Id))
                {
                    items[item.Id] = item;
                }
            }
        }

        return new DataStructureTemplate(templateName, description, items);
    }

    private static bool TryCreateTemplateItem(XElement element, out DataStructureTemplateItem item)
    {
        item = null!;
        var id = element.Attribute("ItemID")?.Value;
        var displaySequence = element.Attribute("DisplaySequence")?.Value;
        var copyWord = element.Attribute("CopyWord")?.Value;
        var alias = element.Attribute("DDAlias")?.Value;
        var fieldName = element.Attribute("FieldName")?.Value;

        if (string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(displaySequence) ||
            string.IsNullOrWhiteSpace(copyWord) ||
            string.IsNullOrWhiteSpace(alias) ||
            string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        item = new DataStructureTemplateItem
        {
            Id = id,
            DisplaySequence = displaySequence,
            CopyWork = copyWord,
            Alias = alias,
            FieldName = fieldName
        };

        return true;
    }

    private static string? TryGetDescription(XElement root)
    {
        var candidateAttributes = new[]
        {
            "szDescription",
            "szDesc",
            "szTitle",
            "szTemplateDesc",
            "szTemplateName"
        };

        foreach (var name in candidateAttributes)
        {
            var value = root.Attribute(name)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    // Spec XML may include padding/nulls; normalize to the first XML element.
    private static string NormalizeXmlPayload(string xml)
    {
        var cleaned = xml
            .Replace("\0", string.Empty)
            .Replace("\uFEFF", string.Empty)
            .Replace("\u200B", string.Empty)
            .TrimStart();
        var start = cleaned.IndexOf('<');
        if (start > 0)
        {
            cleaned = cleaned.Substring(start);
        }

        return cleaned;
    }
}
