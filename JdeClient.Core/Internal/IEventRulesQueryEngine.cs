using JdeClient.Core.Models;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Abstraction for retrieving event rules trees, lines, and XML from specs.
/// </summary>
internal interface IEventRulesQueryEngine
{
    /// <summary>
    /// Build the event rules tree for a business function (BSFN).
    /// </summary>
    JdeEventRulesNode GetBusinessFunctionTree(string objectName);

    /// <summary>
    /// Build the event rules tree for a named event rule (NER).
    /// </summary>
    JdeEventRulesNode GetNamedEventRuleTree(string objectName);

    /// <summary>
    /// Build the event rules tree for an interactive application (APPL).
    /// </summary>
    JdeEventRulesNode GetApplicationEventRulesTree(string objectName);

    /// <summary>
    /// Build the event rules tree for a batch application/report (UBE).
    /// </summary>
    JdeEventRulesNode GetReportEventRulesTree(string objectName);

    /// <summary>
    /// Build the event rules tree for a table (TBLE).
    /// </summary>
    JdeEventRulesNode GetTableEventRulesTree(string objectName);

    /// <summary>
    /// Retrieve diagnostics about decoding event rules specs.
    /// </summary>
    IReadOnlyList<JdeEventRulesDecodeDiagnostics> GetEventRulesDecodeDiagnostics(string eventSpecKey);

    /// <summary>
    /// Retrieve event rule lines for a spec key.
    /// </summary>
    IReadOnlyList<JdeEventRuleLine> GetEventRulesLines(string eventSpecKey);

    /// <summary>
    /// Retrieve event rules XML documents for a spec key.
    /// </summary>
    IReadOnlyList<JdeEventRulesXmlDocument> GetEventRulesXmlDocuments(string eventSpecKey);

    /// <summary>
    /// Retrieve event rules XML documents for a spec key at an explicit spec location.
    /// </summary>
    IReadOnlyList<JdeEventRulesXmlDocument> GetEventRulesXmlDocuments(
        string eventSpecKey,
        JdeSpecLocation location,
        string? dataSourceOverride);

    /// <summary>
    /// Retrieve data structure XML documents for a template.
    /// </summary>
    IReadOnlyList<JdeSpecXmlDocument> GetDataStructureXmlDocuments(string templateName);

    /// <summary>
    /// Retrieve data structure XML documents for a template at an explicit spec location.
    /// </summary>
    IReadOnlyList<JdeSpecXmlDocument> GetDataStructureXmlDocuments(
        string templateName,
        JdeSpecLocation location,
        string? dataSourceOverride);

    /// <summary>
    /// Retrieve C business function source payload/documents from BUSFUNC specs.
    /// </summary>
    IReadOnlyList<JdeBusinessFunctionCodeDocument> GetBusinessFunctionCodeDocuments(string objectName, string? functionName);

    /// <summary>
    /// Retrieve C business function source payload/documents from BUSFUNC specs with explicit location selection.
    /// </summary>
    IReadOnlyList<JdeBusinessFunctionCodeDocument> GetBusinessFunctionCodeDocuments(
        string objectName,
        string? functionName,
        JdeBusinessFunctionCodeLocation location,
        string? dataSourceOverride);
}
