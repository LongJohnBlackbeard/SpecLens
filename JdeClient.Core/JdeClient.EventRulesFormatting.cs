using System.Text;
using System.Linq;
using System.Xml.Linq;
using JdeClient.Core.Models;
using JdeClient.Core.XmlEngine;

namespace JdeClient.Core;

public partial class JdeClient
{
    public async Task<JdeEventRulesFormattedResult> GetFormattedEventRulesAsync(
        JdeEventRulesNode node,
        CancellationToken cancellationToken = default)
    {
        return await GetFormattedEventRulesAsync(
            node,
            JdeEventRulesOutputFormat.Readable,
            useCentralLocation: false,
            dataSourceOverride: null,
            cancellationToken);
    }

    public async Task<JdeEventRulesFormattedResult> GetFormattedEventRulesAsync(
        JdeEventRulesNode node,
        JdeEventRulesOutputFormat outputFormat,
        CancellationToken cancellationToken = default)
    {
        return await GetFormattedEventRulesAsync(
            node,
            outputFormat,
            useCentralLocation: false,
            dataSourceOverride: null,
            cancellationToken);
    }

    public async Task<JdeEventRulesFormattedResult> GetFormattedEventRulesAsync(
        JdeEventRulesNode node,
        bool useCentralLocation,
        string? dataSourceOverride,
        CancellationToken cancellationToken = default)
    {
        return await GetFormattedEventRulesAsync(
            node,
            JdeEventRulesOutputFormat.Readable,
            useCentralLocation,
            dataSourceOverride,
            cancellationToken);
    }

    public async Task<JdeEventRulesFormattedResult> GetFormattedEventRulesAsync(
        JdeEventRulesNode node,
        JdeEventRulesOutputFormat outputFormat,
        bool useCentralLocation,
        string? dataSourceOverride,
        CancellationToken cancellationToken = default)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var templateName = ResolveTemplateName(node);
        var eventSpecKey = node.EventSpecKey ?? string.Empty;
        bool allowDynamicTemplateFallback = ShouldAllowDynamicTemplateFallback(node, templateName);

        if (string.IsNullOrWhiteSpace(eventSpecKey))
        {
            return new JdeEventRulesFormattedResult
            {
                EventSpecKey = eventSpecKey,
                TemplateName = templateName,
                StatusMessage = "No event rules for the selected item."
            };
        }

        if (outputFormat == JdeEventRulesOutputFormat.Readable &&
            string.IsNullOrWhiteSpace(templateName) &&
            !allowDynamicTemplateFallback)
        {
            return new JdeEventRulesFormattedResult
            {
                EventSpecKey = eventSpecKey,
                TemplateName = templateName,
                StatusMessage = "No data structure found for the selected item."
            };
        }

        var eventDocuments = useCentralLocation
            ? await GetEventRulesXmlAsync(eventSpecKey, useCentralLocation, dataSourceOverride, cancellationToken).ConfigureAwait(false)
            : await GetEventRulesXmlAsync(eventSpecKey, cancellationToken).ConfigureAwait(false);

        string resolvedTemplateName = templateName;
        if (string.IsNullOrWhiteSpace(resolvedTemplateName))
        {
            resolvedTemplateName = InferTemplateNameFromEventDocuments(eventDocuments);
        }

        if (outputFormat == JdeEventRulesOutputFormat.Xml)
        {
            return FormatEventRulesXml(eventDocuments, resolvedTemplateName, eventSpecKey);
        }

