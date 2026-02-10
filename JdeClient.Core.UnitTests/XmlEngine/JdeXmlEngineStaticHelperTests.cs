using System.Xml.Linq;
using JdeClient.Core.XmlEngine;

namespace JdeClient.Core.UnitTests.XmlEngine;

public class JdeXmlEngineStaticHelperTests
{
    [Test]
    public async Task FormatFileIoOperation_MapsKnownOperations()
    {
        await Assert.That(JdeXmlEngine.FormatFileIoOperation(null)).IsEqualTo("Operation");
        await Assert.That(JdeXmlEngine.FormatFileIoOperation("  FETCH_SINGLE ")).IsEqualTo("FetchSingle");
        await Assert.That(JdeXmlEngine.FormatFileIoOperation("FETCH_NEXT")).IsEqualTo("FetchNext");
        await Assert.That(JdeXmlEngine.FormatFileIoOperation("SELECT")).IsEqualTo("Select");
        await Assert.That(JdeXmlEngine.FormatFileIoOperation("DELETE")).IsEqualTo("Delete");
        await Assert.That(JdeXmlEngine.FormatFileIoOperation("UPDATE")).IsEqualTo("Update");
        await Assert.That(JdeXmlEngine.FormatFileIoOperation("INSERT")).IsEqualTo("Insert");
        await Assert.That(JdeXmlEngine.FormatFileIoOperation("CUSTOM_OP")).IsEqualTo("CUSTOMOP");
    }

    [Test]
    public async Task FormatFileIoParamLine_RespectsDirection()
    {
        await Assert.That(JdeXmlEngine.FormatFileIoParamLine("OUT", "A", "B")).IsEqualTo("A <- B");
        await Assert.That(JdeXmlEngine.FormatFileIoParamLine("IN", "A", "B")).IsEqualTo("A -> B");
        await Assert.That(JdeXmlEngine.FormatFileIoParamLine(null, "A", "B")).IsEqualTo("A = B");
    }

    [Test]
    public async Task FormatBusinessFunctionParamLine_RespectsDirection()
    {
        await Assert.That(JdeXmlEngine.FormatBusinessFunctionParamLine("OUT", "A", "B")).IsEqualTo("A <- B");
        await Assert.That(JdeXmlEngine.FormatBusinessFunctionParamLine("INOUT", "A", "B")).IsEqualTo("A <-> B");
        await Assert.That(JdeXmlEngine.FormatBusinessFunctionParamLine("IN", "A", "B")).IsEqualTo("A -> B");
    }

    [Test]
    public async Task FormatLiteralValue_UsesLiteralChildIfPresent()
    {
        var literalString = XElement.Parse("<DSOBJLiteral><LiteralString>Value</LiteralString></DSOBJLiteral>");
        var literalNumeric = XElement.Parse("<DSOBJLiteral><LiteralNumeric>42</LiteralNumeric></DSOBJLiteral>");

        await Assert.That(JdeXmlEngine.FormatLiteralValue(literalString)).IsEqualTo("\"Value\"");
        await Assert.That(JdeXmlEngine.FormatLiteralValue(literalNumeric)).IsEqualTo("42");
    }

    [Test]
    public async Task FormatLiteralValue_NoChild_ReturnsTrimmed()
    {
        var literal = XElement.Parse("<DSOBJLiteral>  raw  </DSOBJLiteral>");

        await Assert.That(JdeXmlEngine.FormatLiteralValue(literal)).IsEqualTo("raw");
    }

    [Test]
    public async Task PrefixQualifier_EmptyQualifier_ReturnsValue()
    {
        await Assert.That(JdeXmlEngine.PrefixQualifier(string.Empty, "Value")).IsEqualTo("Value");
        await Assert.That(JdeXmlEngine.PrefixQualifier("BF", "Value")).IsEqualTo("BF Value");
    }

    [Test]
    public async Task SplitQualifier_ParsesKnownTokens()
    {
        await Assert.That(JdeXmlEngine.SplitQualifier(string.Empty).Qualifier).IsNull();
        await Assert.That(JdeXmlEngine.SplitQualifier(string.Empty).Remainder).IsEqualTo(string.Empty);

        var single = JdeXmlEngine.SplitQualifier("Value");
        await Assert.That(single.Qualifier).IsNull();
        await Assert.That(single.Remainder).IsEqualTo("Value");

        var qualified = JdeXmlEngine.SplitQualifier("BF Test");
        await Assert.That(qualified.Qualifier).IsEqualTo("BF");
        await Assert.That(qualified.Remainder).IsEqualTo("Test");

        var unknown = JdeXmlEngine.SplitQualifier("XX Value");
        await Assert.That(unknown.Qualifier).IsNull();
        await Assert.That(unknown.Remainder).IsEqualTo("XX Value");
    }

    [Test]
    public async Task NormalizeXmlPayload_StripsNullsAndPrefixes()
    {
        var xml = "\0\uFEFF junk <root><child /></root>";
        var result = JdeXmlEngine.NormalizeXmlPayload(xml);

        await Assert.That(result).IsEqualTo("<root><child /></root>");
    }

    [Test]
    public async Task NormalizeXmlPayload_NoLeadingJunk_ReturnsOriginal()
    {
        var xml = "<root />";

        await Assert.That(JdeXmlEngine.NormalizeXmlPayload(xml)).IsEqualTo(xml);
    }

    [Test]
    public async Task IndentLine_AddsTabs()
    {
        await Assert.That(JdeXmlEngine.IndentLine("Line", 0)).IsEqualTo("Line");
        await Assert.That(JdeXmlEngine.IndentLine("Line", 2)).IsEqualTo("\t\tLine");
    }
}
