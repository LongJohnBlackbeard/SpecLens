using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JdeClient.Core.XmlEngine;

public partial class JdeXmlEngine
{
    // ReSharper disable once InconsistentNaming
    private List<string> HandleGBRCRIT(XElement xmlEventRuleBlock)
    {
        var critText = xmlEventRuleBlock.Attribute("lpszCritDesc")?.Value;
        var typeAttribute = xmlEventRuleBlock.Attribute("type")?.Value;
        var type = NormalizeKeyword(string.IsNullOrWhiteSpace(typeAttribute) ? "If" : typeAttribute);
        var creHeader = xmlEventRuleBlock.Descendants(_xmlNamespace + "CRE_HEADER").FirstOrDefault()
            ?? throw new InvalidOperationException("CRE_HEADER node not found for criteria block.");
        var nodes = creHeader.Descendants(_xmlNamespace + "CRE_NODE").ToList();
        var statements = SplitIfRules(critText);
        var formattedStatements = new List<string>(capacity: Math.Min(nodes.Count, statements.Count));

        for (var index = 0; index < nodes.Count && index < statements.Count; index++)
        {
            var node = nodes[index];
            var statement = statements[index];
            var comparisonType = node.Attribute("eCompType")?.Value ?? "EQUAL";
            var comparisonString = comparisonType switch
            {
                "EQUAL" => ComparisonEqual,
                "NOT_EQ" => ComparisonNotEqual,
                "LE_OR_EQ" => ComparisonLessOrEqual,
                "GR" => ComparisonGreaterThan,
                "EQ_OR_EMPTY" => ComparisonEqualToOrEmpty,
                _ => ComparisonEqual
            };

            var objectAndPredicate = statement.Split(new[] { comparisonString }, 2, StringSplitOptions.None);
            if (objectAndPredicate.Length < 2)
            {
                formattedStatements.Add(statement);
                continue;
            }

            // Only the first clause should inherit the event type prefix.
            var defaultPrefix = index == 0 ? type : string.Empty;
            var (prefix, objectVariable) = ExtractPrefix(objectAndPredicate[0], defaultPrefix);
            var predicate = objectAndPredicate[1].Trim();

            var subjectValue = ResolveOperand(node, "zSubject", objectVariable, decodeLiteral: false);
            var predicateValue = ResolveOperand(node, "zPredicate", predicate, decodeLiteral: true);

            if (string.IsNullOrWhiteSpace(prefix) || prefix == "")
            {
                prefix = type;
            }

            var formattedStatement = $"{prefix} {subjectValue} {comparisonString} {predicateValue}";
            formattedStatements.Add(formattedStatement);
        }

        return formattedStatements;
    }

    private (string Prefix, string ObjectVariable) ExtractPrefix(string statement, string defaultPrefix)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            return (defaultPrefix, string.Empty);
        }

        var match = PrefixRegex.Match(statement);
        if (match.Success)
        {
            var prefix = NormalizeKeyword(match.Groups[1].Value);
            var remainder = statement[match.Length..].Trim();
            return (prefix, remainder);
        }

        return (defaultPrefix, statement.Trim());
    }

    private string NormalizeKeyword(string keyword)
    {
        return _textInfo.ToTitleCase(keyword.Trim().ToLowerInvariant());
    }

    private string ResolveOperand(XElement creNode, string tagName, string fallback, bool decodeLiteral)
    {
        var parent = creNode.Descendants(_xmlNamespace + tagName).FirstOrDefault();
        var child = parent?.Descendants().FirstOrDefault();
        if (child is null)
        {
            return fallback;
        }

        return child.Name.LocalName switch
        {
            "DSOBJMember" => ApplyQualifier(fallback, ResolveDataStructureMemberLabel(child)) ?? fallback,
            "DSOBJVariable" => ApplyQualifier(fallback, TryGetEventVariableName(child.Attribute("idVariable")?.Value)) ?? fallback,
            "DSOBJLiteral" => decodeLiteral ? WebUtility.HtmlDecode(fallback) : fallback,
            _ => fallback
        };
    }

    private static string? ApplyQualifier(string fallback, string? resolved)
    {
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }

        var (qualifier, _) = SplitQualifier(fallback);
        if (string.IsNullOrWhiteSpace(qualifier))
        {
            return resolved;
        }

        return $"{qualifier} {resolved}";
    }

    private List<string> SplitIfRules(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new List<string>();

        var rule = WebUtility.HtmlDecode(code).Trim();

        // 1) Protect comparator phrases that contain "or"
        rule = Regex.Replace(
            rule,
            @"\b(less\s+than)\s+or\s+(equal\s+to)\b",
            $"$1 {OrMarker} $2",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        rule = Regex.Replace(
            rule,
            @"\b(greater\s+than)\s+or\s+(equal\s+to)\b",
            $"$1 {OrMarker} $2",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        rule = Regex.Replace(
            rule,
            @"\b(equal\s+to)\s+or\s+(empty)\b",
            $"$1 {OrMarker} $2",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // 2) Split on logical AND/OR, keeping them via capturing group
        var raw = SplitOnAndOrRegex
            .Split(rule)                 // [clause1, "and", clause2, "or", clause3, ...]
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Select(t => t.Replace(OrMarker, "or")) // restore comparator phrase
            .ToList();

        if (raw.Count == 0)
            return new List<string>();

        // 3) Merge operator with the following clause (so operators aren't standalone tokens)
        var result = new List<string>(capacity: (raw.Count + 1) / 2);

        result.Add(raw[0]); // first clause as-is (often starts with "If ...")

        for (int i = 1; i + 1 < raw.Count; i += 2)
        {
            var op = raw[i].Equals("or", StringComparison.OrdinalIgnoreCase) ? "or" : "and";
            var clause = raw[i + 1];
            result.Add($"{op} {clause}");
        }

        return result;
    }
}
