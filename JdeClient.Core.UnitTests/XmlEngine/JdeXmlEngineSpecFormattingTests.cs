using System;
using System.Collections.Generic;
using System.Linq;
using JdeClient.Core;
using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using JdeClient.Core.UnitTests.JdeClientCore;
using NSubstitute;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.XmlEngine;

public class JdeXmlEngineSpecFormattingTests
{
    [Test]
    public async Task GetFormattedEventRulesAsync_FormatsSpecBackedTags()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeEventRulesXmlDocument>>(session);
        TestHelpers.SetupExecuteAsync<IReadOnlyList<JdeSpecXmlDocument>>(session);
        TestHelpers.SetupExecuteAsync<List<JdeIndexInfo>>(session);
        TestHelpers.SetupExecuteAsync<List<JdeDataDictionaryTitle>>(session);
        TestHelpers.SetupExecuteAsync<List<JdeObjectInfo>>(session);
        session.UserHandle.Returns(new HUSER { Handle = new IntPtr(99) });

        var eventEngine = Substitute.For<IEventRulesQueryEngine>();
        var eventFactory = Substitute.For<IEventRulesQueryEngineFactory>();
        eventFactory.Create(Arg.Any<HUSER>(), Arg.Any<JdeClientOptions>()).Returns(eventEngine);

        var tableEngine = Substitute.For<IJdeTableQueryEngine>();
        var tableFactory = Substitute.For<IJdeTableQueryEngineFactory>();
        tableFactory.Create(Arg.Any<JdeClientOptions>()).Returns(tableEngine);

        var queryEngine = Substitute.For<IF9860QueryEngine>();
        session.QueryEngine.Returns(queryEngine);

        queryEngine.QueryObjects(JdeObjectType.BusinessFunction, "B0001", null, 1)
            .Returns(new List<JdeObjectInfo> { new() { ObjectName = "B0001" } });

        tableEngine.GetTableIndexes("F0101").Returns(new List<JdeIndexInfo>
        {
            new() { Id = 1, Name = "IDX1", KeyColumns = new List<string> { "ABCD" } }
        });

        tableEngine.GetDataDictionaryTitles(Arg.Any<IEnumerable<string>>(), null)
            .Returns(new List<JdeDataDictionaryTitle>
            {
                new() { DataItem = "ABCD", Title1 = "Col A" },
                new() { DataItem = "EFGH", Title1 = "Col B" },
                new() { DataItem = "IJKL", Title1 = "Col C" }
            });

        var eventXml = "<GBREvent szEventSpecKey=\"EV1\" xmlns=\"http://jde\">" +
                       "<GBRVAR szVariableName=\"evt_var\">" +
                       "<DSOBJVariable idVariable=\"1\" szDict=\"LOTN\" wStyle=\"32\" dataType=\"String\" size=\"30\" />" +
                       "</GBRVAR>" +
                       "<GBRBF szFuncName=\"MyFunc\" szTmplName=\"D0001\">" +
                       "<ERPARAM wCopyWord=\"OUT\" idItem=\"1\">" +
                       "<DSOBJMember idItem=\"1\" szTmplName=\"D0001\" />" +
                       "</ERPARAM>" +
                       "<ERPARAM wCopyWord=\"INOUT\" idItem=\"2\">" +
                       "<DSOBJVariable idVariable=\"1\" />" +
                       "</ERPARAM>" +
                       "<ERPARAM wCopyWord=\"IN\" idItem=\"3\">" +
                       "<DSOBJLiteral><LiteralString>VALUE</LiteralString></DSOBJLiteral>" +
                       "</ERPARAM>" +
                       "</GBRBF>" +
                       "<GBRFileIOOp operation=\"FETCH_SINGLE\" indexId=\"1\">" +
                       "<DSOBJFileIO Name=\"F0101\" />" +
                       "<GBRParam>" +
                       "<DSItem copyWord=\"IN\" dataItem=\"ABCD\">" +
                       "<Dbref szDict=\"ABCD\" />" +
                       "<DsObjFrom><DSOBJSystemVariable idVariable=\"SV1\" /></DsObjFrom>" +
                       "<DsObjTo><DSOBJMember><Dbref szDict=\"ABCD\" /></DSOBJMember></DsObjTo>" +
                       "</DSItem>" +
                       "</GBRParam>" +
                       "<GBRParam>" +
                       "<DSItem copyWord=\"OUT\" dataItem=\"EFGH\">" +
                       "<Dbref szDict=\"EFGH\" />" +
                       "<DsObjFrom><DSOBJConstant idConstant=\"C1\" /></DsObjFrom>" +
                       "<DsObjTo><DSOBJMember><Dbref szDict=\"EFGH\" /></DSOBJMember></DsObjTo>" +
                       "</DSItem>" +
                       "</GBRParam>" +
                       "<GBRParam>" +
                       "<DSItem dataItem=\"IJKL\">" +
                       "<Dbref szDict=\"IJKL\" />" +
                       "<DsObjFrom><DSOBJLiteral><LiteralNumeric>10</LiteralNumeric></DSOBJLiteral></DsObjFrom>" +
                       "<DsObjTo><DSOBJMember><Dbref szDict=\"IJKL\" /></DSOBJMember></DsObjTo>" +
                       "</DSItem>" +
                       "</GBRParam>" +
                       "</GBRFileIOOp>" +
                       "</GBREvent>";

        var dataXml = "<root szTmplName=\"D0001\" xmlns=\"http://jde\">" +
                      "<Template>" +
                      "<Item ItemID=\"1\" DisplaySequence=\"1\" CopyWord=\"IN\" DDAlias=\"AL1\" FieldName=\"Field1\" />" +
                      "<Item ItemID=\"2\" DisplaySequence=\"2\" CopyWord=\"OUT\" DDAlias=\"AL2\" FieldName=\"Field2\" />" +
                      "<Item ItemID=\"3\" DisplaySequence=\"3\" CopyWord=\"INOUT\" DDAlias=\"AL3\" FieldName=\"Field3\" />" +
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

        var client = new JdeClient(
            session,
            new JdeClientOptions(),
            tableQueryEngineFactory: tableFactory,
            eventRulesQueryEngineFactory: eventFactory);

        // Act
        var result = await client.GetFormattedEventRulesAsync("EV1", "D0001");

        // Assert
        await Assert.That(result.StatusMessage).IsEqualTo("Event rules loaded.");
        await Assert.That(result.Text.Contains("MyFunc(B0001.MyFunc)", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Text.Contains("F0101.FetchSingle", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Text.Contains("Index 1", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Text.Contains("|   BF Field1 [AL1] <- Field1 [AL1]", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Text.Contains("|   VA evt_var [LOTN] <-> Field2 [AL2]", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Text.Contains("|   \"VALUE\" -> Field3 [AL3]", StringComparison.Ordinal)).IsTrue();
        var lines = result.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        await Assert.That(lines.Any(line =>
                line.Contains("|   SV ", StringComparison.Ordinal) &&
                line.Contains("->", StringComparison.Ordinal) &&
                line.Contains("[ABCD]", StringComparison.Ordinal)))
            .IsTrue();
        await Assert.That(result.Text.Contains("|   CO EFGH <- Col B [EFGH]", StringComparison.Ordinal)).IsTrue();
        await Assert.That(result.Text.Contains("|   10 = Col C [IJKL]", StringComparison.Ordinal)).IsTrue();
    }
}
