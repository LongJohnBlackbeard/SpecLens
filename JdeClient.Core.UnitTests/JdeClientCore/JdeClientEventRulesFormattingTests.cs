using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using NSubstitute;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.JdeClientCore;

public class JdeClientEventRulesFormattingTests
{
    [Test]
    public async Task GetFormattedEventRulesAsync_NullNode_Throws()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var client = new JdeClient(session, new JdeClientOptions());

        // Act
        var exception = await Assert.That(async () => await client.GetFormattedEventRulesAsync(null!))
            .ThrowsExactly<ArgumentNullException>();

        // Assert
        await Assert.That(exception.ParamName).IsEqualTo("node");
    }

    [Test]
    public async Task GetFormattedEventRulesAsync_MissingSpecKey_ReturnsMessage()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var client = new JdeClient(session, new JdeClientOptions());
        var node = new JdeEventRulesNode
        {
            Name = "B1234",
            EventSpecKey = null
        };

        // Act
        var result = await client.GetFormattedEventRulesAsync(node);

        // Assert
        await Assert.That(result.StatusMessage).IsEqualTo("No event rules for the selected item.");
    }

    [Test]
    public async Task GetFormattedEventRulesAsync_MissingTemplate_ReturnsMessage()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var client = new JdeClient(session, new JdeClientOptions());
        var node = new JdeEventRulesNode
        {
            Name = string.Empty,
            EventSpecKey = "EV1"
        };

        // Act
        var result = await client.GetFormattedEventRulesAsync(node);

        // Assert
        await Assert.That(result.StatusMessage).IsEqualTo("No data structure found for the selected item.");
    }

    [Test]
    public async Task GetFormattedEventRulesAsync_EmptySpecKey_ReturnsMessage()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var client = new JdeClient(session, new JdeClientOptions());

        // Act
        var result = await client.GetFormattedEventRulesAsync(string.Empty, "D0001");

        // Assert
        await Assert.That(result.StatusMessage).IsEqualTo("No event rules found.");
    }

    [Test]
    public async Task GetFormattedEventRulesAsync_EventXmlMissing_ReturnsMessage()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeEventRulesXmlDocument>>(session);
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeSpecXmlDocument>>(session);

        var eventEngine = Substitute.For<IEventRulesQueryEngine>();
        var eventFactory = Substitute.For<IEventRulesQueryEngineFactory>();
        eventFactory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(eventEngine);

        eventEngine.GetEventRulesXmlDocuments("EV1").Returns(new List<JdeEventRulesXmlDocument>());
        eventEngine.GetDataStructureXmlDocuments("D0001").Returns(new List<JdeSpecXmlDocument>
        {
            new() { SpecKey = "D0001", Xml = "<root szTmplName=\"D0001\" xmlns=\"http://jde\"><Template /></root>", RecordCount = 1 }
        });

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: eventFactory);

        // Act
        var result = await client.GetFormattedEventRulesAsync("EV1", "D0001");

        // Assert
        await Assert.That(result.StatusMessage).IsEqualTo("No event rules found.");
    }

    [Test]
    public async Task GetFormattedEventRulesAsync_DataStructureMissing_ReturnsMessage()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeEventRulesXmlDocument>>(session);
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeSpecXmlDocument>>(session);

        var eventEngine = Substitute.For<IEventRulesQueryEngine>();
        var eventFactory = Substitute.For<IEventRulesQueryEngineFactory>();
        eventFactory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(eventEngine);

        eventEngine.GetEventRulesXmlDocuments("EV1").Returns(new List<JdeEventRulesXmlDocument>
        {
            new() { EventSpecKey = "EV1", Xml = "<GBREvent szEventSpecKey=\"EV1\" xmlns=\"http://jde\"><GBRCOMMENT>Hi</GBRCOMMENT></GBREvent>", RecordCount = 1 }
        });

        eventEngine.GetDataStructureXmlDocuments("D0001").Returns(new List<JdeSpecXmlDocument>());

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: eventFactory);

        // Act
        var result = await client.GetFormattedEventRulesAsync("EV1", "D0001");

        // Assert
        await Assert.That(result.StatusMessage).IsEqualTo("No data structure XML available for the selected item.");
    }

    [Test]
    public async Task GetFormattedEventRulesAsync_NoFormattedLines_ReturnsMessage()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeEventRulesXmlDocument>>(session);
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeSpecXmlDocument>>(session);

        var eventEngine = Substitute.For<IEventRulesQueryEngine>();
        var eventFactory = Substitute.For<IEventRulesQueryEngineFactory>();
        eventFactory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(eventEngine);

        var eventXml = "<GBREvent szEventSpecKey=\"EV1\" xmlns=\"http://jde\">" +
                       "<GBRVAR szVariableName=\"evt_var\">" +
                       "<DSOBJVariable idVariable=\"1\" szDict=\"LOTN\" wStyle=\"32\" dataType=\"String\" size=\"30\" />" +
                       "</GBRVAR>" +
                       "</GBREvent>";

        var dataXml = "<root szTmplName=\"D0001\" xmlns=\"http://jde\"><Template /></root>";

        eventEngine.GetEventRulesXmlDocuments("EV1").Returns(new List<JdeEventRulesXmlDocument>
        {
            new() { EventSpecKey = "EV1", Xml = eventXml, RecordCount = 1 }
        });

        eventEngine.GetDataStructureXmlDocuments("D0001").Returns(new List<JdeSpecXmlDocument>
        {
            new() { SpecKey = "D0001", Xml = dataXml, RecordCount = 1 }
        });

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: eventFactory);

        // Act
        var result = await client.GetFormattedEventRulesAsync("EV1", "D0001");

        // Assert
        await Assert.That(result.StatusMessage).IsEqualTo("No formatted event rules available.");
    }

    [Test]
    public async Task GetFormattedEventRulesAsync_FormatsIndentedOutput()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeEventRulesXmlDocument>>(session);
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeSpecXmlDocument>>(session);

        var eventEngine = Substitute.For<IEventRulesQueryEngine>();
        var eventFactory = Substitute.For<IEventRulesQueryEngineFactory>();
        eventFactory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(eventEngine);

        var eventXml = "<GBREvent szEventSpecKey=\"EV1\" xmlns=\"http://jde\">" +
                       "<GBRVAR szVariableName=\"evt_var\">" +
                       "<DSOBJVariable idVariable=\"1\" szDict=\"LOTN\" wStyle=\"32\" dataType=\"String\" size=\"30\" />" +
                       "</GBRVAR>" +
                       "<GBRCOMMENT>// Test comment</GBRCOMMENT>" +
                       "<GBRCRIT type=\"If\" lpszCritDesc=\"If Field1 is equal to 10\">" +
                       "<CRE_HEADER>" +
                       "<CRE_NODE eCompType=\"EQUAL\">" +
                       "<zSubject><DSOBJMember idItem=\"1\" szTmplName=\"D0001\" /></zSubject>" +
                       "<zPredicate><DSOBJLiteral><LiteralNumeric>10</LiteralNumeric></DSOBJLiteral></zPredicate>" +
                       "</CRE_NODE>" +
                       "</CRE_HEADER>" +
                       "</GBRCRIT>" +
                       "<GBRASSIGN textString=\"foo=bar\">" +
                       "<ObjTo><DSOBJMember idItem=\"1\" szTmplName=\"D0001\" /></ObjTo>" +
                       "<ObjFrom><DSOBJLiteral><LiteralString>bar</LiteralString></DSOBJLiteral></ObjFrom>" +
                       "</GBRASSIGN>" +
                       "<GBREndIf />" +
                       "</GBREvent>";

        var dataXml = "<root szTmplName=\"D0001\" xmlns=\"http://jde\">" +
                      "<Template>" +
                      "<Item ItemID=\"1\" DisplaySequence=\"1\" CopyWord=\"IN\" DDAlias=\"AL1\" FieldName=\"Field1\" />" +
                      "</Template>" +
                      "</root>";

        eventEngine.GetEventRulesXmlDocuments("EV1").Returns(new List<JdeEventRulesXmlDocument>
        {
            new() { EventSpecKey = "EV1", Xml = eventXml, RecordCount = 1 }
        });

        eventEngine.GetDataStructureXmlDocuments("D0001").Returns(new List<JdeSpecXmlDocument>
        {
            new() { SpecKey = "D0001", Xml = dataXml, RecordCount = 1 }
        });

        var client = new JdeClient(session, new JdeClientOptions(), eventRulesQueryEngineFactory: eventFactory);

        // Act
        var result = await client.GetFormattedEventRulesAsync("EV1", "D0001");

        // Assert
        await Assert.That(result.StatusMessage).IsEqualTo("Event rules loaded.");
        await Assert.That(result.Text.Contains("|   foo [AL1] = bar", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Text.Contains("End If", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task ResolveTemplateName_DataStructureName_Wins()
    {
        var node = new JdeEventRulesNode
        {
            Name = "B0001",
            DataStructureName = "D0001"
        };

        await Assert.That(JdeClient.ResolveTemplateName(node)).IsEqualTo("D0001");
    }

    [Test]
    public async Task ResolveTemplateName_EmptyName_ReturnsEmpty()
    {
        var node = new JdeEventRulesNode
        {
            Name = string.Empty,
            DataStructureName = null
        };

        await Assert.That(JdeClient.ResolveTemplateName(node)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ResolveTemplateName_BPrefixedName_ConvertsToD()
    {
        var node = new JdeEventRulesNode
        {
            Name = "B1234",
            DataStructureName = null
        };

        await Assert.That(JdeClient.ResolveTemplateName(node)).IsEqualTo("D1234");
    }

    [Test]
    public async Task ResolveTemplateName_OtherName_ReturnsSame()
    {
        var node = new JdeEventRulesNode
        {
            Name = "D5678",
            DataStructureName = null
        };

        await Assert.That(JdeClient.ResolveTemplateName(node)).IsEqualTo("D5678");
    }

    [Test]
    public async Task ApplyIndentGuides_EmptyInput_ReturnsEmpty()
    {
        await Assert.That(JdeClient.ApplyIndentGuides(string.Empty)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ApplyIndentGuides_NoTabs_Unchanged()
    {
        var input = $"Line1{Environment.NewLine}Line2";

        await Assert.That(JdeClient.ApplyIndentGuides(input)).IsEqualTo(input);
    }

    [Test]
    public async Task ApplyIndentGuides_ReplacesTabs()
    {
        var input = $"\tLine1{Environment.NewLine}\t\tLine2";
        var expected = $"|   Line1{Environment.NewLine}|   |   Line2";

        await Assert.That(JdeClient.ApplyIndentGuides(input)).IsEqualTo(expected);
    }
}
