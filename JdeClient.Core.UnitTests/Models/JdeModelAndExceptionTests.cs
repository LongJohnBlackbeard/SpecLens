using System;
using JdeClient.Core.Exceptions;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using static JdeClient.Core.Interop.JdeStructures;

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

    [Test]
    public async Task JdeException_MessageAndInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new JdeException("msg", inner);
        await Assert.That(ex.Message).IsEqualTo("msg");
        await Assert.That(ex.InnerException).IsEqualTo(inner);
        await Assert.That(ex.ResultCode).IsNull();
    }

    [Test]
    public async Task JdeException_MessageAndCode()
    {
        var ex = new JdeException("msg", 42);
        await Assert.That(ex.Message).IsEqualTo("msg");
        await Assert.That(ex.ResultCode).IsEqualTo(42);
    }

    [Test]
    public async Task JdeConnectionException_MessageAndInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new JdeConnectionException("conn fail", inner);
        await Assert.That(ex.Message).IsEqualTo("conn fail");
        await Assert.That(ex.InnerException).IsEqualTo(inner);
    }

    [Test]
    public async Task JdeConnectionException_MessageAndCode()
    {
        var ex = new JdeConnectionException("conn fail", 3);
        await Assert.That(ex.ResultCode).IsEqualTo(3);
    }

    [Test]
    public async Task JdeApiException_FunctionAndMessage()
    {
        var ex = new JdeApiException("JDB_Open", "failed");
        await Assert.That(ex.ApiFunction).IsEqualTo("JDB_Open");
        await Assert.That(ex.Message.Contains("JDB_Open", StringComparison.Ordinal)).IsTrue();
        await Assert.That(ex.Message.Contains("failed", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task JdeApiException_FunctionMessageAndInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new JdeApiException("JDB_Fetch", "error", inner);
        await Assert.That(ex.ApiFunction).IsEqualTo("JDB_Fetch");
        await Assert.That(ex.InnerException).IsEqualTo(inner);
    }

    [Test]
    public async Task JdeTableException_TableAndMessage()
    {
        var ex = new JdeTableException("F0101", "not found");
        await Assert.That(ex.TableName).IsEqualTo("F0101");
        await Assert.That(ex.Message.Contains("F0101", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task JdeTableException_TableMessageAndInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new JdeTableException("F0101", "error", inner);
        await Assert.That(ex.TableName).IsEqualTo("F0101");
        await Assert.That(ex.InnerException).IsEqualTo(inner);
    }

    [Test]
    public async Task JdeQueryResult_Properties()
    {
        var result = new JdeQueryResult
        {
            TableName = "F0101",
            IsTruncated = true,
            MaxRows = 100
        };
        result.Rows.Add(new Dictionary<string, object> { ["AN8"] = 42 });
        result.ColumnNames.Add("AN8");

        await Assert.That(result.RowCount).IsEqualTo(1);
        await Assert.That(result.IsTruncated).IsTrue();
        await Assert.That(result.MaxRows).IsEqualTo(100);
        await Assert.That(result.ToString()).IsEqualTo("F0101: 1 rows");
    }

    [Test]
    public async Task JdeFilter_ParameterizedConstructor()
    {
        var filter = new JdeFilter("AN8", "500", JdeFilterOperator.GreaterThan);
        await Assert.That(filter.ColumnName).IsEqualTo("AN8");
        await Assert.That(filter.Value).IsEqualTo("500");
        await Assert.That(filter.Operator).IsEqualTo(JdeFilterOperator.GreaterThan);
    }

    [Test]
    public async Task JdeColumn_ToStringAndProperties()
    {
        var column = new JdeColumn
        {
            Name = "AN8",
            DataType = 9,
            Length = 10,
            SourceTable = "F0101",
            InstanceId = 1
        };

        await Assert.That(column.ToString()).IsEqualTo("AN8 (9, 10)");
        await Assert.That(column.SourceTable).IsEqualTo("F0101");
        await Assert.That(column.InstanceId).IsEqualTo(1);
    }

    [Test]
    public async Task JdeObjectInfo_ToStringAndProperties()
    {
        var info = new JdeObjectInfo
        {
            ObjectName = "F0101",
            ObjectType = "TBLE",
            ProductCode = "01",
            Status = "Active"
        };

        await Assert.That(info.ToString()).IsEqualTo("F0101 (TBLE)");
        await Assert.That(info.ProductCode).IsEqualTo("01");
        await Assert.That(info.Status).IsEqualTo("Active");
    }

    [Test]
    public async Task JdeIndexInfo_DisplayName()
    {
        var primary = new JdeIndexInfo { Name = "F0101_PK", IsPrimary = true };
        var secondary = new JdeIndexInfo { Name = "F0101_1", IsPrimary = false };

        await Assert.That(primary.DisplayName).IsEqualTo("F0101_PK (Primary)");
        await Assert.That(secondary.DisplayName).IsEqualTo("F0101_1");
    }

    [Test]
    public async Task JdeDataDictionaryTitle_CombinedTitle_BothNull()
    {
        var title = new JdeDataDictionaryTitle();
        await Assert.That(title.CombinedTitle is null).IsTrue();
    }

    [Test]
    public async Task JdeDataDictionaryTitle_CombinedTitle_OnlyTitle1()
    {
        var title = new JdeDataDictionaryTitle { Title1 = "Address" };
        await Assert.That(title.CombinedTitle).IsEqualTo("Address");
    }

    [Test]
    public async Task JdeDataDictionaryTitle_CombinedTitle_OnlyTitle2()
    {
        var title = new JdeDataDictionaryTitle { Title2 = "Number" };
        await Assert.That(title.CombinedTitle).IsEqualTo("Number");
    }

    [Test]
    public async Task JdeDataDictionaryTitle_CombinedTitle_Both()
    {
        var title = new JdeDataDictionaryTitle { Title1 = "Address", Title2 = "Number" };
        await Assert.That(title.CombinedTitle).IsEqualTo("Address Number");
    }

    [Test]
    public async Task JdeDataDictionaryDetails_AllPropertySetters()
    {
        var details = new JdeDataDictionaryDetails
        {
            DataItem = "AN8",
            VarLength = 100,
            FormatNumber = 1,
            DictionaryName = "AddressNumber",
            SystemCode = "01",
            GlossaryGroup = 'A',
            ErrorLevel = '1',
            Alias = "AN8",
            TypeCode = 'C',
            EverestType = 9,
            As400Class = "AN8",
            Length = 8,
            Decimals = 0,
            DisplayDecimals = 0,
            DefaultValue = "0",
            ControlType = 1,
            As400EditRule = "NN",
            As400EditParm1 = "P1",
            As400EditParm2 = "P2",
            As400DispRule = "DR",
            As400DispParm = "DP",
            EditBehavior = 1,
            DisplayBehavior = 2,
            SecurityFlag = 'N',
            NextNumberIndex = 5,
            NextNumberSystem = "01",
            Style = 1,
            Behavior = 2,
            DataSourceTemplateName = "DSTN",
            DisplayRuleBfnName = "DRBF",
            EditRuleBfnName = "ERBF",
            SearchFormName = "SFN"
        };

        await Assert.That(details.DataItem).IsEqualTo("AN8");
        await Assert.That(details.VarLength).IsEqualTo(100u);
        await Assert.That(details.FormatNumber).IsEqualTo(1);
        await Assert.That(details.DictionaryName).IsEqualTo("AddressNumber");
        await Assert.That(details.SystemCode).IsEqualTo("01");
        await Assert.That(details.GlossaryGroup).IsEqualTo('A');
        await Assert.That(details.ErrorLevel).IsEqualTo('1');
        await Assert.That(details.Alias).IsEqualTo("AN8");
        await Assert.That(details.TypeCode).IsEqualTo('C');
        await Assert.That(details.EverestType).IsEqualTo(9);
        await Assert.That(details.As400Class).IsEqualTo("AN8");
        await Assert.That(details.Length).IsEqualTo(8);
        await Assert.That(details.Decimals).IsEqualTo((ushort)0);
        await Assert.That(details.DisplayDecimals).IsEqualTo((ushort)0);
        await Assert.That(details.DefaultValue).IsEqualTo("0");
        await Assert.That(details.ControlType).IsEqualTo((ushort)1);
        await Assert.That(details.As400EditRule).IsEqualTo("NN");
        await Assert.That(details.As400EditParm1).IsEqualTo("P1");
        await Assert.That(details.As400EditParm2).IsEqualTo("P2");
        await Assert.That(details.As400DispRule).IsEqualTo("DR");
        await Assert.That(details.As400DispParm).IsEqualTo("DP");
        await Assert.That(details.EditBehavior).IsEqualTo(1);
        await Assert.That(details.DisplayBehavior).IsEqualTo(2);
        await Assert.That(details.SecurityFlag).IsEqualTo('N');
        await Assert.That(details.NextNumberIndex).IsEqualTo((ushort)5);
        await Assert.That(details.NextNumberSystem).IsEqualTo("01");
        await Assert.That(details.Style).IsEqualTo(1);
        await Assert.That(details.Behavior).IsEqualTo(2);
        await Assert.That(details.DataSourceTemplateName).IsEqualTo("DSTN");
        await Assert.That(details.DisplayRuleBfnName).IsEqualTo("DRBF");
        await Assert.That(details.EditRuleBfnName).IsEqualTo("ERBF");
        await Assert.That(details.SearchFormName).IsEqualTo("SFN");
    }

    [Test]
    public async Task JdeDataDictionaryText_AllPropertySetters()
    {
        var text = new JdeDataDictionaryText
        {
            DataItem = "AN8",
            TextType = 'A',
            Language = "EN",
            SystemCode = "01",
            DictionaryName = "AddressNumber",
            VarLength = 50,
            FormatNumber = 1,
            Text = "Address Number"
        };

        await Assert.That(text.DataItem).IsEqualTo("AN8");
        await Assert.That(text.TextType).IsEqualTo('A');
        await Assert.That(text.Language).IsEqualTo("EN");
        await Assert.That(text.SystemCode).IsEqualTo("01");
        await Assert.That(text.DictionaryName).IsEqualTo("AddressNumber");
        await Assert.That(text.VarLength).IsEqualTo(50u);
        await Assert.That(text.FormatNumber).IsEqualTo(1);
        await Assert.That(text.Text).IsEqualTo("Address Number");
    }

    [Test]
    public async Task JdeBusinessViewColumn_AllProperties()
    {
        var column = new JdeBusinessViewColumn
        {
            Sequence = 1,
            DataItem = "AN8",
            TableName = "F0101",
            InstanceId = 0,
            DataType = 9,
            Length = 8,
            Decimals = 0,
            DisplayDecimals = 0,
            TypeCode = 'C',
            ClassCode = 'A'
        };

        await Assert.That(column.Sequence).IsEqualTo(1);
        await Assert.That(column.DataItem).IsEqualTo("AN8");
        await Assert.That(column.TableName).IsEqualTo("F0101");
        await Assert.That(column.InstanceId).IsEqualTo(0);
        await Assert.That(column.DataType).IsEqualTo(9);
        await Assert.That(column.Length).IsEqualTo(8);
        await Assert.That(column.Decimals).IsEqualTo(0);
        await Assert.That(column.DisplayDecimals).IsEqualTo(0);
        await Assert.That(column.TypeCode).IsEqualTo('C');
        await Assert.That(column.ClassCode).IsEqualTo('A');
    }

    [Test]
    public async Task JdeEventRuleLine_AllProperties()
    {
        var line = new JdeEventRuleLine
        {
            Sequence = 1,
            RecordType = 10,
            Text = "VA evt_an8 = FC GetAddressNumber()",
            IndentLevel = 2
        };

        await Assert.That(line.Sequence).IsEqualTo(1);
        await Assert.That(line.RecordType).IsEqualTo((short)10);
        await Assert.That(line.Text).IsEqualTo("VA evt_an8 = FC GetAddressNumber()");
        await Assert.That(line.IndentLevel).IsEqualTo(2);
    }

    [Test]
    public async Task JdeEventRulesDecodeDiagnostics_AllPropertySetters()
    {
        var diag = new JdeEventRulesDecodeDiagnostics
        {
            Sequence = 5,
            BlobSize = 1024,
            HeadHex = "FF00AA",
            RawLooksLikeGbrSpec = true,
            Uncompressed = true,
            UncompressedSize = 2048,
            UncompressedLooksLikeGbrSpec = true
        };

        await Assert.That(diag.Sequence).IsEqualTo(5);
        await Assert.That(diag.BlobSize).IsEqualTo(1024);
        await Assert.That(diag.HeadHex).IsEqualTo("FF00AA");
        await Assert.That(diag.RawLooksLikeGbrSpec).IsTrue();
        await Assert.That(diag.Uncompressed).IsTrue();
        await Assert.That(diag.UncompressedSize).IsEqualTo(2048);
        await Assert.That(diag.UncompressedLooksLikeGbrSpec).IsTrue();
    }

    [Test]
    public async Task JdeEventRulesDecodeDiagnostics_UnpackAttempt_AllPropertySetters()
    {
        var attempt = new JdeEventRulesDecodeDiagnostics.UnpackAttempt
        {
            Status = JdeStructures.JdeUnpackSpecStatus.Success,
            UnpackedLength = 512,
            LooksLikeGbrSpec = true,
            Error = "test error"
        };

        await Assert.That(attempt.Status).IsEqualTo(JdeStructures.JdeUnpackSpecStatus.Success);
        await Assert.That(attempt.UnpackedLength).IsEqualTo(512);
        await Assert.That(attempt.LooksLikeGbrSpec).IsTrue();
        await Assert.That(attempt.Error).IsEqualTo("test error");
    }

    [Test]
    public async Task JdeEventRulesDecodeDiagnostics_B733UnpackAttempt_AllPropertySetters()
    {
        var attempt = new JdeEventRulesDecodeDiagnostics.B733UnpackAttempt
        {
            Status = JdeStructures.JdeB733UnpackSpecStatus.Success,
            UnpackedLength = 256,
            LooksLikeGbrSpec = true,
            CodePage = 65001,
            OsType = 2,
            Error = "b733 error"
        };

        await Assert.That(attempt.Status).IsEqualTo(JdeStructures.JdeB733UnpackSpecStatus.Success);
        await Assert.That(attempt.UnpackedLength).IsEqualTo(256);
        await Assert.That(attempt.LooksLikeGbrSpec).IsTrue();
        await Assert.That(attempt.CodePage).IsEqualTo(65001u);
        await Assert.That(attempt.OsType).IsEqualTo(2);
        await Assert.That(attempt.Error).IsEqualTo("b733 error");
    }

    [Test]
    public async Task JdeEventRulesDecodeDiagnostics_AllEndianSetters()
    {
        var raw = new JdeEventRulesDecodeDiagnostics.UnpackAttempt { UnpackedLength = 10 };
        var rawBe = new JdeEventRulesDecodeDiagnostics.UnpackAttempt { UnpackedLength = 20 };
        var b733 = new JdeEventRulesDecodeDiagnostics.B733UnpackAttempt { UnpackedLength = 30 };
        var b733Be = new JdeEventRulesDecodeDiagnostics.B733UnpackAttempt { UnpackedLength = 40 };
        var ucRaw = new JdeEventRulesDecodeDiagnostics.UnpackAttempt { UnpackedLength = 50 };
        var ucRawBe = new JdeEventRulesDecodeDiagnostics.UnpackAttempt { UnpackedLength = 60 };
        var ucB733 = new JdeEventRulesDecodeDiagnostics.B733UnpackAttempt { UnpackedLength = 70 };
        var ucB733Be = new JdeEventRulesDecodeDiagnostics.B733UnpackAttempt { UnpackedLength = 80 };

        var diag = new JdeEventRulesDecodeDiagnostics
        {
            RawLittleEndian = raw,
            RawBigEndian = rawBe,
            RawB733LittleEndian = b733,
            RawB733BigEndian = b733Be,
            UncompressedLittleEndian = ucRaw,
            UncompressedBigEndian = ucRawBe,
            UncompressedB733LittleEndian = ucB733,
            UncompressedB733BigEndian = ucB733Be
        };

        await Assert.That(diag.RawLittleEndian.UnpackedLength).IsEqualTo(10);
        await Assert.That(diag.RawBigEndian.UnpackedLength).IsEqualTo(20);
        await Assert.That(diag.RawB733LittleEndian.UnpackedLength).IsEqualTo(30);
        await Assert.That(diag.RawB733BigEndian.UnpackedLength).IsEqualTo(40);
        await Assert.That(diag.UncompressedLittleEndian.UnpackedLength).IsEqualTo(50);
        await Assert.That(diag.UncompressedBigEndian.UnpackedLength).IsEqualTo(60);
        await Assert.That(diag.UncompressedB733LittleEndian.UnpackedLength).IsEqualTo(70);
        await Assert.That(diag.UncompressedB733BigEndian.UnpackedLength).IsEqualTo(80);
    }

    [Test]
    public async Task JdeEventRulesDocument_AllProperties()
    {
        var lines = new[] { new JdeEventRuleLine { Text = "line1" } };
        var doc = new JdeEventRulesDocument
        {
            Title = "Test Document",
            DataStructureName = "D0100041",
            Lines = lines
        };

        await Assert.That(doc.Title).IsEqualTo("Test Document");
        await Assert.That(doc.DataStructureName).IsEqualTo("D0100041");
        await Assert.That(doc.Lines.Count).IsEqualTo(1);
        await Assert.That(doc.Lines[0].Text).IsEqualTo("line1");
    }

    [Test]
    public async Task JdeEventRulesNode_AllProperties()
    {
        var child = new JdeEventRulesNode { Id = "child1", Name = "Child" };
        var node = new JdeEventRulesNode
        {
            Id = "root",
            Name = "Root",
            NodeType = JdeEventRulesNodeType.Function,
            EventSpecKey = "EVSK001",
            DataStructureName = "D0100041",
            Children = new[] { child },
            IsExpanded = true
        };

        await Assert.That(node.Id).IsEqualTo("root");
        await Assert.That(node.Name).IsEqualTo("Root");
        await Assert.That(node.NodeType).IsEqualTo(JdeEventRulesNodeType.Function);
        await Assert.That(node.EventSpecKey).IsEqualTo("EVSK001");
        await Assert.That(node.DataStructureName).IsEqualTo("D0100041");
        await Assert.That(node.Children.Count).IsEqualTo(1);
        await Assert.That(node.IsExpanded).IsTrue();
        await Assert.That(node.HasEventRules).IsTrue();
    }

    [Test]
    public async Task JdeEventRulesNode_HasEventRules_FalseWhenNoKey()
    {
        var node = new JdeEventRulesNode();
        await Assert.That(node.HasEventRules).IsFalse();
    }

    [Test]
    public async Task JdeProjectInfo_AllProperties()
    {
        var info = new JdeProjectInfo
        {
            ProjectName = "PRJ001",
            Description = "Test Project",
            Status = "Active",
            Type = "Standard",
            SourceRelease = "E920",
            TargetRelease = "E930",
            SaveName = "SavePkg"
        };

        await Assert.That(info.ProjectName).IsEqualTo("PRJ001");
        await Assert.That(info.Description).IsEqualTo("Test Project");
        await Assert.That(info.Status).IsEqualTo("Active");
        await Assert.That(info.Type).IsEqualTo("Standard");
        await Assert.That(info.SourceRelease).IsEqualTo("E920");
        await Assert.That(info.TargetRelease).IsEqualTo("E930");
        await Assert.That(info.SaveName).IsEqualTo("SavePkg");
        await Assert.That(info.ToString()).IsEqualTo("PRJ001 (Active)");
    }

    [Test]
    public async Task JdeProjectObjectInfo_AllProperties()
    {
        var info = new JdeProjectObjectInfo
        {
            ProjectName = "PRJ001",
            ObjectId = "F0101|ZJDE0001",
            ObjectName = "F0101",
            VersionName = "ZJDE0001",
            ObjectType = "TBLE",
            PathCode = "DV920",
            SourceRelease = "E920",
            ObjectStatus = "28",
            VersionStatus = "01",
            User = "JDE"
        };

        await Assert.That(info.ProjectName).IsEqualTo("PRJ001");
        await Assert.That(info.ObjectId).IsEqualTo("F0101|ZJDE0001");
        await Assert.That(info.ObjectName).IsEqualTo("F0101");
        await Assert.That(info.VersionName).IsEqualTo("ZJDE0001");
        await Assert.That(info.ObjectType).IsEqualTo("TBLE");
        await Assert.That(info.PathCode).IsEqualTo("DV920");
        await Assert.That(info.SourceRelease).IsEqualTo("E920");
        await Assert.That(info.ObjectStatus).IsEqualTo("28");
        await Assert.That(info.VersionStatus).IsEqualTo("01");
        await Assert.That(info.User).IsEqualTo("JDE");
        await Assert.That(info.ToString()).IsEqualTo("F0101|ZJDE0001 (TBLE)");
    }

    [Test]
    public async Task JdeTableInfo_AllProperties()
    {
        var info = new JdeTableInfo
        {
            TableName = "F0101",
            Description = "Address Book Master",
            SystemCode = "01",
            ProductCode = "AB"
        };
        info.Columns.Add(new JdeColumn { Name = "AN8", Length = 8 });

        await Assert.That(info.TableName).IsEqualTo("F0101");
        await Assert.That(info.Description).IsEqualTo("Address Book Master");
        await Assert.That(info.SystemCode).IsEqualTo("01");
        await Assert.That(info.ProductCode).IsEqualTo("AB");
        await Assert.That(info.ToString()).IsEqualTo("F0101 (1 columns)");
    }

    [Test]
    public async Task JdeUserDefinedCodeTypes_ParameterizedConstructor()
    {
        var types = new JdeUserDefinedCodeTypes("01", "ST", "Status Codes", "3");

        await Assert.That(types.ProductCode).IsEqualTo("01");
        await Assert.That(types.UserDefinedCodeType).IsEqualTo("ST");
        await Assert.That(types.Description).IsEqualTo("Status Codes");
        await Assert.That(types.CodeLength).IsEqualTo("3");
    }

    [Test]
    public async Task JdeColumn_Description_Property()
    {
        var column = new JdeColumn
        {
            Name = "AN8",
            Description = "Address Number",
            Decimals = 2
        };

        await Assert.That(column.Description).IsEqualTo("Address Number");
        await Assert.That(column.Decimals).IsEqualTo(2);
    }

    [Test]
    public async Task NID_ToString_ReturnsValue()
    {
        var nid = new NID("F0101");
        await Assert.That(nid.ToString()).IsEqualTo("F0101");
    }

    [Test]
    public async Task ID_ToString_ReturnsValue()
    {
        var id = new ID(42);
        await Assert.That(id.ToString()).IsEqualTo("42");
    }

    [Test]
    public async Task JdeQueryStream_Properties()
    {
        var rows = new List<Dictionary<string, object>>
        {
            new() { ["AN8"] = 42 }
        };

        var stream = new JdeQueryStream(
            "F0101",
            new[] { "AN8" },
            100,
            () => rows);

        await Assert.That(stream.TableName).IsEqualTo("F0101");
        await Assert.That(stream.ColumnNames.Count).IsEqualTo(1);
        await Assert.That(stream.MaxRows).IsEqualTo(100);

        var enumeratedRows = new List<Dictionary<string, object>>();
        foreach (var row in stream)
        {
            enumeratedRows.Add(row);
        }
        await Assert.That(enumeratedRows.Count).IsEqualTo(1);
    }

    [Test]
    public async Task JdeQueryStream_NonGenericEnumerator()
    {
        var rows = new List<Dictionary<string, object>> { new() { ["AN8"] = 1 } };
        var stream = new JdeQueryStream("F0101", new[] { "AN8" }, null, () => rows);

        // Exercise the non-generic IEnumerable.GetEnumerator() path
        System.Collections.IEnumerable enumerable = stream;
        int count = 0;
        foreach (var _ in enumerable)
        {
            count++;
        }
        await Assert.That(count).IsEqualTo(1);
        await Assert.That(stream.MaxRows is null).IsTrue();
    }

    [Test]
    public async Task JdeUserDefinedCodeTypes_DefaultConstructor()
    {
        var types = new JdeUserDefinedCodeTypes();
        await Assert.That(types.ProductCode is null).IsTrue();
        await Assert.That(types.UserDefinedCodeType is null).IsTrue();
        await Assert.That(types.Description is null).IsTrue();
        await Assert.That(types.CodeLength is null).IsTrue();
    }

    [Test]
    public async Task JdeBusinessViewJoin_ForeignAndPrimaryInstanceIds()
    {
        var join = new JdeBusinessViewJoin
        {
            ForeignInstanceId = 1,
            PrimaryInstanceId = 2
        };
        await Assert.That(join.ForeignInstanceId).IsEqualTo(1);
        await Assert.That(join.PrimaryInstanceId).IsEqualTo(2);
    }
}
