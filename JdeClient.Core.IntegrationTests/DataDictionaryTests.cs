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
    public async Task GetDataDictionaryTitlesAsync_WithKnownItem_ReturnsTitle()
    {
        await EnsureConnectedAsync();

        var results = await _client.GetDataDictionaryTitlesAsync(new[] { KnownDataItem });
        var item = results.FirstOrDefault(entry =>
            string.Equals(entry.DataItem, KnownDataItem, StringComparison.OrdinalIgnoreCase));

        await Assert.That(item).IsNotNull();
        if (item == null)
        {
            return;
        }

        await Assert.That(item.DataItem).IsEqualTo(KnownDataItem);
        await Assert.That(string.IsNullOrWhiteSpace(item.Title1) && string.IsNullOrWhiteSpace(item.Title2)).IsFalse();
    }

    [Test]
    public async Task GetDataDictionaryItemNamesAsync_WithKnownItem_ReturnsName()
    {
        await EnsureConnectedAsync();

        var results = await _client.GetDataDictionaryItemNamesAsync(new[] { KnownDataItem });
        var item = results.FirstOrDefault(entry =>
            string.Equals(entry.DataItem, KnownDataItem, StringComparison.OrdinalIgnoreCase));

        await Assert.That(item).IsNotNull();
        if (item == null)
        {
            return;
        }

        await Assert.That(item.DataItem).IsEqualTo(KnownDataItem);
        await Assert.That(string.IsNullOrWhiteSpace(item.Name)).IsFalse();
    }

    [Test]
    public async Task GetDataDictionaryDetailsAsync_WithKnownItem_ReturnsDetails()
    {
        await EnsureConnectedAsync();

        var results = await _client.GetDataDictionaryDetailsAsync(new[] { KnownDataItem });
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
}
