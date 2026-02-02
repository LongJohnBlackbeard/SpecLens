namespace JdeClient.Core.IntegrationTests;

public class DataSourceTests
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
    public async Task GetAvailableDataSourcesAsync_WhenConnected_ReturnsDataSources()
    {
        await EnsureConnectedAsync();

        var sources = await _client.GetAvailableDataSourcesAsync();

        await Assert.That(sources.Count).IsGreaterThan(0);
        await Assert.That(sources.All(source => !string.IsNullOrWhiteSpace(source.Name))).IsTrue();
    }

    [Test]
    public async Task GetDefaultTableDataSourceAsync_ForF0101_ReturnsKnownOrNull()
    {
        await EnsureConnectedAsync();

        var sources = await _client.GetAvailableDataSourcesAsync();
        var defaultSource = await _client.GetDefaultTableDataSourceAsync("F0101");

        bool matchesList = string.IsNullOrWhiteSpace(defaultSource)
            || sources.Any(source => string.Equals(source.Name, defaultSource, StringComparison.OrdinalIgnoreCase));

        await Assert.That(matchesList).IsTrue();
    }
}
