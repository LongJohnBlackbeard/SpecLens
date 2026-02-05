using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JdeClient.Core.XmlEngine.Models;

namespace JdeClient.Core.XmlEngine;

/// <summary>
/// Parses JDE event rule XML alongside data structure templates into readable event rule text.
/// </summary>
/// <remarks>
/// Only a subset of GBR tags are interpreted today. Unhandled tags are intentionally ignored.
/// Call <see cref="ConvertXmlToReadableEr"/> to rebuild output from the current XML content.
/// Provide a <see cref="JdeSpecResolver"/> to enable spec-backed formatting for table I/O and business functions.
/// </remarks>
public partial class JdeXmlEngine
{
    private const string ComparisonEqual = "is equal to";
    private const string ComparisonNotEqual = "is not equal to";
    private const string ComparisonLessOrEqual = "is less than or equal to";
    private const string ComparisonGreaterThan = "is greater than";
    private const string ComparisonEqualToOrEmpty = "is equal to or empty";
    private const string OrMarker = "__OR_MARKER__";

    private static readonly Regex SplitOnAndOrRegex = new(
        @"\s+(and|or)\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PrefixRegex = new(
        @"^\s*(if|while|and|or)\b\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly HashSet<string> QualifierTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "BF",
        "VA",
        "SV",
        "CO"
    };

    private readonly JdeSpecResolver? _specResolver;
    private readonly XNamespace _xmlNamespace;
    private readonly TextInfo _textInfo;
    private readonly Dictionary<string, DataStructureTemplateItem> _primaryTemplateItems;
    private readonly Dictionary<string, DataStructureTemplate> _templateCache;
    private readonly Dictionary<string, EventLevelVariable> _eventVariables;
    private readonly List<string> _outputLines;
    private readonly string? _primaryTemplateName;
    private int _indentLevel;

    // Event Rule XML
    public string EventXmlString { get; }
    public XDocument EventXmlDocument { get; }
    public IEnumerable<XElement> EventElements { get; private set; } = Enumerable.Empty<XElement>();

    // Data Structure XML
    public string DataStructureXmlString { get; }
    private XDocument DataStructureXmlDocument { get; }
    public IEnumerable<XElement> DataStructureElements { get; private set; } = Enumerable.Empty<XElement>();

    public string RootEventSpecKey { get; private set; } = string.Empty;
    public string ReadableEventRule { get; private set; } = string.Empty;

    public JdeXmlEngine(string xmlString, string dsXmlString)
        : this(xmlString, dsXmlString, null)
    {
    }

    public JdeXmlEngine(string xmlString, string dsXmlString, JdeSpecResolver? specResolver)
    {
        _specResolver = specResolver;
        EventXmlString = NormalizeXmlPayload(xmlString ?? throw new ArgumentNullException(nameof(xmlString)));
        DataStructureXmlString = NormalizeXmlPayload(dsXmlString ?? throw new ArgumentNullException(nameof(dsXmlString)));

        EventXmlDocument = XDocument.Parse(EventXmlString);
        DataStructureXmlDocument = XDocument.Parse(DataStructureXmlString);

        var dsRoot = DataStructureXmlDocument.Root
            ?? throw new InvalidOperationException("Data structure XML root not found.");
        _xmlNamespace = dsRoot.Name.Namespace;
        _textInfo = CultureInfo.GetCultureInfo("en-US").TextInfo;

        var templateRoot = dsRoot.Descendants().FirstOrDefault()
            ?? throw new InvalidOperationException("Data structure template root not found.");
        DataStructureElements = templateRoot.Descendants();
        _primaryTemplateItems = BuildDataStructureIndex(templateRoot);
        _templateCache = new Dictionary<string, DataStructureTemplate>(StringComparer.OrdinalIgnoreCase);
        _primaryTemplateName = TryGetTemplateName(dsRoot);
        if (!string.IsNullOrWhiteSpace(_primaryTemplateName))
        {
            _templateCache[_primaryTemplateName] = new DataStructureTemplate(
                _primaryTemplateName,
                description: null,
                itemsById: _primaryTemplateItems);
        }
        _eventVariables = new Dictionary<string, EventLevelVariable>(StringComparer.Ordinal);
        _outputLines = new List<string>();
    }

