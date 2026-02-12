using System.Text;
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
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var templateName = ResolveTemplateName(node);
        var eventSpecKey = node.EventSpecKey ?? string.Empty;

        if (string.IsNullOrWhiteSpace(eventSpecKey))
        {
            return new JdeEventRulesFormattedResult
            {
                EventSpecKey = eventSpecKey,
                TemplateName = templateName,
                StatusMessage = "No event rules for the selected item."
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

        var eventDocuments = useCentralLocation
            ? await GetEventRulesXmlAsync(eventSpecKey, useCentralLocation, dataSourceOverride, cancellationToken).ConfigureAwait(false)
            : await GetEventRulesXmlAsync(eventSpecKey, cancellationToken).ConfigureAwait(false);
        var dataStructureDocuments = useCentralLocation
            ? await GetDataStructureXmlAsync(templateName, useCentralLocation, dataSourceOverride, cancellationToken).ConfigureAwait(false)
            : await GetDataStructureXmlAsync(templateName, cancellationToken).ConfigureAwait(false);

        return FormatEventRules(eventDocuments, dataStructureDocuments, templateName, eventSpecKey, this);
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

        return FormatEventRules(eventDocuments, dataStructureDocuments, templateName, eventSpecKey, this);
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

    /// <summary>
    /// Format event rules XML with the provided data structure XML.
    /// </summary>
    private static JdeEventRulesFormattedResult FormatEventRules(
        IReadOnlyList<JdeEventRulesXmlDocument> eventDocuments,
        IReadOnlyList<JdeSpecXmlDocument> dataStructureDocuments,
        string templateName,
        string eventSpecKey,
        JdeClient client)
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
            return new JdeEventRulesFormattedResult
            {
                EventSpecKey = eventSpecKey,
                TemplateName = templateName,
                StatusMessage = "No data structure XML available for the selected item."
            };
        }

        var resolver = new JdeSpecResolver(client);
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
