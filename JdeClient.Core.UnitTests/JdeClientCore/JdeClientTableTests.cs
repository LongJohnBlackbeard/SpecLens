using JdeClient.Core.Exceptions;
using JdeClient.Core.Internal;
using JdeClient.Core.Models;
using NSubstitute;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.JdeClientCore;

public class JdeClientTableTests
{
    [Test]
    public async Task QueryTableAsync_NullFilters_PassesEmptyFilters()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeQueryResult>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        engine.QueryTable(
                "F0101",
                1000,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters => filters.Count == 0),
                null,
                null)
            .Returns(new JdeQueryResult { TableName = "F0101" });

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var result = await client.QueryTableAsync("F0101");

        // Assert
        await Assert.That(result.TableName).IsEqualTo("F0101");
    }

    [Test]
    public async Task QueryTableCountAsync_NullFilters_PassesEmptyFilters()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<int>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        engine.CountTable(
                "F0101",
                Arg.Is<IReadOnlyList<JdeFilter>>(filters => filters.Count == 0),
                null)
            .Returns(42);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var result = await client.QueryTableCountAsync("F0101");

        // Assert
        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task GetBusinessViewInfoAsync_DelegatesToTableEngine()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeBusinessViewInfo?>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var expected = new JdeBusinessViewInfo
        {
            ViewName = "V0101A"
        };
        expected.Columns.Add(new JdeBusinessViewColumn());
        engine.GetBusinessViewInfo("V0101A").Returns(expected);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var result = await client.GetBusinessViewInfoAsync("V0101A");

        // Assert
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task GetTableIndexesAsync_DelegatesToTableEngine()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeIndexInfo>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var expected = new List<JdeIndexInfo>
        {
            new() { Name = "IDX1" }
        };
        engine.GetTableIndexes("F4801").Returns(expected);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var result = await client.GetTableIndexesAsync("F4801");

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetDataDictionaryDescriptionsAsync_UsesExpectedTextTypes()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeDataDictionaryTitle>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        engine.GetDataDictionaryTitles(Arg.Any<IEnumerable<string>>(), Arg.Any<IReadOnlyList<int>>())
            .Returns(callInfo =>
            {
                var types = callInfo.ArgAt<IReadOnlyList<int>>(1);
                var matches =
                    types.Count == 4 &&
                    types.Contains(DDT_GLOSSARY) &&
                    types.Contains(DDT_ROW_DESC) &&
                    types.Contains(DDT_ALPHA_DESC) &&
                    types.Contains(DDT_COL_TITLE);

                return matches
                    ? new List<JdeDataDictionaryTitle> { new() { DataItem = "AN8", Title1 = "Title" } }
                    : new List<JdeDataDictionaryTitle>();
            });

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var result = await client.GetDataDictionaryDescriptionsAsync(new[] { "AN8" });

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].DataItem).IsEqualTo("AN8");
    }

    [Test]
    public async Task GetDataDictionaryTitlesAsync_DelegatesToTableEngine()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeDataDictionaryTitle>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var expected = new List<JdeDataDictionaryTitle>
        {
            new() { DataItem = "AN8", Title1 = "Address Number" }
        };
        engine.GetDataDictionaryTitles(Arg.Any<IEnumerable<string>>(), null).Returns(expected);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var result = await client.GetDataDictionaryTitlesAsync(new[] { "AN8" });

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetDataDictionaryItemNamesAsync_DelegatesToTableEngine()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeDataDictionaryItemName>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var expected = new List<JdeDataDictionaryItemName>
        {
            new() { DataItem = "AN8", Name = "Address Number" }
        };
        engine.GetDataDictionaryItemNames(Arg.Any<IEnumerable<string>>()).Returns(expected);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var result = await client.GetDataDictionaryItemNamesAsync(new[] { "AN8" });

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetDataDictionaryDetailsAsync_DelegatesToTableEngine()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeDataDictionaryDetails>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var expected = new List<JdeDataDictionaryDetails>
        {
            new() { DataItem = "AN8" }
        };
        engine.GetDataDictionaryDetails(Arg.Any<IEnumerable<string>>()).Returns(expected);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var result = await client.GetDataDictionaryDetailsAsync(new[] { "AN8" });

        // Assert
        await Assert.That(result.Count).IsEqualTo(1);
    }

    [Test]
    public async Task QueryTableAsync_QueryEngineThrows_WrapsInJdeApiException()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeQueryResult>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        engine.QueryTable(
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<JdeSort>?>())
            .Returns(_ => throw new InvalidOperationException("boom"));

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var exception = await Assert.That(async () => await client.QueryTableAsync("F0101", new List<JdeFilter>()))
            .ThrowsExactly<JdeApiException>();

        // Assert
        await Assert.That(exception.ApiFunction).IsEqualTo("QueryTableAsync");
        await Assert.That(exception.InnerException is InvalidOperationException).IsTrue();
    }

    [Test]
    public async Task GetProjectsAsync_StatusFilter_MapsRows()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeProjectInfo>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var result = new JdeQueryResult { TableName = "F98220" };
        result.Rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["OMWPRJID"] = "PRJ1",
            ["OMWDESC"] = "Test Project",
            ["OMWPS"] = "28",
            ["SRCRLS"] = "PY920"
        });

        engine.QueryTable(
                "F98220",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 1 &&
                    filters[0].ColumnName == "OMWPS" &&
                    filters[0].Value == "28"),
                "JPY920",
                null)
            .Returns(result);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var projects = await client.GetProjectsAsync(status: "28", "JPY920");

        // Assert
        await Assert.That(projects.Count).IsEqualTo(1);
        await Assert.That(projects[0].ProjectName).IsEqualTo("PRJ1");
        await Assert.That(projects[0].Description).IsEqualTo("Test Project");
        await Assert.That(projects[0].Status).IsEqualTo("28");
    }

    [Test]
    public async Task GetProjectsAsync_UserFilter_FiltersProjects()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeProjectInfo>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();

        var userResult = new JdeQueryResult { TableName = "F98221" };
        userResult.Rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["OMWPRJID"] = "PRJ1",
            ["OMWUSER"] = "ALICE"
        });
        userResult.Rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["OMWPRJID"] = "PRJ2",
            ["OMWUSER"] = "ALICE"
        });

        var projectResult = new JdeQueryResult { TableName = "F98220" };
        projectResult.Rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["OMWPRJID"] = "PRJ1",
            ["OMWPS"] = "28"
        });
        projectResult.Rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["OMWPRJID"] = "PRJ3",
            ["OMWPS"] = "28"
        });

        engine.QueryTable(
                "F98221",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 1 &&
                    filters[0].ColumnName == "OMWUSER" &&
                    filters[0].Value == "ALICE"),
                "JPY920",
                null)
            .Returns(userResult);

        engine.QueryTable(
                "F98220",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 1 &&
                    filters[0].ColumnName == "OMWPS" &&
                    filters[0].Value == "28"),
                "JPY920",
                null)
            .Returns(projectResult);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var projects = await client.GetProjectsAsync("28", "JPY920", user: "ALICE");

        // Assert
        await Assert.That(projects.Count).IsEqualTo(1);
        await Assert.That(projects[0].ProjectName).IsEqualTo("PRJ1");
    }

    [Test]
    public async Task GetProjectsAsync_UserFilter_Wildcard_UsesLikeFilter()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeProjectInfo>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();

        var userResult = new JdeQueryResult { TableName = "F98221" };
        userResult.Rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["OMWPRJID"] = "PRJ1",
            ["OMWUSER"] = "ALICE"
        });

        var projectResult = new JdeQueryResult { TableName = "F98220" };
        projectResult.Rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["OMWPRJID"] = "PRJ1",
            ["OMWPS"] = "28"
        });

        engine.QueryTable(
                "F98221",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 1 &&
                    filters[0].ColumnName == "OMWUSER" &&
                    filters[0].Value == "AL%" &&
                    filters[0].Operator == JdeFilterOperator.Like),
                "JPY920",
                null)
            .Returns(userResult);

        engine.QueryTable(
                "F98220",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 1 &&
                    filters[0].ColumnName == "OMWPS" &&
                    filters[0].Value == "28"),
                "JPY920",
                null)
            .Returns(projectResult);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var projects = await client.GetProjectsAsync("28", "JPY920", user: "AL*");

        // Assert
        await Assert.That(projects.Count).IsEqualTo(1);
    }

    [Test]
    public async Task GetProjectsAsync_UserFilter_NoProjects_ReturnsEmpty()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeProjectInfo>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();

        var userResult = new JdeQueryResult { TableName = "F98221" };

        engine.QueryTable(
                "F98221",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 1 &&
                    filters[0].ColumnName == "OMWUSER" &&
                    filters[0].Value == "ALICE"),
                "JPY920",
                null)
            .Returns(userResult);

        engine.QueryTable(
                "F98220",
                Arg.Any<int>(),
                Arg.Any<IReadOnlyList<JdeFilter>>(),
                Arg.Any<string?>(),
                Arg.Any<IReadOnlyList<JdeSort>?>())
            .Returns(_ => throw new InvalidOperationException("F98220 should not be queried when no user projects exist."));

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var projects = await client.GetProjectsAsync("28", "JPY920", user: "ALICE");

        // Assert
        await Assert.That(projects.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetProjectObjectsAsync_PathCodeFilter_ParsesObjectId()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeProjectObjectInfo>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var result = new JdeQueryResult { TableName = "F98222" };
        result.Rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["OMWPRJID"] = "PRJ1",
            ["OMWOBJID"] = "R123456!VER000001",
            ["OMWOT"] = "UBE",
            ["PATHCD"] = "PY920"
        });

        engine.QueryTable(
                "F98222",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 2 &&
                    filters.Any(filter => filter.ColumnName == "OMWPRJID" && filter.Value == "PRJ1") &&
                    filters.Any(filter => filter.ColumnName == "PATHCD" && filter.Value == "PY920")),
                "JPY920",
                null)
            .Returns(result);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var objects = await client.GetProjectObjectsAsync("PRJ1", "PY920", "JPY920");

        // Assert
        await Assert.That(objects.Count).IsEqualTo(1);
        await Assert.That(objects[0].ObjectId).IsEqualTo("R123456!VER000001");
        await Assert.That(objects[0].ObjectName).IsEqualTo("R123456");
        await Assert.That(objects[0].VersionName).IsEqualTo("VER000001");
        await Assert.That(objects[0].ObjectType).IsEqualTo("UBE");
        await Assert.That(objects[0].PathCode).IsEqualTo("PY920");
    }

    [Test]
    public async Task GetProjectObjectsAsync_NoPathCode_DoesNotApplyPathCodeFilter()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<List<JdeProjectObjectInfo>>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var result = new JdeQueryResult { TableName = "F98222" };
        result.Rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["OMWPRJID"] = "PRJ1",
            ["OMWOBJID"] = "F0101",
            ["OMWOT"] = "TBLE"
        });

        engine.QueryTable(
                "F98222",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 1 &&
                    filters[0].ColumnName == "OMWPRJID" &&
                    filters[0].Value == "PRJ1"),
                "JPY920",
                null)
            .Returns(result);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var objects = await client.GetProjectObjectsAsync("PRJ1", dataSourceOverride: "JPY920");

        // Assert
        await Assert.That(objects.Count).IsEqualTo(1);
        await Assert.That(objects[0].ObjectName).IsEqualTo("F0101");
    }

    [Test]
    public async Task GetUserDefinedCodeTypesAsync_FiltersAndMapsRows()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeQueryResult>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var result = new JdeQueryResult { TableName = "F0004" };
        result.Rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["SY"] = "01",
            ["RT"] = "ST",
            ["DL01"] = "Status Codes",
            ["CDL"] = "1"
        });

        engine.QueryTable(
                "F0004",
                50,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 2 &&
                    filters.Any(filter => filter.ColumnName == "SY" && filter.Value == "01") &&
                    filters.Any(filter => filter.ColumnName == "RT" && filter.Value == "ST")),
                "JPY920",
                null)
            .Returns(result);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var codes = await client.GetUserDefinedCodeTypesAsync(
            productCode: "01",
            userDefinedCode: "ST",
            dataSourceOverride: "JPY920",
            maxRows: 50);

        // Assert
        await Assert.That(codes.Count).IsEqualTo(1);
        await Assert.That(codes[0].ProductCode).IsEqualTo("01");
        await Assert.That(codes[0].UserDefinedCodeType).IsEqualTo("ST");
        await Assert.That(codes[0].Description).IsEqualTo("Status Codes");
        await Assert.That(codes[0].CodeLength).IsEqualTo("1");
    }

    [Test]
    public async Task GetUserDefinedCodeTypesAsync_NoFilters_PassesEmptyFilters()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeQueryResult>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var result = new JdeQueryResult { TableName = "F0004" };

        engine.QueryTable(
                "F0004",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters => filters.Count == 0),
                null,
                null)
            .Returns(result);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var codes = await client.GetUserDefinedCodeTypesAsync();

        // Assert
        await Assert.That(codes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetUserDefinedCodeTypesAsync_Wildcard_UsesLikeFilter()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeQueryResult>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var result = new JdeQueryResult { TableName = "F0004" };

        engine.QueryTable(
                "F0004",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 2 &&
                    filters.Any(filter => filter.ColumnName == "SY" && filter.Value == "0%" && filter.Operator == JdeFilterOperator.Like) &&
                    filters.Any(filter => filter.ColumnName == "RT" && filter.Value == "S%" && filter.Operator == JdeFilterOperator.Like)),
                "JPY920",
                null)
            .Returns(result);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var codes = await client.GetUserDefinedCodeTypesAsync(
            productCode: "0*",
            userDefinedCode: "S*",
            dataSourceOverride: "JPY920");

        // Assert
        await Assert.That(codes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetUserDefinedCodesAsync_FiltersAndMapsRows()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeQueryResult>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var result = new JdeQueryResult { TableName = "F0005" };
        result.Rows.Add(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["SY"] = "01",
            ["RT"] = "ST",
            ["KY"] = "A",
            ["DL01"] = "Active",
            ["DL02"] = "Active Long",
            ["SPHD"] = "H",
            ["HRDC"] = "Y"
        });

        engine.QueryTable(
                "F0005",
                25,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 5 &&
                    filters.Any(filter => filter.ColumnName == "SY" && filter.Value == "01") &&
                    filters.Any(filter => filter.ColumnName == "RT" && filter.Value == "ST") &&
                    filters.Any(filter => filter.ColumnName == "KY" && filter.Value == "A") &&
                    filters.Any(filter => filter.ColumnName == "DL01" && filter.Value == "Act%") &&
                    filters.Any(filter => filter.ColumnName == "DL02" && filter.Value == "Long%")),
                "JPY920",
                null)
            .Returns(result);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var codes = await client.GetUserDefinedCodesAsync(
            productCode: "01",
            userDefinedCodeType: "ST",
            userDefinedCode: "A",
            description: "Act*",
            description2: "Long*",
            dataSourceOverride: "JPY920",
            maxRows: 25);

        // Assert
        await Assert.That(codes.Count).IsEqualTo(1);
        await Assert.That(codes[0].ProductCode).IsEqualTo("01");
        await Assert.That(codes[0].UserDefinedCodeType).IsEqualTo("ST");
        await Assert.That(codes[0].Code).IsEqualTo("A");
        await Assert.That(codes[0].Description).IsEqualTo("Active");
        await Assert.That(codes[0].Description2).IsEqualTo("Active Long");
        await Assert.That(codes[0].SpecialHandlingCode).IsEqualTo("H");
        await Assert.That(codes[0].HardCoded).IsEqualTo("Y");
    }

    [Test]
    public async Task GetUserDefinedCodesAsync_Wildcard_UsesLikeFilters()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeQueryResult>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var result = new JdeQueryResult { TableName = "F0005" };

        engine.QueryTable(
                "F0005",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 3 &&
                    filters.Any(filter => filter.ColumnName == "SY" && filter.Value == "0%" && filter.Operator == JdeFilterOperator.Like) &&
                    filters.Any(filter => filter.ColumnName == "RT" && filter.Value == "S%" && filter.Operator == JdeFilterOperator.Like) &&
                    filters.Any(filter => filter.ColumnName == "KY" && filter.Value == "A%" && filter.Operator == JdeFilterOperator.Like)),
                "JPY920",
                null)
            .Returns(result);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var codes = await client.GetUserDefinedCodesAsync(
            productCode: "0*",
            userDefinedCodeType: "S*",
            userDefinedCode: "A*",
            description: null,
            description2: null,
            dataSourceOverride: "JPY920");

        // Assert
        await Assert.That(codes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetUserDefinedCodesAsync_NullProductCode_ThrowsArgumentNullException()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var client = new JdeClient(session, new JdeClientOptions());

        // Act
        var exception = await Assert.That(async () =>
                await client.GetUserDefinedCodesAsync(
                    null!,
                    "ST",
                    userDefinedCode: "A",
                    description: null,
                    description2: null))
            .ThrowsExactly<ArgumentNullException>();

        // Assert
        await Assert.That(exception.ParamName).IsEqualTo("productCode");
    }

    [Test]
    public async Task GetUserDefinedCodesAsync_NullUserDefinedCodeType_ThrowsArgumentNullException()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        var client = new JdeClient(session, new JdeClientOptions());

        // Act
        var exception = await Assert.That(async () =>
                await client.GetUserDefinedCodesAsync(
                    "01",
                    null!,
                    userDefinedCode: "A",
                    description: null,
                    description2: null))
            .ThrowsExactly<ArgumentNullException>();

        // Assert
        await Assert.That(exception.ParamName).IsEqualTo("userDefinedCodeType");
    }

    [Test]
    public async Task GetUserDefinedCodesAsync_NoUserDefinedCode_DoesNotApplyCodeFilter()
    {
        // Arrange
        var session = Substitute.For<IJdeSession>();
        TestHelpers.SetupExecuteAsync<JdeQueryResult>(session);

        var engine = Substitute.For<IJdeTableQueryEngine>();
        var result = new JdeQueryResult { TableName = "F0005" };

        engine.QueryTable(
                "F0005",
                0,
                Arg.Is<IReadOnlyList<JdeFilter>>(filters =>
                    filters.Count == 2 &&
                    filters.Any(filter => filter.ColumnName == "SY" && filter.Value == "01") &&
                    filters.Any(filter => filter.ColumnName == "RT" && filter.Value == "ST")),
                null,
                null)
            .Returns(result);

        var factory = Substitute.For<IJdeTableQueryEngineFactory>();
        factory.Create(Arg.Any<JdeClientOptions>()).Returns(engine);

        var client = new JdeClient(session, new JdeClientOptions(), factory);

        // Act
        var codes = await client.GetUserDefinedCodesAsync(
            productCode: "01",
            userDefinedCodeType: "ST",
            userDefinedCode: null,
            description: null,
            description2: null);

        // Assert
        await Assert.That(codes.Count).IsEqualTo(0);
    }
}
