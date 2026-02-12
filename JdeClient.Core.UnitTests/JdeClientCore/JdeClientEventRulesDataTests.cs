using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using NSubstitute;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.JdeClientCore;

public class JdeClientEventRulesDataTests
{
    [Test]
    public async Task EventRulesDataMethods_DelegateToEngine()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeEventRuleLine>>(session);
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeEventRulesDecodeDiagnostics>>(session);
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeEventRulesXmlDocument>>(session);
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeSpecXmlDocument>>(session);
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeBusinessFunctionCodeDocument>>(session);
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(1) });

        var engine = Substitute.For<IEventRulesQueryEngine>();
        var factory = Substitute.For<IEventRulesQueryEngineFactory>();
        factory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(engine);

        engine.GetEventRulesLines("EV1").Returns(new List<JdeEventRuleLine>
        {
            new() { Sequence = 1, Text = "Line1" }
        });

        engine.GetEventRulesDecodeDiagnostics("EV1").Returns(new List<JdeEventRulesDecodeDiagnostics>
        {
            new() { Sequence = 1, BlobSize = 10 }
        });

        engine.GetEventRulesXmlDocuments("EV1").Returns(new List<JdeEventRulesXmlDocument>
        {
            new() { EventSpecKey = "EV1", Xml = "<GBREvent szEventSpecKey=\"EV1\" xmlns=\"http://jde\" />", RecordCount = 1 }
        });

        engine.GetDataStructureXmlDocuments("D0001").Returns(new List<JdeSpecXmlDocument>
        {
            new() { SpecKey = "D0001", Xml = "<root szTmplName=\"D0001\" xmlns=\"http://jde\" />", RecordCount = 1 }
        });

        var busFuncDocuments = new List<JdeBusinessFunctionCodeDocument>
        {
            new()
            {
                ObjectName = "B5500725",
                FunctionName = "GetVersion",
                SourceFileName = "B5500725.c",
                SourceCode = "int GetVersion(void) { return 1; }",
                SourceLooksLikeCode = true
            }
        };
        engine.GetBusinessFunctionCodeDocuments("B5500725", "GetVersion", JdeBusinessFunctionCodeLocation.Auto, null)
            .Returns(busFuncDocuments);
        engine.GetBusinessFunctionCodeDocuments("B5500725", "GetVersion", JdeBusinessFunctionCodeLocation.Central, "Central Objects - PY920")
            .Returns(busFuncDocuments);

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: factory);

        // Act
        var lines = await client.GetEventRulesLinesAsync("EV1");
        var diagnostics = await client.GetEventRulesDecodeDiagnosticsAsync("EV1");
        var xmlDocs = await client.GetEventRulesXmlAsync("EV1");
        var dsDocs = await client.GetDataStructureXmlAsync("D0001");
        var busFuncDocs = await client.GetBusinessFunctionCodeAsync("B5500725", "GetVersion");
        var busFuncCentralDocs = await client.GetBusinessFunctionCodeAsync(
            "B5500725",
            "GetVersion",
            JdeBusinessFunctionCodeLocation.Central,
            "Central Objects - PY920");

        // Assert
        await Assert.That(lines.Count).IsEqualTo(1);
        await Assert.That(diagnostics.Count).IsEqualTo(1);
        await Assert.That(xmlDocs.Count).IsEqualTo(1);
        await Assert.That(dsDocs.Count).IsEqualTo(1);
        await Assert.That(busFuncDocs.Count).IsEqualTo(1);
        await Assert.That(busFuncCentralDocs.Count).IsEqualTo(1);
    }
}
