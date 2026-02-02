using JdeClient.Core.Models;

namespace JdeClient.Core.IntegrationTests;

public class EventRulesTests
{
    private const string NerSearchPattern = "N*";

    private static readonly JdeClientOptions options = new JdeClientOptions
    {
        EnableDebug = false,
        EnableSpecDebug = false,
        EnableQueryDebug = false
    };

    private JdeClient _client { get; set; } = new(options);

    private async Task EnsureConnectedAsync()
    {
        if (_client.IsConnected)
        {
            return;
        }

        await _client.ConnectAsync();

        if (!_client.IsConnected)
        {
            throw new Exception("Failed to connect to JDE");
        }
    }

    [Test]
    public async Task GetEventRulesTreeAsync_WithNerBusinessFunction_ReturnsEventNodes()
    {
        await EnsureConnectedAsync();

        var found = await FindEventRulesNodeAsync(node => node.HasEventRules);
        await Assert.That(string.IsNullOrWhiteSpace(found.EventNode.EventSpecKey)).IsFalse();
    }

    [Test]
    public async Task GetFormattedEventRulesAsync_WithEventNode_ReturnsFormattedText()
    {
        await EnsureConnectedAsync();

        var found = await FindEventRulesNodeAsync(HasFormattedCandidate);
        var result = await _client.GetFormattedEventRulesAsync(found.EventNode);

        await Assert.That(result.EventSpecKey).IsEqualTo(found.EventNode.EventSpecKey);
        await Assert.That(string.IsNullOrWhiteSpace(result.Text)).IsFalse();
        await Assert.That(result.StatusMessage).IsEqualTo("Event rules loaded.");
    }

    private async Task<(JdeObjectInfo ObjectInfo, JdeEventRulesNode Root, JdeEventRulesNode EventNode)> FindEventRulesNodeAsync(
        Func<JdeEventRulesNode, bool> predicate)
    {
        var candidates = await _client.GetObjectsAsync(JdeObjectType.BusinessFunction, NerSearchPattern, maxResults: 200);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.ObjectName))
            {
                continue;
            }

            var root = await _client.GetEventRulesTreeAsync(candidate);
            var eventNode = Flatten(root).FirstOrDefault(predicate);
            if (eventNode != null)
            {
                return (candidate, root, eventNode);
            }
        }

        throw new InvalidOperationException($"No event rules were found for business functions matching {NerSearchPattern}.");
    }

    private static bool HasFormattedCandidate(JdeEventRulesNode node)
    {
        return node.HasEventRules && !string.IsNullOrWhiteSpace(ResolveTemplateName(node));
    }

    private static string ResolveTemplateName(JdeEventRulesNode node)
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

    private static IEnumerable<JdeEventRulesNode> Flatten(JdeEventRulesNode node)
    {
        yield return node;

        foreach (var child in node.Children)
        {
            foreach (var entry in Flatten(child))
            {
                yield return entry;
            }
        }
    }
}
