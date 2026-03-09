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

    [Test]
    public async Task ConvertXmlToReadableEr_EmptyDataStructureXml_UsesFallbackTemplate()
    {
        // Arrange
        var eventXml = "<GBRSPEC szEventSpecKey=\"EV1\" xmlns=\"http://jde\">" +
                       "<GBRCOMMENT><text>// Hello</text></GBRCOMMENT>" +
                       "</GBRSPEC>";

        var engine = new JdeXmlEngine(eventXml, string.Empty);

        // Act
        engine.ConvertXmlToReadableEr();

        // Assert
        await Assert.That(engine.ReadableEventRule.Contains("// Hello", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task ConvertXmlToReadableEr_VariableOnlySpec_ListsVariables()
    {
        // Arrange
        var eventXml = "<GBRSPEC szEventSpecKey=\"EV1\" xmlns=\"http://jde\">" +
                       "<GBRVAR szVariableName=\"frm_mnWONumber_DOCO\">" +
                       "<DSOBJVariable idVariable=\"3\" szDict=\"DOCO\" wStyle=\"8\" dataType=\"MathNumeric\" size=\"8\" />" +
                       "</GBRVAR>" +
                       "<GBRVAR szVariableName=\"frm_cStopFilterLogic_YN\">" +
                       "<DSOBJVariable idVariable=\"28\" szDict=\"YN\" wStyle=\"8\" dataType=\"Char\" size=\"1\" />" +
                       "</GBRVAR>" +
                       "</GBRSPEC>";

        var engine = new JdeXmlEngine(eventXml, string.Empty);

        // Act
        engine.ConvertXmlToReadableEr();

        // Assert
        await Assert.That(engine.ReadableEventRule.Contains("frm_mnWONumber_DOCO [DOCO]", StringComparison.Ordinal)).IsTrue();
        await Assert.That(engine.ReadableEventRule.Contains("frm_cStopFilterLogic_YN [YN]", StringComparison.Ordinal)).IsTrue();
    }
}
