using System.Linq;
using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using NSubstitute;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.JdeClientCore;

public class JdeClientDataSourcesTests
{
    [Test]
    public async Task GetAvailableDataSourcesAsync_MapsRows()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeDataSourceInfo>>(session);
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(1) });

        var tableEngine = Substitute.For<IJdeTableQueryEngine>();
        var tableFactory = Substitute.For<IJdeTableQueryEngineFactory>();
        tableFactory.Create(Arg.Any<JdeClientOptions>()).Returns(tableEngine);

        tableEngine.GetTableInfo("F98611", null, null).Returns(new JdeTableInfo
        {
            TableName = "F98611",
            Columns = new List<JdeColumn> { new() { Name = "DATP" } }
        });

        var rows = new List<Dictionary<string, object>>
        {
            new()
            {
                ["DATP"] = "PathA",
                ["SRVR"] = "ServerA",
                ["DATB"] = "DBA"
            },
            new()
            {
                ["DATASOURCE_NAME"] = "DataSourceB",
                ["DSDBPATH"] = "PathB",
                ["DSDBSRVR"] = "ServerB",
                ["DSDBNAME"] = "DBB"
            }
        };

        tableEngine
            .StreamTableRows(
                "F98611",
                0,
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<IReadOnlyList<JdeColumn>>(),
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<JdeSort>>(),
                Arg.Any<int?>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var dataSource = callInfo.ArgAt<string?>(4);
                return dataSource == "System - 920"
                    ? rows
                    : Enumerable.Empty<Dictionary<string, object>>();
            });

        var resolver = Substitute.For<IDataSourceResolver>();
        resolver.ResolveTableDataSource(Arg.Any<HUSER>(), "F98611").Returns((string?)null);

        var client = new JdeClient(
            session,
            new JdeClientOptions(),
            tableQueryEngineFactory: tableFactory,
            dataSourceResolver: resolver);

        // Act
        var result = await client.GetAvailableDataSourcesAsync("System - 920");

        // Assert
        await Assert.That(result.Count).IsEqualTo(2);
        await Assert.That(result[0].Name).IsEqualTo("DataSourceB");
        await Assert.That(result[1].Name).IsEqualTo("PathA");
        await Assert.That(result[0].DatabasePath).IsEqualTo("PathB");
    }
}
