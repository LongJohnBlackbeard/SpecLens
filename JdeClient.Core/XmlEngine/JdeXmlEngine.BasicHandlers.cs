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
        var commentText = xmlEventRuleBlock.Attribute("comment_text")?.Value ?? "ERROR";
        // When comments are too long in ER it is sent to the next line yet in XML it is the same object.
        // Thus, Carriage returns are in the middle of the string. Remove them here and handle wrapping in the UI.
        commentText = commentText.Replace("\r", "").Replace("\n", "");
        return commentText;
    }

    // ReSharper disable once InconsistentNaming
    private string HandleGBRASSIGN(XElement xmlEventRuleBlock)
    {
        // Get Assign Type and Text string
        var textString = xmlEventRuleBlock.Attribute("textString")?.Value ?? "ERROR";

        if (textString == "ERROR")
            return textString;

        // Get Obj From and Obj To Sections
        var objTo = xmlEventRuleBlock.Descendants(_xmlNamespace + "ObjTo").FirstOrDefault();
        var objFrom = xmlEventRuleBlock.Descendants(_xmlNamespace + "ObjFrom").FirstOrDefault();
        // Get the inner object
        var objToInner = objTo?.Descendants().FirstOrDefault();
        var objFromInner = objFrom?.Descendants().FirstOrDefault();

        // Split the textString to get each side of the assignment
        var assignmentParts = textString.Split('=', 2);
        var targetVariable = assignmentParts.Length > 0 ? assignmentParts[0].Trim() : string.Empty;
        var assignedValue = assignmentParts.Length > 1 ? assignmentParts[1].Trim() : string.Empty;
        var assignmentPartOne = "";
        var assignmentPartTwo = "";
        // TODO: At this point, the inner object elements could be different, handle them accordingly
        switch (objToInner?.Name.LocalName)
        {
            case "DSOBJLiteral":
                // Not expected for assignment targets.
                break;
            case "DSOBJVariable":
                // Get the inner elements DD type
                var targetDataDictionary = objToInner.Attribute("szDict")?.Value ?? "N/A";
                assignmentPartOne = $"{targetVariable} [{targetDataDictionary}] = ";
                break;
            case "DSOBJMember":
                // Get Element Id Attribute and find from the DS List
                var dsItem = ResolveDataStructureItem(objToInner);
                var targetAlias = dsItem?.Alias ?? "N/A";
                assignmentPartOne = $"{targetVariable} [{targetAlias}] = ";
                break;
            // TODO: There could be other element types here. Will find out when looking at other ER Object Types
            default:
                assignmentPartOne = $"{targetVariable} = ";
                break;
        }

        assignmentPartTwo = assignedValue;
        switch (objFromInner?.Name.LocalName)
        {
            case "DSOBJLiteral":
                assignmentPartTwo = WebUtility.HtmlDecode(assignmentPartTwo);
                break;
            case "DSOBJVariable":
                var assignedDataDictionary = objFromInner.Attribute("szDict")?.Value ?? "N/A";
                assignmentPartTwo = $"{assignmentPartTwo} [{assignedDataDictionary}]";
                break;
            case "DSOBJMember":
                var dsItem = ResolveDataStructureItem(objFromInner);
                var assignedAlias = dsItem?.Alias ?? "N/A";
                assignmentPartTwo = $"{assignmentPartTwo} [{assignedAlias}]";
                break;
            // TODO: There could be other element types here. Will find out when looking at other ER Object Types
            default:
                break;
        }

        var completeAssignment = assignmentPartOne + assignmentPartTwo;
        return completeAssignment;
    }
}
