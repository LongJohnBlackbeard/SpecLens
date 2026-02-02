using System;
using JdeClient.Core.Exceptions;
using JdeClient.Core.Models;

namespace JdeClient.Core.UnitTests.Models;

public class JdeModelAndExceptionTests
{
    [Test]
    public async Task JdeExceptions_Constructors_PopulateProperties()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var baseWithCode = new JdeException("base", 7, inner);
        var conn = new JdeConnectionException("conn");
        var api = new JdeApiException("Query", "failed", 5);
        var table = new JdeTableException("F0101", "oops", 9);

        // Assert
        await Assert.That(baseWithCode.ResultCode).IsEqualTo(7);
        await Assert.That(baseWithCode.InnerException).IsEqualTo(inner);
        await Assert.That(conn.Message.Contains("conn", StringComparison.Ordinal)).IsTrue();
        await Assert.That(api.ApiFunction).IsEqualTo("Query");
        await Assert.That(api.ResultCode).IsEqualTo(5);
        await Assert.That(table.TableName).IsEqualTo("F0101");
        await Assert.That(table.ResultCode).IsEqualTo(9);
    }

    [Test]
    public async Task JdeDataDictionaryDetails_InitializesTexts()
    {
        // Arrange
        var details = new JdeDataDictionaryDetails
        {
            DataItem = "AN8"
        };

        // Act
        details.Texts.Add(new JdeDataDictionaryText
        {
            DataItem = "AN8",
            Text = "Address Number"
        });

        // Assert
        await Assert.That(details.Texts.Count).IsEqualTo(1);
        await Assert.That(details.Texts[0].Text).IsEqualTo("Address Number");
    }

    [Test]
    public async Task JdeEventRulesDecodeDiagnostics_EmptyDefaults()
    {
        // Arrange
        var diagnostics = new JdeEventRulesDecodeDiagnostics();

        // Act
        var raw = diagnostics.RawLittleEndian;
        var b733 = diagnostics.RawB733LittleEndian;

        // Assert
        await Assert.That(ReferenceEquals(raw, JdeEventRulesDecodeDiagnostics.UnpackAttempt.Empty)).IsTrue();
        await Assert.That(ReferenceEquals(b733, JdeEventRulesDecodeDiagnostics.B733UnpackAttempt.Empty)).IsTrue();
    }

    [Test]
    public async Task JdeFilterAndSort_Defaults()
    {
        var filter = new JdeFilter();
        var sort = new JdeSort();

        await Assert.That(filter.ColumnName).IsEqualTo(string.Empty);
        await Assert.That(filter.Value).IsEqualTo(string.Empty);
        await Assert.That(filter.Operator).IsEqualTo(JdeFilterOperator.Equals);

        await Assert.That(sort.ColumnName).IsEqualTo(string.Empty);
        await Assert.That(sort.Direction).IsEqualTo(JdeSortDirection.Ascending);
    }

    [Test]
    public async Task JdeBusinessViewModels_Defaults()
    {
        var info = new JdeBusinessViewInfo();
        var table = new JdeBusinessViewTable();
        var join = new JdeBusinessViewJoin();
        var column = new JdeBusinessViewColumn();

        await Assert.That(info.Tables.Count).IsEqualTo(0);
        await Assert.That(info.Columns.Count).IsEqualTo(0);
        await Assert.That(info.Joins.Count).IsEqualTo(0);

        await Assert.That(table.TableName).IsEqualTo(string.Empty);
        await Assert.That(table.InstanceCount).IsEqualTo(0);
        await Assert.That(table.PrimaryIndexId).IsEqualTo(0);

        await Assert.That(join.ForeignTable).IsEqualTo(string.Empty);
        await Assert.That(join.ForeignColumn).IsEqualTo(string.Empty);
        await Assert.That(join.PrimaryTable).IsEqualTo(string.Empty);
        await Assert.That(join.PrimaryColumn).IsEqualTo(string.Empty);
        await Assert.That(join.JoinOperator).IsEqualTo(string.Empty);
        await Assert.That(join.JoinType).IsEqualTo(string.Empty);

        await Assert.That(column.DataItem).IsEqualTo(string.Empty);
        await Assert.That(column.TableName).IsEqualTo(string.Empty);
    }
}
