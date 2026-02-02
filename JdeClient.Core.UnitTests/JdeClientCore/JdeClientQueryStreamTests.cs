using System.Linq;
using JdeClient.Core.Exceptions;
using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using NSubstitute;

namespace JdeClient.Core.UnitTests.JdeClientCore;

public class JdeClientQueryStreamTests
{
    [Test]
    public async Task QueryTableStream_ReturnsRowsAndColumns()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeTableInfo?>(session);
        TestHelpers.SetupExecuteAsync<bool>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        engine.GetTableInfo("F0101", null, null).Returns(new JdeTableInfo
        {
            TableName = "F0101",
            Columns = new List<JdeColumn>
            {
                new() { Name = "AN8" },
                new() { Name = "ALPH" }
            }
        });

        var rows = new[]
        {
            new Dictionary<string, object> { ["AN8"] = 1, ["ALPH"] = "Alpha" }
        };

        engine.StreamTableRows(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<IReadOnlyList<JdeColumn>>(),
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<JdeSort>>(),
                Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(rows);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var stream = client.QueryTableStream("F0101", 10, CancellationToken.None);
        var resultRows = stream.ToList();

        // Assert
        await Assert.That(stream.TableName).IsEqualTo("F0101");
        await Assert.That(stream.ColumnNames.Count).IsEqualTo(2);
        await Assert.That(stream.MaxRows).IsEqualTo(10);
        await Assert.That(resultRows.Count).IsEqualTo(1);
    }

    [Test]
    public async Task QueryTableStream_TableNotFound_Throws()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeTableInfo?>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        engine.GetTableInfo("F0101", null, null).Returns((JdeTableInfo?)null);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var exception = await Assert.That(() => client.QueryTableStream("F0101", 0))
            .ThrowsExactly<JdeTableException>();

        // Assert
        await Assert.That(exception.TableName).IsEqualTo("F0101");
    }

    [Test]
    public async Task QueryTableStream_StreamError_ThrowsOnEnumeration()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeTableInfo?>(session);
        TestHelpers.SetupExecuteAsync<bool>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        engine.GetTableInfo("F0101", null, null).Returns(new JdeTableInfo
        {
            TableName = "F0101",
            Columns = new List<JdeColumn> { new() { Name = "AN8" } }
        });

        engine.StreamTableRows(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<IReadOnlyList<JdeColumn>>(),
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<JdeSort>>(),
                Arg.Any<int?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => throw new InvalidOperationException("boom"));

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var stream = client.QueryTableStream("F0101", 0);
        var exception = await Assert.That(() => stream.ToList())
            .ThrowsExactly<InvalidOperationException>();

        // Assert
        await Assert.That(exception.Message).IsEqualTo("boom");
    }

    [Test]
    public async Task QueryViewStream_ReturnsRows()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeColumn>>(session);
        TestHelpers.SetupExecuteAsync<bool>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        engine.GetViewColumns("V0101A").Returns(new List<JdeColumn>
        {
            new() { Name = "ALPH" }
        });

        engine.StreamViewRows(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<IReadOnlyList<JdeColumn>>(),
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<JdeSort>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new Dictionary<string, object> { ["ALPH"] = "Alpha" }
            });

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var stream = client.QueryViewStream("V0101A");
        var rows = stream.ToList();

        // Assert
        await Assert.That(stream.ColumnNames.Count).IsEqualTo(1);
        await Assert.That(rows.Count).IsEqualTo(1);
    }

    [Test]
    public async Task QueryViewStream_NoColumns_Throws()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeColumn>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        engine.GetViewColumns("V0101A").Returns(new List<JdeColumn>());

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var exception = await Assert.That(() => client.QueryViewStream("V0101A", filters: null))
            .ThrowsExactly<JdeTableException>();

        // Assert
        await Assert.That(exception.TableName).IsEqualTo("V0101A");
    }
}
