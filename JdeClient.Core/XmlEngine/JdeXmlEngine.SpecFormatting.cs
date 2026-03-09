using System.Linq;
using System.Xml.Linq;
using JdeClient.Core.XmlEngine.Models;

namespace JdeClient.Core.XmlEngine;

public partial class JdeXmlEngine
{
    // Spec-backed tag handlers
    private IEnumerable<string> HandleGBRFileIOOp(XElement xmlEventRuleBlock)
    {
        if (_specResolver == null)
        {
            // Spec-backed formatting requires JDE runtime; skip when unavailable.
            return Array.Empty<string>();
        }

        var fileIo = xmlEventRuleBlock.Descendants(_xmlNamespace + "DSOBJFileIO").FirstOrDefault();
        var tableName = fileIo?.Attribute("Name")?.Value ?? "UnknownTable";
        var operation = FormatFileIoOperation(xmlEventRuleBlock.Attribute("operation")?.Value);
        var indexId = xmlEventRuleBlock.Attribute("indexId")?.Value;

        var dataDictionaryItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paramItems = xmlEventRuleBlock.Elements(_xmlNamespace + "GBRParam")
            .Select(param => param.Element(_xmlNamespace + "DSItem"))
            .Where(item => item != null)
            .ToList();

        foreach (var item in paramItems)
        {
            var tableColumn = item!
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Dbref");
            var dataItem = tableColumn?.Attribute("szDict")?.Value;
            if (!string.IsNullOrWhiteSpace(dataItem))
            {
                dataDictionaryItems.Add(dataItem);
            }
        }

        var indexes = _specResolver.GetTableIndexes(tableName);
        foreach (var index in indexes)
        {
            foreach (var key in index.KeyColumns)
            {
                dataDictionaryItems.Add(key);
            }
        }

        var ddTitlesByItem = _specResolver.GetDataDictionaryTitles(dataDictionaryItems);
        var header = $"{tableName}.{operation}{FormatIndexLabel(tableName, indexId, ddTitlesByItem)}";

        var lines = new List<string> { header };
        foreach (var param in xmlEventRuleBlock.Elements(_xmlNamespace + "GBRParam"))
        {
            var dsItem = param.Element(_xmlNamespace + "DSItem");
            if (dsItem == null)
            {
                continue;
            }

            var copyWord = dsItem.Attribute("copyWord")?.Value;
            var dataItemHint = dsItem.Attribute("dataItem")?.Value;
            var fromElement = dsItem.Descendants(_xmlNamespace + "DsObjFrom").FirstOrDefault()?.Elements().FirstOrDefault();
            var toElement = dsItem.Descendants(_xmlNamespace + "DsObjTo").FirstOrDefault()?.Elements().FirstOrDefault();

            if (fromElement == null || toElement == null)
            {
                continue;
            }

            var sourceLabel = ResolveEventOperandLabel(fromElement, dataItemHint, defaultQualifier: "BF");
            var targetLabel = ResolveTableColumnLabel(toElement, ddTitlesByItem);
            var paramLine = FormatFileIoParamLine(copyWord, sourceLabel, targetLabel);
            lines.Add($"\t{paramLine}");
        }

        return lines;
    }

    private IEnumerable<string> HandleGBRBF(XElement xmlEventRuleBlock)
    {
        if (_specResolver == null)
        {
            // Spec-backed formatting requires JDE runtime; skip when unavailable.
            return Array.Empty<string>();
        }

        var functionName = xmlEventRuleBlock.Attribute("szFuncName")?.Value ?? "UnknownFunction";
        var templateName = xmlEventRuleBlock.Attribute("szTmplName")?.Value ?? string.Empty;
        var bsfnName = _specResolver.ResolveBusinessFunctionName(templateName) ?? templateName;

        var header = $"{functionName}({bsfnName}.{functionName})";
        var lines = new List<string> { header };

        var template = GetTemplate(templateName);
        foreach (var param in xmlEventRuleBlock.Elements(_xmlNamespace + "ERPARAM"))
        {
            var copyWord = param.Attribute("wCopyWord")?.Value;
            var paramId = param.Attribute("idItem")?.Value;
            var paramItem = template?.TryGetItem(paramId);
            var parameterLabel = paramItem?.GetFormattedName() ?? $"Param {paramId}";
            var valueElement = param.Elements().FirstOrDefault();
            if (valueElement == null)
            {
                continue;
            }

            var eventLabel = ResolveEventOperandLabel(valueElement, qualifierHint: null, defaultQualifier: "BF");
            var paramLine = FormatBusinessFunctionParamLine(copyWord, eventLabel, parameterLabel);
            lines.Add($"\t{paramLine}");
        }

        return lines;
    }

