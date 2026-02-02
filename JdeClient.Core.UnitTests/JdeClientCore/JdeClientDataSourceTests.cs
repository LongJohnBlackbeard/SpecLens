using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using NSubstitute;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.JdeClientCore;

public class JdeClientDataSourceTests
{
    [Test]
    public async Task GetAvailableDataSourcesAsync_UsesFallbackCandidates()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(5) });
        TestHelpers.SetupExecuteAsync<List<JdeDataSourceInfo>>(session);

        var resolver = Substitute.For<IDataSourceResolver>();
        resolver.ResolveTableDataSource(Arg.Any<HUSER>(), "F98611").Returns("PrimaryDS");

        var engine = Substitute.For<IJdeTableQueryEngine>();
        engine.GetTableInfo("F98611", null, null).Returns(new JdeTableInfo
        {
            TableName = "F98611",
            Columns = new List<JdeColumn>
            {
                new() { Name = "DATASOURCE" },
                new() { Name = "DBSERVER" },
                new() { Name = "DATABASE" },
                new() { Name = "DBPATH" }
            }
        });

        engine.StreamTableRows("F98611", 0, Arg.Any<IReadOnlyList<JdeFilter>>(), Arg.Any<IReadOnlyList<JdeColumn>>(), "OverrideDS", null, null, true, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<Dictionary<string, object>>());

        engine.StreamTableRows("F98611", 0, Arg.Any<IReadOnlyList<JdeFilter>>(), Arg.Any<IReadOnlyList<JdeColumn>>(), "PrimaryDS", null, null, true, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["DATASOURCE"] = "PrimaryDS",
                    ["DBSERVER"] = "Server1",
                    ["DATABASE"] = "DB1",
                    ["DBPATH"] = "C:\\JDE\\DB"
                }
            });

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory, dataSourceResolver: resolver);

        // Act
        var result = await client.GetAvailableDataSourcesAsync("OverrideDS");

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Name).IsEqualTo("PrimaryDS");
    }
}
