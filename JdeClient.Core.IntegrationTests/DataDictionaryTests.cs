using JdeClient.Core.Models;

namespace JdeClient.Core.IntegrationTests;

public class DataDictionaryTests
{
    private const string KnownDataItem = "AN8";

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
    public async Task GetDataDictionariesAsync_WithKnownItem_ReturnsDetails()
    {
        await EnsureConnectedAsync();

        var results = await _client.GetDataDictionariesAsync(new[] { KnownDataItem });
        var item = results.FirstOrDefault(entry =>
            string.Equals(entry.DataItem, KnownDataItem, StringComparison.OrdinalIgnoreCase));

        await Assert.That(item).IsNotNull();
        if (item == null)
        {
            return;
        }

        await Assert.That(item.DataItem).IsEqualTo(KnownDataItem);
        await Assert.That(item.Length).IsGreaterThan(0);
        await Assert.That(item.TypeCode).IsNotEqualTo(default(char));
    }

    [Test]
    public async Task SearchDataDictionariesAsync_WithWildcard_ReturnsKnownItem()
    {
        await EnsureConnectedAsync();

        var results = await _client.SearchDataDictionariesAsync("AN*");
        var item = results.FirstOrDefault(entry =>
            string.Equals(entry.DataItem, KnownDataItem, StringComparison.OrdinalIgnoreCase));

        await Assert.That(item).IsNotNull();
        if (item == null)
        {
            return;
        }

        await Assert.That(item.DataItem).IsEqualTo(KnownDataItem);
        await Assert.That(item.Length).IsGreaterThan(0);
        await Assert.That(item.TypeCode).IsNotEqualTo(default(char));
    }

    [Test]
    public async Task GetDataDictionaryAsync_WithKnownItem_ReturnsDetails()
    {
        await EnsureConnectedAsync();

        var item = await _client.GetDataDictionaryAsync(KnownDataItem);

        await Assert.That(item).IsNotNull();
        if (item == null)
        {
            return;
        }

        await Assert.That(item.DataItem).IsEqualTo(KnownDataItem);
        await Assert.That(item.Length).IsGreaterThan(0);
    }
}
