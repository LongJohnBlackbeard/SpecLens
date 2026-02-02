using JdeClient.Core.Models;

namespace JdeClient.Core.IntegrationTests;

public class QueryTests
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
    public async Task QueryTableAsync_F9860WithTypeFilter_ReturnsRows()
    {
        await EnsureConnectedAsync();

        var filters = new[]
        {
            new JdeFilter
            {
                ColumnName = "FUNO",
                Operator = JdeFilterOperator.Equals,
                Value = "TBLE"
            }
        };

        var result = await _client.QueryTableAsync("F9860", filters, maxRows: 5);

        await Assert.That(result.TableName).IsEqualTo("F9860");
        await Assert.That(result.Rows.Count).IsGreaterThan(0);
        await Assert.That(result.Rows.Count).IsLessThanOrEqualTo(5);
        await Assert.That(result.ColumnNames.Any(name => string.Equals(name, "OBNM", StringComparison.OrdinalIgnoreCase))).IsTrue();

        foreach (var row in result.Rows)
        {
            await Assert.That(TryGetValue(row, "FUNO", out var value)).IsTrue();
            await Assert.That(string.IsNullOrWhiteSpace(value)).IsFalse();
            await Assert.That(value!.Trim()).IsEqualTo("TBLE");
        }
    }

    [Test]
    public async Task QueryTableCountAsync_F9860_ReturnsCount()
    {
        await EnsureConnectedAsync();

        var count = await _client.QueryTableCountAsync("F9860");

        await Assert.That(count).IsGreaterThan(0);
    }

    [Test]
    public async Task QueryTableCountAsync_F9860WithTypeFilter_ReturnsCount()
    {
        await EnsureConnectedAsync();

        var filters = new[]
        {
            new JdeFilter
            {
                ColumnName = "FUNO",
                Operator = JdeFilterOperator.Equals,
                Value = "TBLE"
            }
        };

        var count = await _client.QueryTableCountAsync("F9860", filters);

        await Assert.That(count).IsGreaterThan(0);
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, object> row, string column, out string? value)
    {
        if (row.TryGetValue(column, out var direct))
        {
            value = direct?.ToString();
            return true;
        }

        foreach (var entry in row)
        {
            if (!string.Equals(entry.Key, column, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = entry.Value?.ToString();
            return true;
        }

        value = null;
        return false;
    }
}
