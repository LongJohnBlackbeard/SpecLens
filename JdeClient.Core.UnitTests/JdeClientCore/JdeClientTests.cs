using JdeClient.Core.Exceptions;
using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using NSubstitute;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.JdeClientCore;

public class JdeClientTests
{
    [Test]
    public async Task JdeClient_NullOptions_CreatesJdeClientInstance()
    {
        // Arrange
        var client = new JdeClient();
        
        // Act

        // Assert
        await Assert.That(client is not null).IsTrue();
    }

    [Test]
    public async Task JdeClient_DefaultCtor_StartsDisconnected()
    {
        // Arrange
        var client = new JdeClient();

        // Act

        // Assert
        await Assert.That(client.IsConnected).IsEqualTo(false);
    }

    [Test]
    public async Task JdeClient_ExplicitOptions_StartsDisconnected()
    {
        // Arrange
        var options = new JdeClientOptions
        {
            EnableDebug = false,
            EnableSpecDebug = false,
            EnableQueryDebug = false
        };
        var client = new JdeClient(options);

        // Act

        // Assert
        await Assert.That(client.IsConnected).IsEqualTo(false);
    }

    [Test]
    public async Task JdeClient_Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var client = new JdeClient();

        // Act
        client.Dispose();
        client.Dispose();

        // Assert
        await Assert.That(client.IsConnected).IsEqualTo(false);
    }

    [Test]
    public async Task JdeClient_ConnectAsync_UpdatesConnectionState()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var connected = false;
        session.IsConnected.Returns(_ => connected);
        session.ConnectAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                connected = true;
                return Task.CompletedTask;
            });

        var client = new JdeClient(session, new JdeClientOptions());

        // Act
        await client.ConnectAsync();

        // Assert
        await Assert.That(client.IsConnected).IsTrue();
    }

    [Test]
    public async Task JdeClient_DisconnectAsync_UpdatesConnectionState()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var connected = true;
        session.IsConnected.Returns(_ => connected);
        session.DisconnectAsync()
            .Returns(_ =>
            {
                connected = false;
                return Task.CompletedTask;
            });
        var client = new JdeClient(session, new JdeClientOptions());

        // Act
        await client.DisconnectAsync();

        // Assert
        await Assert.That(client.IsConnected).IsFalse();
    }

    [Test]
    public async Task JdeClient_GetObjectsAsync_UsesQueryEngineParameters()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var engine = Substitute.For<IF9860QueryEngine>();
        session.QueryEngine.Returns(engine);
        session.ExecuteAsync(Arg.Any<Func<List<JdeObjectInfo>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var action = callInfo.Arg<Func<List<JdeObjectInfo>>>();
                try
                {
                    return Task.FromResult(action());
                }
                catch (Exception ex)
                {
                    return Task.FromException<List<JdeObjectInfo>>(ex);
                }
            });

        var expected = new List<JdeObjectInfo>
        {
            new() { ObjectName = "F0101", ObjectType = "TBLE" }
        };

        engine.QueryObjects(JdeObjectType.Table, "F01*", "Address*", 5).Returns(expected);

        var client = new JdeClient(session, new JdeClientOptions());

        // Act
        var result = await client.GetObjectsAsync(JdeObjectType.Table, "F01*", "Address*", 5);

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].ObjectName).IsEqualTo("F0101");
    }

    [Test]
    public async Task JdeClient_GetObjectsAsync_QueryEngineThrows_WrapsInJdeApiException()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var engine = Substitute.For<IF9860QueryEngine>();
        session.QueryEngine.Returns(engine);
        session.ExecuteAsync(Arg.Any<Func<List<JdeObjectInfo>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var action = callInfo.Arg<Func<List<JdeObjectInfo>>>();
                try
                {
                    return Task.FromResult(action());
                }
                catch (Exception ex)
                {
                    return Task.FromException<List<JdeObjectInfo>>(ex);
                }
            });

        engine.QueryObjects(Arg.Any<JdeObjectType?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<int>())
            .Returns(_ => throw new InvalidOperationException("boom"));

        var client = new JdeClient(session, new JdeClientOptions());

        // Act
        var exception = await Assert.That(async () => await client.GetObjectsAsync())
            .ThrowsExactly<JdeApiException>();

        // Assert
        await Assert.That(exception.ApiFunction).IsEqualTo("GetObjectsAsync");
        await Assert.That(exception.InnerException is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task JdeClient_GetEventRulesTreeAsync_NullObject_ThrowsArgumentNullException()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var client = new JdeClient(session, new JdeClientOptions());

        // Act
        var exception = await Assert.That(async () => await client.GetEventRulesTreeAsync(null!))
            .ThrowsExactly<ArgumentNullException>();

        // Assert
        await Assert.That(exception.ParamName).IsEqualTo("jdeObject");
    }

    [Test]
    public async Task JdeClient_GetTableInfoAsync_UsesFactoryAndEnrichesFromObjectInfo()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var f9860Engine = Substitute.For<IF9860QueryEngine>();
        session.QueryEngine.Returns(f9860Engine);
        session.ExecuteAsync(Arg.Any<Func<JdeTableInfo?>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var action = callInfo.Arg<Func<JdeTableInfo?>>();
                try
                {
                    return Task.FromResult(action());
                }
                catch (Exception ex)
                {
                    return Task.FromException<JdeTableInfo?>(ex);
                }
            });

        var tableEngine = Substitute.For<IJdeTableQueryEngine>();
        var tableFactory = Substitute.For<IJdeTableQueryEngineFactory>();
        tableFactory.Create(Arg.Any<JdeClientOptions>()).Returns(tableEngine);

        tableEngine.GetTableInfo("F0101", null, null).Returns(new JdeTableInfo
        {
            TableName = "F0101",
            Columns = new List<JdeColumn>()
        });

        f9860Engine.GetObjectByName("F0101", JdeObjectType.Table).Returns(new JdeObjectInfo
        {
            ObjectName = "F0101",
            ObjectType = "TBLE",
            Description = "Address Book Master",
            SystemCode = "01",
            ProductCode = "AB"
        });

        var client = new JdeClient(session, new JdeClientOptions(), tableFactory);

        // Act
        var result = await client.GetTableInfoAsync("F0101");

        // Assert
        await Assert.That(result is not null).IsTrue();
        await Assert.That(result!.Description).IsEqualTo("Address Book Master");
        await Assert.That(result.SystemCode).IsEqualTo("01");
        await Assert.That(result.ProductCode).IsEqualTo("AB");
    }

    [Test]
    public async Task JdeClient_GetDefaultTableDataSourceAsync_UsesResolver()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(123) });
        session.ExecuteAsync(Arg.Any<Func<string?>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var action = callInfo.Arg<Func<string?>>();
                try
                {
                    return Task.FromResult(action());
                }
                catch (Exception ex)
                {
                    return Task.FromException<string?>(ex);
                }
            });

        var resolver = Substitute.For<IDataSourceResolver>();
        resolver.ResolveTableDataSource(Arg.Any<HUSER>(), "F0101").Returns("System - 920");

        var client = new JdeClient(session, new JdeClientOptions(), dataSourceResolver: resolver);

        // Act
        var result = await client.GetDefaultTableDataSourceAsync("F0101");

        // Assert
        await Assert.That(result).IsEqualTo("System - 920");
    }

    [Test]
    public async Task JdeClient_GetEventRulesTreeAsync_BusinessFunction_UsesFactory()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(456) });
        session.ExecuteAsync(Arg.Any<Func<JdeEventRulesNode>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var action = callInfo.Arg<Func<JdeEventRulesNode>>();
                try
                {
                    return Task.FromResult(action());
                }
                catch (Exception ex)
                {
                    return Task.FromException<JdeEventRulesNode>(ex);
                }
            });

        var engine = Substitute.For<IEventRulesQueryEngine>();
        var factory = Substitute.For<IEventRulesQueryEngineFactory>();
        factory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(engine);

        engine.GetBusinessFunctionTree("B1234").Returns(new JdeEventRulesNode
        {
            Id = "ENGINE_B1234",
            Name = "ENGINE_B1234",
            NodeType = JdeEventRulesNodeType.Object
        });

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: factory);

        var obj = new JdeObjectInfo
        {
            ObjectName = "B1234",
            ObjectType = "BSFN"
        };

        // Act
        var result = await client.GetEventRulesTreeAsync(obj);

        // Assert
        await Assert.That(result.Id).IsEqualTo("ENGINE_B1234");
    }
}
