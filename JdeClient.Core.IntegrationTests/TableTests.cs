using JdeClient.Core.Models;

namespace JdeClient.Core.IntegrationTests;

public class TableTests
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
            return;

        await _client.ConnectAsync();

        if (!_client.IsConnected)
            throw new Exception("Failed to connect to JDE");
    }

    [Test]
    public async Task EnsureConnectedAsync_WhenDisconnected_ConnectsClient()
    {
        await EnsureConnectedAsync();
        await Assert.That(_client.IsConnected).IsTrue();
    }
    
    
    #region GetObjectsAsync() Tests

    [Test]
    public async Task GetObjectsAsync_AnyObject50MaxResults_ReturnsFiftyObjects()
    {
        // Arrange
        await EnsureConnectedAsync();

        // Act
        var objects = await _client.GetObjectsAsync(JdeObjectType.All, maxResults: 50);

        // Assert
        await Assert.That(objects.Count).IsEqualTo(50);

    }

    [Test]
    public async Task GetObjectsAsync_WithBSFNFilter_ReturnsManyBSFNObjects()
    {
        // Arrange
        await EnsureConnectedAsync();

        // Act
        var businessFunctions = await _client.GetObjectsAsync(JdeObjectType.BusinessFunction);

        // Assert
        await Assert.That(businessFunctions.Count).IsGreaterThan(1);
        await Assert.That(businessFunctions.All(x => string.Equals(x.ObjectType, "BSFN", StringComparison.OrdinalIgnoreCase))).IsTrue();
    }

    [Test]
    public async Task GetObjectsAsync_WithDescriptionPattern_ReturnsSpecificObjects()
    {
        // Arrange
        await EnsureConnectedAsync();

        var expectedResultList = new List<JdeObjectInfo>();
        
        var workOrderTableObject = new JdeObjectInfo()
        {
            Description = "Work Order Master File",
            ObjectName = "F4801",
            ObjectType = "TBLE",
            ProductCode = null,
            Status = null,
            SystemCode = "48"
        };

        var pricingWorkBenchObject = new JdeObjectInfo()
        {
            Description = "Pricing WorkBench",
            ObjectName = "P45501",
            ObjectType = "APPL",
            ProductCode = null,
            Status = null,
            SystemCode = "45"
        };
        
        expectedResultList.Add(workOrderTableObject);
        expectedResultList.Add(pricingWorkBenchObject);

        // Act
        var results = await _client.GetObjectsAsync(JdeObjectType.All, null, "*Work*");

        // Assert
        await Assert.That(results.Any(x => x.ObjectName == "F4801")).IsTrue();

    }
    
    #endregion
    
    #region GetTableInfoAsync() Tests
    
    [Test]
    public async Task GetTableInfoAsync_F0101_ReturnsF0101TableInfo()
    {
        // Arrange
        await EnsureConnectedAsync();
        // Act
        var table = await _client.GetTableInfoAsync("F0101");
        // Assert
        await Assert.That(table.TableName).IsEqualTo("F0101");
        await Assert.That(table.Columns.Count).IsEqualTo(95);
        await Assert.That(table.ToString()).IsEqualTo("F0101 (95 columns)");
        await Assert.That(table.Description).IsEqualTo("Address Book Master");
        await Assert.That(table.Columns.Any(x => x.Name == "AN8")).IsTrue();
        await Assert.That(table.Columns.Any(x => x.Name == "ALKY")).IsTrue();
        await Assert.That(table.Columns.Any(x => x.SqlName == "ABAN8")).IsTrue();
        await Assert.That(table.Columns.Any(x => x.SqlName == "ABALKY")).IsTrue();
    }
    #endregion

    #region GetBusinessViewInfoAsync() Tests

    [Test]
    public async Task GetBusinessViewInfoAsync_V4108A_ReturnsV4108AViewInfo()
    {
        // Arrange
        await EnsureConnectedAsync();
        
        // Act
        var view = await _client.GetBusinessViewInfoAsync("V4108A");
        
        // Assert
        await Assert.That(view.ViewName).IsEqualTo("V4108A");
        await Assert.That(view.Description).IsEqualTo("Lot Master");
        await Assert.That(view.SystemCode).IsEqualTo("40");
        await Assert.That(view.Columns.Count).IsEqualTo(61);
        await Assert.That(view.Tables.First().TableName).IsEqualTo("F4108");
        await Assert.That(view.Joins.Count).IsEqualTo(0);
    }

    #endregion
    
    #region GetTableIndexesAsync() Tests

    [Test]
    public async Task GetTableIndexesAsync_F4801_ReturnsAllIndexes()
    {
        
        // Arrange
        await EnsureConnectedAsync();
        
        // Act
        var table = "F4801";
        var indexes = await _client.GetTableIndexesAsync(table);
        
        // Assert
        await Assert.That(indexes.Count).IsGreaterThan(0);
    }
    
    
    #endregion
}
