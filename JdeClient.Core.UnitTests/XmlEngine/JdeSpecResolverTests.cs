using JdeClient.Core;
using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using JdeClient.Core.UnitTests.JdeClientCore;
using JdeClient.Core.XmlEngine;
using NSubstitute;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.XmlEngine;

public class JdeSpecResolverTests
{
    [Test]
    public async Task GetDataStructureTemplate_CachesResults()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeSpecXmlDocument>>(session);
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(1) });

        var eventEngine = Substitute.For<IEventRulesQueryEngine>();
        var eventFactory = Substitute.For<IEventRulesQueryEngineFactory>();
        eventFactory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(eventEngine);

        var xml = "<root szDescription=\"Template Desc\" xmlns=\"http://jde\">" +
                  "<Template>" +
                  "<Item ItemID=\"1\" DisplaySequence=\"1\" CopyWord=\"IN\" DDAlias=\"AL1\" FieldName=\"Field1\" />" +
                  "</Template>" +
                  "</root>";

        eventEngine.GetDataStructureXmlDocuments("D0001").Returns(new List<JdeSpecXmlDocument>
        {
            new() { SpecKey = "D0001", Xml = xml, RecordCount = 1 }
        });

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: eventFactory);
        var resolver = new JdeSpecResolver(client);

        // Act
        var first = resolver.GetDataStructureTemplate("D0001");
        var second = resolver.GetDataStructureTemplate("D0001");

        // Assert
        await Assert.That(first is not null).IsTrue();
        await Assert.That(second is not null).IsTrue();
        await Assert.That(ReferenceEquals(first, second)).IsTrue();
    }

    [Test]
    public async Task ResolveBusinessFunctionName_UsesTemplatePrefix()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeObjectInfo>>(session);
        var queryEngine = Substitute.For<IF9860QueryEngine>();
        session.QueryEngine.Returns(queryEngine);

        queryEngine.QueryObjects(JdeObjectType.BusinessFunction, "B1234", null, 1)
            .Returns(new List<JdeObjectInfo> { new() { ObjectName = "B1234_ENGINE" } });

        var client = new JdeClient(session, new JdeClientOptions());
        var resolver = new JdeSpecResolver(client);

        // Act
        var resolved = resolver.ResolveBusinessFunctionName("D1234");

        // Assert
        await Assert.That(resolved).IsEqualTo("B1234_ENGINE");
    }
}
