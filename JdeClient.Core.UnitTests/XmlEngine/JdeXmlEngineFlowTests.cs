using JdeClient.Core.XmlEngine;

namespace JdeClient.Core.UnitTests.XmlEngine;

public class JdeXmlEngineFlowTests
{
    [Test]
    public async Task ConvertXmlToReadableEr_HandlesElseAndEndWhile()
    {
        // Arrange
        var eventXml = "<GBREvent szEventSpecKey=\"EV1\" xmlns=\"http://jde\">" +
                       "<GBRCRIT type=\"If\" lpszCritDesc=\"If X is equal to Y\">" +
                       "<CRE_HEADER>" +
                       "<CRE_NODE eCompType=\"EQUAL\">" +
                       "<zSubject><DSOBJLiteral><LiteralString>X</LiteralString></DSOBJLiteral></zSubject>" +
                       "<zPredicate><DSOBJLiteral><LiteralString>Y</LiteralString></DSOBJLiteral></zPredicate>" +
                       "</CRE_NODE>" +
                       "</CRE_HEADER>" +
                       "</GBRCRIT>" +
                       "<GBRElse />" +
                       "<GBREndIf />" +
                       "<GBREndWhile />" +
                       "</GBREvent>";

        var dataXml = "<root szTmplName=\"D0001\" xmlns=\"http://jde\"><Template /></root>";

        var engine = new JdeXmlEngine(eventXml, dataXml);

        // Act
        engine.ConvertXmlToReadableEr();

        // Assert
        await Assert.That(engine.ReadableEventRule.Contains("Else", StringComparison.Ordinal)).IsTrue();
        await Assert.That(engine.ReadableEventRule.Contains("End While", StringComparison.Ordinal)).IsTrue();
    }
}