    /// <summary>
    /// Builds readable event rule output and populates related state such as event variables.
    /// </summary>
    /// <remarks>
    /// This method resets any previous output. Unsupported tags are skipped.
    /// </remarks>
    public void ConvertXmlToReadableEr()
    {
        ResetState();

        var root = EventXmlDocument.Root
            ?? throw new InvalidOperationException("Event XML root not found.");
        EventElements = root.Descendants();

        RootEventSpecKey = root.Attribute("szEventSpecKey")?.Value
                           ?? throw new InvalidOperationException("Event Spec Key Not Found");

        foreach (var block in EventElements)
        {
            InterpretXmlTag(block);
        }

        ReadableEventRule = _outputLines.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, _outputLines) + Environment.NewLine;
    }

    private void ResetState()
    {
        _indentLevel = 0;
        _outputLines.Clear();
        _eventVariables.Clear();
        RootEventSpecKey = string.Empty;
        ReadableEventRule = string.Empty;
    }

    private void InterpretXmlTag(XElement xmlEventRuleBlock)
    {
        switch (xmlEventRuleBlock.Name.LocalName)
        {
            case "GBREvent":
                break;
            case "GBRVAR":
                var variable = HandleGBRVAR(xmlEventRuleBlock);
                if (!string.IsNullOrWhiteSpace(variable.VariableId))
                {
                    _eventVariables[variable.VariableId] = variable;
                }
                break;
            case "GBRASSIGN":
                var assignment = HandleGBRASSIGN(xmlEventRuleBlock);
                AddIndentedLine(assignment);
                break;
            case "GBRSLBF":
                var text = HandleGBRSLBF(xmlEventRuleBlock);
                AddIndentedLine(text);
                break;
            case "GBROPTIONS":
                // Not implemented yet. Keep for future investigation.
                break;
            case "GBRCOMMENT":
                var comment = HandleGBRCOMMENT(xmlEventRuleBlock);
                AddIndentedLine(comment);
                break;
            case "GBRFileIOOp":
                var fileIoLines = HandleGBRFileIOOp(xmlEventRuleBlock);
                AddIndentedLines(fileIoLines);
                break;
            case "GBRCRIT":
                var crit = HandleGBRCRIT(xmlEventRuleBlock);
                AddIndentedLines(crit);
                IncreaseIndent();
                break;
            case "GBRBF":
                var bfLines = HandleGBRBF(xmlEventRuleBlock);
                AddIndentedLines(bfLines);
                break;
            case "GBREndIf":
                DecreaseIndent();
                AddIndentedLine("End If");
                break;
            case "GBREndWhile":
                DecreaseIndent();
                AddIndentedLine("End While");
                break;
            case "GBRElse":
                DecreaseIndent();
                AddIndentedLine("Else");
                IncreaseIndent();
                break;
            default:
                break;
        }
    }

    private static Dictionary<string, DataStructureTemplateItem> BuildDataStructureIndex(XElement templateRoot)
    {
        var templateItems = templateRoot.Descendants().Select(element => new DataStructureTemplateItem(element));

        var index = new Dictionary<string, DataStructureTemplateItem>(StringComparer.Ordinal);
        foreach (var item in templateItems)
        {
            // Preserve the first occurrence to keep deterministic mapping if duplicates exist.
            if (!index.ContainsKey(item.Id))
            {
                index[item.Id] = item;
            }
        }

        return index;
    }

    private void AddIndentedLine(string line)
    {
        _outputLines.Add(IndentLine(line, _indentLevel));
    }

    private void AddIndentedLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            _outputLines.Add(IndentLine(line, _indentLevel));
        }
    }

    private static string IndentLine(string line, int indentLevel)
    {
        var safeIndent = Math.Max(indentLevel, 0);
        return $"{new string('\t', safeIndent)}{line}";
    }

    private void IncreaseIndent()
    {
        _indentLevel += 1;
    }

    private void DecreaseIndent()
    {
        if (_indentLevel > 0)
        {
            _indentLevel -= 1;
        }
    }

    private static (string? Qualifier, string Remainder) SplitQualifier(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return (null, string.Empty);
        }

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return (null, trimmed);
        }

        if (!QualifierTokens.Contains(parts[0]))
        {
            return (null, trimmed);
        }

        return (parts[0].ToUpperInvariant(), parts[1].Trim());
    }

    private string? TryGetEventVariableName(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return _eventVariables.TryGetValue(id, out var variable) ? variable.VariableName : null;
    }

    // Spec XML can contain padding or non-XML bytes; normalize to the first element.
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
