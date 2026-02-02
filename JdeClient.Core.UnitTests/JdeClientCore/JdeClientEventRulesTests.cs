using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using NSubstitute;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.JdeClientCore;

public class JdeClientEventRulesTests
{
    [Test]
    public async Task GetEventRulesLinesAsync_DelegatesToFactory()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(1) });
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeEventRuleLine>>(session);

        var engine = Substitute.For<IEventRulesQueryEngine>();
        var expected = new List<JdeEventRuleLine> { new() { Sequence = 1, Text = "TEST" } };
        engine.GetEventRulesLines("EVSK-1").Returns(expected);

        var factory = Substitute.For<IEventRulesQueryEngineFactory>();
        factory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: factory);

        // Act
        var result = await client.GetEventRulesLinesAsync("EVSK-1");

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetEventRulesDecodeDiagnosticsAsync_DelegatesToFactory()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(2) });
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeEventRulesDecodeDiagnostics>>(session);

        var engine = Substitute.For<IEventRulesQueryEngine>();
        var expected = new List<JdeEventRulesDecodeDiagnostics> { new() { Sequence = 1 } };
        engine.GetEventRulesDecodeDiagnostics("EVSK-2").Returns(expected);

        var factory = Substitute.For<IEventRulesQueryEngineFactory>();
        factory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: factory);

        // Act
        var result = await client.GetEventRulesDecodeDiagnosticsAsync("EVSK-2");

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetEventRulesTreeAsync_Application_UsesFactory()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(3) });
        TestHelpers.SetupExecuteAsync<JdeEventRulesNode>(session);

        var engine = Substitute.For<IEventRulesQueryEngine>();
        engine.GetApplicationEventRulesTree("P01012").Returns(new JdeEventRulesNode
        {
            Id = "P01012",
            Name = "P01012",
            NodeType = JdeEventRulesNodeType.Object
        });

        var factory = Substitute.For<IEventRulesQueryEngineFactory>();
        factory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: factory);

        var obj = new JdeObjectInfo
        {
            ObjectName = "P01012",
            ObjectType = "APPL"
        };

        // Act
        var result = await client.GetEventRulesTreeAsync(obj);

        // Assert
        await Assert.That(result.Id).IsEqualTo("P01012");
    }

    [Test]
    public async Task GetEventRulesTreeAsync_UnknownType_ReturnsDefaultNode()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(4) });
        TestHelpers.SetupExecuteAsync<JdeEventRulesNode>(session);

        var engine = Substitute.For<IEventRulesQueryEngine>();
        var factory = Substitute.For<IEventRulesQueryEngineFactory>();
        factory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: factory);

        var obj = new JdeObjectInfo
        {
            ObjectName = "X0001",
            ObjectType = "XYZ"
        };

        // Act
        var result = await client.GetEventRulesTreeAsync(obj);

        // Assert
        await Assert.That(result.Id).IsEqualTo("X0001");
        await Assert.That(result.NodeType).IsEqualTo(JdeEventRulesNodeType.Object);
    }

    [Test]
    public async Task GetEventRulesXmlAsync_DelegatesToFactory()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(6) });
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeEventRulesXmlDocument>>(session);

        var engine = Substitute.For<IEventRulesQueryEngine>();
        var expected = new List<JdeEventRulesXmlDocument>
        {
            new() { EventSpecKey = "EVSK-3", Xml = "<xml/>" }
        };
        engine.GetEventRulesXmlDocuments("EVSK-3").Returns(expected);

        var factory = Substitute.For<IEventRulesQueryEngineFactory>();
        factory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: factory);

        // Act
        var result = await client.GetEventRulesXmlAsync("EVSK-3");

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetDataStructureXmlAsync_DelegatesToFactory()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(7) });
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeSpecXmlDocument>>(session);

        var engine = Substitute.For<IEventRulesQueryEngine>();
        var expected = new List<JdeSpecXmlDocument>
        {
            new() { SpecKey = "D0101", Xml = "<xml/>" }
        };
        engine.GetDataStructureXmlDocuments("D0101").Returns(expected);

        var factory = Substitute.For<IEventRulesQueryEngineFactory>();
        factory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: factory);

        // Act
        var result = await client.GetDataStructureXmlAsync("D0101");

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
    }
}
