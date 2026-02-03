// See https://aka.ms/new-console-template for more information

using JdeClient.Core;
using JdeClient.Core.Models;
using JdeClient.Core.XmlEngine;

const string TestBusinessFunction = "N00101";

using var client = new JdeClient.Core.JdeClient();
await client.ConnectAsync();

var objects = await client.GetObjectsAsync(
    JdeObjectType.BusinessFunction,
    searchPattern: TestBusinessFunction,
    maxResults: 1);

if (objects.Count == 0)
{
    Console.Error.WriteLine($"No business function found for '{TestBusinessFunction}'.");
    return;
}

var tree = await client.GetEventRulesTreeAsync(objects[0]);
var targetNode = FindFirstNodeWithEventRules(tree);

if (targetNode == null)
{
    Console.Error.WriteLine($"No event rules found for '{TestBusinessFunction}'.");
    return;
}

if (string.IsNullOrWhiteSpace(targetNode.DataStructureName))
{
    Console.Error.WriteLine($"No data structure template found for '{TestBusinessFunction}'.");
    return;
}

var eventDocs = await client.GetEventRulesXmlAsync(targetNode.EventSpecKey!);
var dsDocs = await client.GetDataStructureXmlAsync(targetNode.DataStructureName);

if (eventDocs.Count == 0 || dsDocs.Count == 0)
{
    Console.Error.WriteLine("Unable to load XML specs for event rules or data structure template.");
    return;
}

var resolver = new JdeSpecResolver(client);
var xmlEngine = new JdeXmlEngine(eventDocs[0].Xml, dsDocs[0].Xml, resolver);
xmlEngine.ConvertXmlToReadableEr();

Console.Write(xmlEngine.ReadableEventRule);

static JdeEventRulesNode? FindFirstNodeWithEventRules(JdeEventRulesNode node)
{
    if (node.HasEventRules && !string.IsNullOrWhiteSpace(node.DataStructureName))
    {
        return node;
    }

    foreach (var child in node.Children)
    {
        var match = FindFirstNodeWithEventRules(child);
        if (match != null)
        {
            return match;
        }
    }

    return node.HasEventRules ? node : null;
}