    internal static string FormatFileIoOperation(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return "Operation";
        }

        return operation.Trim().ToUpperInvariant() switch
        {
            "FETCH_SINGLE" => "FetchSingle",
            "FETCH_NEXT" => "FetchNext",
            "SELECT" => "Select",
            "DELETE" => "Delete",
            "UPDATE" => "Update",
            "INSERT" => "Insert",
            _ => operation.Replace("_", string.Empty)
        };
    }

    private string FormatIndexLabel(string tableName, string? indexId, IReadOnlyDictionary<string, string> ddTitlesByItem)
    {
        if (string.IsNullOrWhiteSpace(indexId))
        {
            return string.Empty;
        }

        if (!int.TryParse(indexId, out var idValue))
        {
            return $" [Index {indexId}]";
        }

        var indexes = _specResolver!.GetTableIndexes(tableName);
        var index = indexes.FirstOrDefault(info => info.Id == idValue);
        if (index == null)
        {
            return $" [Index {indexId}]";
        }

        var keyLabels = index.KeyColumns
            .Select(key => ddTitlesByItem.TryGetValue(key, out var title) ? title : key)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToList();

        if (keyLabels.Count == 0)
        {
            return $" [Index {indexId}]";
        }

        var keyLabel = string.Join(", ", keyLabels);
        return $" [Index {indexId}: {keyLabel}]";
    }

    internal static string FormatFileIoParamLine(string? copyWord, string source, string target)
    {
        var direction = copyWord?.Trim().ToUpperInvariant();
        return direction switch
        {
            "OUT" => $"{source} <- {target}",
            "IN" => $"{source} -> {target}",
            _ => $"{source} = {target}"
        };
    }

    internal static string FormatBusinessFunctionParamLine(string? copyWord, string eventLabel, string paramLabel)
    {
        var direction = copyWord?.Trim().ToUpperInvariant();
        return direction switch
        {
            "OUT" => $"{eventLabel} <- {paramLabel}",
            "INOUT" => $"{eventLabel} <-> {paramLabel}",
            _ => $"{eventLabel} -> {paramLabel}"
        };
    }

    private string ResolveTableColumnLabel(XElement columnElement, IReadOnlyDictionary<string, string> ddTitlesByItem)
    {
        var dbRef = columnElement.Descendants().FirstOrDefault(element => element.Name.LocalName == "Dbref");
        var dict = dbRef?.Attribute("szDict")?.Value;
        if (string.IsNullOrWhiteSpace(dict))
        {
            return "UnknownColumn";
        }

        var title = ddTitlesByItem.TryGetValue(dict, out var label) ? label : dict;
        return $"{title} [{dict}]";
    }

    private string ResolveEventOperandLabel(XElement operandElement, string? qualifierHint, string? defaultQualifier = null)
    {
        var (qualifier, remainder) = SplitQualifier(qualifierHint ?? string.Empty);
        var effectiveQualifier = qualifier;

        switch (operandElement.Name.LocalName)
        {
            case "DSOBJMember":
                var memberLabel = ResolveDataStructureMemberLabel(operandElement);
                if (!string.IsNullOrWhiteSpace(remainder))
                {
                    string? alias = ResolveDataStructureItem(operandElement)?.Alias;
                    return PrefixQualifierIfNeeded(effectiveQualifier ?? defaultQualifier, EnsureAliasSuffix(remainder, alias));
                }

                return PrefixQualifierIfNeeded(effectiveQualifier ?? defaultQualifier, memberLabel ?? "Member");
            case "DSOBJVariable":
                return ResolveVariableOperandLabel(operandElement, remainder, effectiveQualifier ?? "VA");
            case "DSOBJSystemVariable":
                var systemValue = remainder ?? operandElement.Attribute("idVariable")?.Value ?? "SystemVariable";
                return PrefixQualifierIfNeeded(effectiveQualifier ?? "SV", systemValue);
            case "DSOBJConstant":
                var constantValue = remainder ?? operandElement.Attribute("idConstant")?.Value ?? "Constant";
                return PrefixQualifierIfNeeded(effectiveQualifier ?? "CO", constantValue);
            case "DSOBJBSTableColumn":
                return ResolveBusinessViewColumnLabel(operandElement, remainder, effectiveQualifier ?? "BC");
            case "DSOBJTableColumn":
                return ResolveTableColumnOperandLabel(operandElement, remainder, effectiveQualifier);
            case "DSOBJGridColumn":
                return ResolveControlOperandLabel(
                    operandElement,
                    remainder,
                    effectiveQualifier ?? "GC",
                    "idObject");
            case "DSOBJFormControl":
                return ResolveControlOperandLabel(
                    operandElement,
                    remainder,
                    effectiveQualifier ?? "FC",
                    "idObject");
            case "DSOBJLiteral":
                return FormatLiteralValue(operandElement);
            case "DSOBJExpression":
                return remainder ?? operandElement.Descendants()
                    .Attributes("text")
                    .FirstOrDefault()?.Value
                    ?? operandElement.Value;
            default:
                return remainder ?? operandElement.Value;
        }
    }

    internal static string FormatLiteralValue(XElement literalElement)
    {
        var valueElement = literalElement.Descendants().FirstOrDefault(element => element.Name.LocalName.StartsWith("Literal", StringComparison.OrdinalIgnoreCase));
        if (valueElement == null)
        {
            return literalElement.Value.Trim();
        }

        var value = valueElement.Value.Trim();
        if (valueElement.Name.LocalName.Equals("LiteralString", StringComparison.OrdinalIgnoreCase))
        {
            return $"\"{value}\"";
        }

        return value;
    }

    internal static string PrefixQualifier(string qualifier, string value)
    {
        if (string.IsNullOrWhiteSpace(qualifier))
        {
            return value;
        }

        return $"{qualifier} {value}";
    }

    private string ResolveVariableOperandLabel(XElement operandElement, string? fallback, string qualifier)
    {
        string? variableId = operandElement.Attribute("idVariable")?.Value;
        string? alias = operandElement.Attribute("szDict")?.Value;
        string? variableName = TryGetEventVariableName(variableId);
        string value = EnsureAliasSuffix(
            variableName ?? fallback ?? alias ?? "Variable",
            alias);
        return PrefixQualifierIfNeeded(qualifier, value);
    }

    private string ResolveBusinessViewColumnLabel(XElement operandElement, string? fallback, string qualifier)
    {
        var (tableName, alias) = GetDbRefInfo(operandElement);
        string baseLabel = GetDataDictionaryTitle(alias) ??
                           ExtractReadableOperandName(fallback) ??
                           alias ??
                           "Column";
        string tableLabel = string.IsNullOrWhiteSpace(tableName) ? string.Empty : $" ({tableName}.0)";
        string aliasLabel = string.IsNullOrWhiteSpace(alias) ? string.Empty : $" [{alias}]";
        return $"{qualifier} {baseLabel}{tableLabel}{aliasLabel}".Trim();
    }

    private string ResolveTableColumnOperandLabel(XElement operandElement, string? fallback, string? qualifier)
    {
        var (tableName, alias) = GetDbRefInfo(operandElement);
        string baseLabel = GetDataDictionaryTitle(alias) ??
                           ExtractReadableOperandName(fallback) ??
                           alias ??
                           "Column";
        string tableLabel = string.IsNullOrWhiteSpace(tableName) ? string.Empty : $" ({tableName})";
        string aliasLabel = string.IsNullOrWhiteSpace(alias) ? string.Empty : $" [{alias}]";
        string value = $"{baseLabel}{tableLabel}{aliasLabel}".Trim();
        return PrefixQualifierIfNeeded(qualifier, value);
    }

    private static string ResolveControlOperandLabel(
        XElement operandElement,
        string? fallback,
        string qualifier,
        string idAttributeName)
    {
        string? label = string.IsNullOrWhiteSpace(fallback)
            ? operandElement.Attribute(idAttributeName)?.Value
            : fallback;
        return PrefixQualifierIfNeeded(qualifier, label ?? "Control");
    }

    private string? GetDataDictionaryTitle(string? alias)
    {
        if (_specResolver == null || string.IsNullOrWhiteSpace(alias))
        {
            return null;
        }

        return _specResolver.GetDataDictionaryTitles(new[] { alias })
            .TryGetValue(alias, out string? title)
            ? title
            : null;
    }

    private static (string? TableName, string? Alias) GetDbRefInfo(XElement operandElement)
    {
        XElement? dbRef = operandElement.Descendants()
            .FirstOrDefault(element => element.Name.LocalName.Equals("Dbref", StringComparison.OrdinalIgnoreCase));
        return (dbRef?.Attribute("szTable")?.Value, dbRef?.Attribute("szDict")?.Value);
    }

    private static string ExtractReadableOperandName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var (_, remainder) = SplitQualifier(value);
        string normalized = remainder.Trim();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+\[[^\]]+\]\s*$", string.Empty);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+\([^)]+\)\s*$", string.Empty);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+\([^)]+\)\s*$", string.Empty);
        return normalized.Trim();
    }

    private static string EnsureAliasSuffix(string? label, string? alias)
    {
        string normalized = label?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || string.IsNullOrWhiteSpace(alias))
        {
            return normalized;
        }

        return normalized.Contains($"[{alias}]", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"{normalized} [{alias}]";
    }

    private static string PrefixQualifierIfNeeded(string? qualifier, string value)
    {
        if (string.IsNullOrWhiteSpace(qualifier))
        {
            return value;
        }

        return value.StartsWith($"{qualifier} ", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{qualifier} {value}";
    }

    private DataStructureTemplate? GetTemplate(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return null;
        }

        if (_templateCache.TryGetValue(templateName, out var cached))
        {
            return cached;
        }

        if (_specResolver == null)
        {
            return null;
        }

        var template = _specResolver.GetDataStructureTemplate(templateName);
        if (template != null)
        {
            _templateCache[templateName] = template;
        }

        return template;
    }

    private DataStructureTemplateItem? ResolveDataStructureItem(XElement memberElement)
    {
        var id = memberElement.Attribute("idItem")?.Value;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var templateName = memberElement.Attribute("szTmplName")?.Value;
        if (!string.IsNullOrWhiteSpace(templateName))
        {
            var template = GetTemplate(templateName);
            var item = template?.TryGetItem(id);
            if (item != null)
            {
                return item;
            }
        }

        if (!string.IsNullOrWhiteSpace(_primaryTemplateName) &&
            _templateCache.TryGetValue(_primaryTemplateName, out var primaryTemplate))
        {
            var item = primaryTemplate.TryGetItem(id);
            if (item != null)
            {
                return item;
            }
        }

        return _primaryTemplateItems.TryGetValue(id, out var fallbackItem) ? fallbackItem : null;
    }

    private string? ResolveDataStructureMemberLabel(XElement memberElement)
    {
        return ResolveDataStructureItem(memberElement)?.GetFormattedName();
    }

    private static string? TryGetTemplateName(XElement root)
    {
        var candidateAttributes = new[]
        {
            "szTmplName",
            "szTemplateName",
            "szName"
        };

        foreach (var name in candidateAttributes)
        {
            var value = root.Attribute(name)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        var descendant = root.Descendants()
            .Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName.Equals("szTmplName", StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(descendant?.Value) ? null : descendant?.Value.Trim();
    }
}
