using JdeClient.Core.Models;

namespace JdeClient.Core.IntegrationTests;

public class ObjectCatalogTests
{
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
    public async Task GetObjectsAsync_WithBusinessViewFilter_ReturnsBusinessViews()
    {
        await EnsureConnectedAsync();

        var views = await _client.GetObjectsAsync(JdeObjectType.BusinessView, maxResults: 25);

        await Assert.That(views.Count).IsGreaterThan(0);
        await Assert.That(views.All(view =>
            string.Equals(view.ObjectType, "BSVW", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }
}
