using System.Linq;
using System.Net;
using System.Xml.Linq;
using JdeClient.Core.XmlEngine.Models;

namespace JdeClient.Core.XmlEngine;

public partial class JdeXmlEngine
{
    // ReSharper disable once InconsistentNaming
    private EventLevelVariable HandleGBRVAR(XElement xmlEventRuleBlock)
    {
        // <GBRVAR szVariableName="evt_szBlankLotSerialNum_LOTN">
        //     <DSOBJVariable idVariable="1" szDict="LOTN" wStyle="32" dataType="String" size="30" />
        // </GBRVAR>

        var name = xmlEventRuleBlock.Attribute("szVariableName")?.Value ?? "Could Not Parse Variable Name";
        var dsobjVariable = xmlEventRuleBlock.Descendants(_xmlNamespace + "DSOBJVariable").First();
        var dict = dsobjVariable.Attribute("szDict")?.Value ?? "N/A";
        var id = dsobjVariable.Attribute("idVariable")?.Value ?? string.Empty;

        return new EventLevelVariable
        {
            VariableId = id,
            VariableName = $"{name} [{dict}]",
            Alias = dict
        };
    }

    // ReSharper disable once InconsistentNaming
    // This is for NER Specific functions. Returning just the text for now until more information is gathered.
    private string HandleGBRSLBF(XElement xmlEventRuleBlock)
    {

        var summaryText = xmlEventRuleBlock.Attribute("summary_text")?.Value ?? "ERROR";
        summaryText = summaryText.Replace("&quot;", "\"");
        return summaryText;
    }

    // ReSharper disable once InconsistentNaming
    private string HandleGBRCOMMENT(XElement xmlEventRuleBlock)
    {
        var commentText = xmlEventRuleBlock.Value ?? "ERROR";
        // When comments are too long in ER it is sent to the next line yet in XML it is the same object.
        // Thus, Carriage returns are in the middle of the string. Remove them here and handle wrapping in the UI.
        commentText = commentText.Replace("\r", "").Replace("\n", "");
        return commentText;
    }

    // ReSharper disable once InconsistentNaming
    private string HandleGBRASSIGN(XElement xmlEventRuleBlock)
    {
        var textString = xmlEventRuleBlock.Attribute("textString")?.Value ?? "ERROR";
        if (textString == "ERROR")
        {
            return textString;
        }

        var objTo = xmlEventRuleBlock.Descendants(_xmlNamespace + "ObjTo").FirstOrDefault();
        var objFrom = xmlEventRuleBlock.Descendants(_xmlNamespace + "ObjFrom").FirstOrDefault();
        var objToInner = objTo?.Descendants().FirstOrDefault();
        var objFromInner = objFrom?.Descendants().FirstOrDefault();

        var assignmentParts = textString.Split('=', 2);
        var targetVariable = assignmentParts.Length > 0 ? assignmentParts[0].Trim() : string.Empty;
        var assignedValue = assignmentParts.Length > 1 ? assignmentParts[1].Trim() : string.Empty;
        string assignmentPartOne = ResolveAssignmentOperandLabel(objToInner, targetVariable, isTarget: true);
        string assignmentPartTwo = ResolveAssignmentOperandLabel(objFromInner, assignedValue, isTarget: false);
        return $"{assignmentPartOne} = {assignmentPartTwo}";
    }

    private string ResolveAssignmentOperandLabel(XElement? operandElement, string fallback, bool isTarget)
    {
        if (operandElement == null)
        {
            return fallback;
        }

        switch (operandElement.Name.LocalName)
        {
            case "DSOBJLiteral":
                return string.IsNullOrWhiteSpace(fallback)
                    ? FormatLiteralValue(operandElement)
                    : WebUtility.HtmlDecode(fallback);
            case "DSOBJVariable":
                var variableAlias = operandElement.Attribute("szDict")?.Value;
                return EnsureAliasSuffix(ResolveEventOperandLabel(operandElement, fallback), variableAlias);
            case "DSOBJMember":
                var dsItem = ResolveDataStructureItem(operandElement);
                return EnsureAliasSuffix(fallback, dsItem?.Alias);
            case "DSOBJBSTableColumn":
                return ResolveBusinessViewColumnLabel(
                    operandElement,
                    fallback,
                    SplitQualifier(fallback).Qualifier ?? "BC");
            case "DSOBJTableColumn":
                return ResolveTableColumnOperandLabel(
                    operandElement,
                    fallback,
                    SplitQualifier(fallback).Qualifier);
            case "DSOBJGridColumn":
            case "DSOBJFormControl":
                return string.IsNullOrWhiteSpace(fallback)
                    ? ResolveEventOperandLabel(operandElement, fallback)
                    : fallback;
            case "DSOBJExpression":
                return fallback;
            default:
                return isTarget ? fallback : WebUtility.HtmlDecode(fallback);
        }
    }
}