        if (outputFormat != JdeEventRulesOutputFormat.Readable)
        {
            throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, "Unsupported event rules output format.");
        }

        IReadOnlyList<JdeSpecXmlDocument> dataStructureDocuments = Array.Empty<JdeSpecXmlDocument>();
        if (!string.IsNullOrWhiteSpace(resolvedTemplateName))
        {
            dataStructureDocuments = useCentralLocation
                ? await GetDataStructureXmlAsync(resolvedTemplateName, useCentralLocation, dataSourceOverride, cancellationToken).ConfigureAwait(false)
                : await GetDataStructureXmlAsync(resolvedTemplateName, cancellationToken).ConfigureAwait(false);
        }

        return FormatEventRules(
            eventDocuments,
            dataStructureDocuments,
            resolvedTemplateName,
            eventSpecKey,
            this,
            allowDynamicTemplateFallback,
            useCentralLocation,
            dataSourceOverride);
    }

    public async Task<JdeEventRulesFormattedResult> GetFormattedEventRulesAsync(
        string eventSpecKey,
        string templateName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventSpecKey))
        {
            return new JdeEventRulesFormattedResult
            {
                EventSpecKey = eventSpecKey,
                TemplateName = templateName,
                StatusMessage = "No event rules found."
            };
        }

        if (string.IsNullOrWhiteSpace(templateName))
        {
            return new JdeEventRulesFormattedResult
            {
                EventSpecKey = eventSpecKey,
                TemplateName = templateName,
                StatusMessage = "No data structure found for the selected item."
            };
        }

        var eventDocuments = await GetEventRulesXmlAsync(eventSpecKey, cancellationToken)
            .ConfigureAwait(false);
        var dataStructureDocuments = await GetDataStructureXmlAsync(templateName, cancellationToken)
            .ConfigureAwait(false);

        return FormatEventRules(
            eventDocuments,
            dataStructureDocuments,
            templateName,
            eventSpecKey,
            this,
            allowDynamicTemplateFallback: false,
            useCentralLocation: false,
            dataSourceOverride: null);
    }

    /// <summary>
    /// Resolve a data structure template name from an event rules node.
    /// </summary>
    internal static string ResolveTemplateName(JdeEventRulesNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.DataStructureName))
        {
            return node.DataStructureName;
        }

        if (node.NodeType == JdeEventRulesNodeType.Event)
        {
            // Event nodes from APPL/UBE/TBLE trees use display labels ("Event 11"), not DSTMPL names.
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(node.Name))
        {
            return string.Empty;
        }

        if (node.Name.StartsWith("B", StringComparison.OrdinalIgnoreCase) && node.Name.Length > 1)
        {
            return $"D{node.Name.Substring(1)}";
        }

        return node.Name;
    }

    private static bool ShouldAllowDynamicTemplateFallback(JdeEventRulesNode node, string templateName)
    {
        return string.IsNullOrWhiteSpace(templateName) &&
               node.NodeType == JdeEventRulesNodeType.Event;
    }

    internal static string InferTemplateNameFromEventDocuments(IReadOnlyList<JdeEventRulesXmlDocument> eventDocuments)
    {
        foreach (var document in eventDocuments)
        {
            if (TryInferTemplateNameFromEventXml(document.Xml, out string inferred))
            {
                return inferred;
            }
        }

        return string.Empty;
    }

    private static bool TryInferTemplateNameFromEventXml(string? xml, out string templateName)
    {
        templateName = string.Empty;
        if (string.IsNullOrWhiteSpace(xml))
        {
            return false;
        }

        try
        {
            string normalized = JdeXmlEngine.NormalizeXmlPayload(xml);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            var document = XDocument.Parse(normalized);
            var root = document.Root;
            if (root == null)
            {
                return false;
            }

            string[] candidates = { "szTmplName", "szTemplateName", "szDsTmplName" };
            foreach (string candidate in candidates)
            {
                var attribute = root
                    .DescendantsAndSelf()
                    .Attributes()
                    .FirstOrDefault(attr =>
                        attr.Name.LocalName.Equals(candidate, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(attr.Value));
                if (attribute != null)
                {
                    templateName = attribute.Value.Trim();
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Format event rules XML with the provided data structure XML.
    /// </summary>
    private static JdeEventRulesFormattedResult FormatEventRules(
        IReadOnlyList<JdeEventRulesXmlDocument> eventDocuments,
        IReadOnlyList<JdeSpecXmlDocument> dataStructureDocuments,
        string templateName,
        string eventSpecKey,
        JdeClient client,
        bool allowDynamicTemplateFallback,
        bool useCentralLocation,
        string? dataSourceOverride)
    {
        string eventXml = eventDocuments.FirstOrDefault(doc => !string.IsNullOrWhiteSpace(doc.Xml))?.Xml ?? string.Empty;
        string dataStructureXml = dataStructureDocuments.FirstOrDefault(doc => !string.IsNullOrWhiteSpace(doc.Xml))?.Xml ?? string.Empty;

        if (string.IsNullOrWhiteSpace(eventXml))
        {
            return new JdeEventRulesFormattedResult
            {
                EventSpecKey = eventSpecKey,
                TemplateName = templateName,
                StatusMessage = "No event rules found."
            };
        }

        if (string.IsNullOrWhiteSpace(dataStructureXml))
        {
            if (!allowDynamicTemplateFallback)
            {
                return new JdeEventRulesFormattedResult
                {
                    EventSpecKey = eventSpecKey,
                    TemplateName = templateName,
                    StatusMessage = "No data structure XML available for the selected item."
                };
            }

            dataStructureXml = BuildFallbackDataStructureXml(eventDocuments, templateName);
        }

        var resolver = new JdeSpecResolver(client, useCentralLocation, dataSourceOverride);
        var combined = new List<string>();

        foreach (var document in eventDocuments)
        {
            if (string.IsNullOrWhiteSpace(document.Xml))
            {
                continue;
            }

            var engine = new JdeXmlEngine(document.Xml, dataStructureXml, resolver);
            engine.ConvertXmlToReadableEr();
            if (!string.IsNullOrWhiteSpace(engine.ReadableEventRule))
            {
                combined.Add(engine.ReadableEventRule.TrimEnd());
            }
        }

        string text = combined.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine + Environment.NewLine, combined);

        if (string.IsNullOrWhiteSpace(text))
        {
            return new JdeEventRulesFormattedResult
            {
                EventSpecKey = eventSpecKey,
                TemplateName = templateName,
                StatusMessage = "No formatted event rules available."
            };
        }

        return new JdeEventRulesFormattedResult
        {
            EventSpecKey = eventSpecKey,
            TemplateName = templateName,
            Text = ApplyIndentGuides(text),
            StatusMessage = "Event rules loaded."
        };
    }

    private static JdeEventRulesFormattedResult FormatEventRulesXml(
        IReadOnlyList<JdeEventRulesXmlDocument> eventDocuments,
        string templateName,
        string eventSpecKey)
    {
        var xmlParts = eventDocuments
            .Where(document => !string.IsNullOrWhiteSpace(document.Xml))
            .Select(document => document.Xml.Trim())
            .Where(xml => !string.IsNullOrWhiteSpace(xml))
            .ToList();

        if (xmlParts.Count == 0)
        {
            return new JdeEventRulesFormattedResult
            {
                EventSpecKey = eventSpecKey,
                TemplateName = templateName,
                StatusMessage = "No event rules found."
            };
        }

        return new JdeEventRulesFormattedResult
        {
            EventSpecKey = eventSpecKey,
            TemplateName = templateName,
            Text = string.Join(Environment.NewLine + Environment.NewLine, xmlParts),
            StatusMessage = "Event rules XML loaded."
        };
    }

    private static string BuildFallbackDataStructureXml(
        IReadOnlyList<JdeEventRulesXmlDocument> eventDocuments,
        string templateName)
    {
        XNamespace xmlNamespace = "http://peoplesoft.com/e1/metadata/v1.0";
        foreach (var document in eventDocuments)
        {
            if (string.IsNullOrWhiteSpace(document.Xml))
            {
                continue;
            }

            try
            {
                string normalized = JdeXmlEngine.NormalizeXmlPayload(document.Xml);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                var parsed = XDocument.Parse(normalized);
                var root = parsed.Root;
                if (root != null && root.Name.Namespace != XNamespace.None)
                {
                    xmlNamespace = root.Name.Namespace;
                    break;
                }
            }
            catch
            {
                // Ignore malformed fragments and keep searching for a namespace.
            }
        }

        string resolvedTemplateName = string.IsNullOrWhiteSpace(templateName)
            ? "__DYNAMIC__"
            : templateName.Trim();
        var dataStructureRoot = new XElement(xmlNamespace + "DSTMPL",
            new XAttribute("szTmplName", resolvedTemplateName),
            new XElement(xmlNamespace + "Template"));

        return new XDocument(dataStructureRoot).ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// Replace leading tabs with visual indentation guides for display.
    /// </summary>
    internal static string ApplyIndentGuides(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        var builder = new StringBuilder(text.Length + lines.Length * 2);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int tabs = 0;
            while (tabs < line.Length && line[tabs] == '\t')
            {
                tabs++;
            }

            if (tabs > 0)
            {
                for (int t = 0; t < tabs; t++)
                {
                    builder.Append("|   ");
                }

                builder.Append(line.Substring(tabs));
            }
            else
            {
                builder.Append(line);
            }

            if (i < lines.Length - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
