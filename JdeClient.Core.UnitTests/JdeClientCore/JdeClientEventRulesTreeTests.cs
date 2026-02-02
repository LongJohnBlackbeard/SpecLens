using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using NSubstitute;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.JdeClientCore;

public class JdeClientEventRulesTreeTests
{
    [Test]
    public async Task GetEventRulesTreeAsync_UsesCorrectBranch()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeEventRulesNode>(session);
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(1) });

        var engine = Substitute.For<IEventRulesQueryEngine>();
        var factory = Substitute.For<IEventRulesQueryEngineFactory>();
        factory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(engine);

        engine.GetNamedEventRuleTree("NER1").Returns(new JdeEventRulesNode { Id = "NER1_ENGINE", Name = "NER1_ENGINE" });
        engine.GetApplicationEventRulesTree("APP1").Returns(new JdeEventRulesNode { Id = "APP1_ENGINE", Name = "APP1_ENGINE" });
        engine.GetReportEventRulesTree("UBE1").Returns(new JdeEventRulesNode { Id = "UBE1_ENGINE", Name = "UBE1_ENGINE" });
        engine.GetTableEventRulesTree("TBL1").Returns(new JdeEventRulesNode { Id = "TBL1_ENGINE", Name = "TBL1_ENGINE" });

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: factory);

        // Act
        var ner = await client.GetEventRulesTreeAsync(new JdeObjectInfo { ObjectName = "NER1", ObjectType = "NER" });
        var appl = await client.GetEventRulesTreeAsync(new JdeObjectInfo { ObjectName = "APP1", ObjectType = "APPL" });
        var ube = await client.GetEventRulesTreeAsync(new JdeObjectInfo { ObjectName = "UBE1", ObjectType = "UBE" });
        var tble = await client.GetEventRulesTreeAsync(new JdeObjectInfo { ObjectName = "TBL1", ObjectType = "TBLE" });
        var unknown = await client.GetEventRulesTreeAsync(new JdeObjectInfo { ObjectName = "UNK1", ObjectType = "???" });

        // Assert
        await Assert.That(ner.Id).IsEqualTo("NER1_ENGINE");
        await Assert.That(appl.Id).IsEqualTo("APP1_ENGINE");
        await Assert.That(ube.Id).IsEqualTo("UBE1_ENGINE");
        await Assert.That(tble.Id).IsEqualTo("TBL1_ENGINE");
        await Assert.That(unknown.NodeType).IsEqualTo(JdeEventRulesNodeType.Object);
        await Assert.That(unknown.Children.Count).IsEqualTo(0);
    }
}
