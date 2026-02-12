using System;
using System.Collections.Generic;
using System.IO;
using JdeClient.Core.Exceptions;
using JdeClient.Core.Models;

namespace JdeClient.Core.UnitTests.JdeClientCore;

public class JdeClientStaticHelpersTests
{
    [Test]
    public async Task NormalizeUser_NullOrWhitespace_ReturnsNull()
    {
        await Assert.That(JdeClient.NormalizeUser(null)).IsNull();
        await Assert.That(JdeClient.NormalizeUser("   ")).IsNull();
    }

    [Test]
    public async Task NormalizeUser_TrimsValue()
    {
        await Assert.That(JdeClient.NormalizeUser("  user  ")).IsEqualTo("user");
    }

    [Test]
    public async Task BuildProjectStatusFilters_EmptyStatus_ReturnsEmpty()
    {
        await Assert.That(JdeClient.BuildProjectStatusFilters(null).Count).IsEqualTo(0);
        await Assert.That(JdeClient.BuildProjectStatusFilters(" ").Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuildProjectStatusFilters_ValidStatus_AddsFilter()
    {
        var filters = JdeClient.BuildProjectStatusFilters(" 28 ");

        await Assert.That(filters.Count).IsEqualTo(1);
        await Assert.That(filters[0].ColumnName).IsEqualTo("OMWPS");
        await Assert.That(filters[0].Value).IsEqualTo("28");
        await Assert.That(filters[0].Operator).IsEqualTo(JdeFilterOperator.Equals);
    }

    [Test]
    public async Task AddWildcardFilter_IgnoresNullOrWhitespace()
    {
        var filters = new List<JdeFilter>();

        JdeClient.AddWildcardFilter(filters, "OMWUSER", null);
        JdeClient.AddWildcardFilter(filters, "OMWUSER", "  ");

        await Assert.That(filters.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AddWildcardFilter_ExactValue_UsesEquals()
    {
        var filters = new List<JdeFilter>();

        JdeClient.AddWildcardFilter(filters, "OMWUSER", "User1");

        await Assert.That(filters.Count).IsEqualTo(1);
        await Assert.That(filters[0].ColumnName).IsEqualTo("OMWUSER");
        await Assert.That(filters[0].Value).IsEqualTo("User1");
        await Assert.That(filters[0].Operator).IsEqualTo(JdeFilterOperator.Equals);
    }

    [Test]
    public async Task AddWildcardFilter_WildcardValue_UsesLike()
    {
        var filters = new List<JdeFilter>();

        JdeClient.AddWildcardFilter(filters, "OMWUSER", "USR*");

        await Assert.That(filters.Count).IsEqualTo(1);
        await Assert.That(filters[0].Value).IsEqualTo("USR%");
        await Assert.That(filters[0].Operator).IsEqualTo(JdeFilterOperator.Like);
    }

    [Test]
    public async Task AddLikeFilter_IgnoresNullOrWhitespace()
    {
        var filters = new List<JdeFilter>();

        JdeClient.AddLikeFilter(filters, "DL01", null);
        JdeClient.AddLikeFilter(filters, "DL01", "   ");

        await Assert.That(filters.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AddLikeFilter_ExactValue_UsesLike()
    {
        var filters = new List<JdeFilter>();

        JdeClient.AddLikeFilter(filters, "DL01", "Test");

        await Assert.That(filters.Count).IsEqualTo(1);
        await Assert.That(filters[0].Value).IsEqualTo("Test");
        await Assert.That(filters[0].Operator).IsEqualTo(JdeFilterOperator.Like);
    }

    [Test]
    public async Task AddLikeFilter_WildcardValue_ReplacesAsterisk()
    {
        var filters = new List<JdeFilter>();

        JdeClient.AddLikeFilter(filters, "DL01", "A*B");

        await Assert.That(filters.Count).IsEqualTo(1);
        await Assert.That(filters[0].Value).IsEqualTo("A%B");
        await Assert.That(filters[0].Operator).IsEqualTo(JdeFilterOperator.Like);
    }

    [Test]
    public async Task SplitObjectId_NoMarker_ReturnsObjectName()
    {
        JdeClient.SplitObjectId("  OBJ  ", out var objectName, out var versionName);

        await Assert.That(objectName).IsEqualTo("OBJ");
        await Assert.That(versionName).IsNull();
    }

    [Test]
    public async Task SplitObjectId_WithMarker_ReturnsVersion()
    {
        JdeClient.SplitObjectId("OBJ!VER1", out var objectName, out var versionName);

        await Assert.That(objectName).IsEqualTo("OBJ");
        await Assert.That(versionName).IsEqualTo("VER1");
    }

    [Test]
    public async Task SplitObjectId_MarkerAtEnd_ReturnsNoVersion()
    {
        JdeClient.SplitObjectId("OBJ!", out var objectName, out var versionName);

        await Assert.That(objectName).IsEqualTo("OBJ");
        await Assert.That(versionName).IsNull();
    }

    [Test]
    public async Task SplitObjectId_LongVersion_Truncates()
    {
        JdeClient.SplitObjectId("OBJ!VER123456789", out var objectName, out var versionName);

        await Assert.That(objectName).IsEqualTo("OBJ");
        await Assert.That(versionName).IsEqualTo("VER1234567");
    }

    [Test]
    public async Task FindFirstValue_ExactMatch_ReturnsTrimmed()
    {
        var row = new Dictionary<string, object>
        {
            ["OMWPRJID"] = "  PRJ1  "
        };

        var value = JdeClient.FindFirstValue(row, "OMWPRJID", "PROJECT");

        await Assert.That(value).IsEqualTo("PRJ1");
    }

    [Test]
    public async Task FindFirstValue_NoMatch_ReturnsNull()
    {
        var row = new Dictionary<string, object>
        {
            ["OTHER"] = "VAL"
        };

        var value = JdeClient.FindFirstValue(row, "OMWPRJID", "PROJECT");

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task FindFirstValue_SubstringFallback_ReturnsValue()
    {
        var row = new Dictionary<string, object>
        {
            ["PROJECT_NAME"] = "PRJ2"
        };

        var value = JdeClient.FindFirstValue(row, "PROJECT");

        await Assert.That(value).IsEqualTo("PRJ2");
    }

    [Test]
    public async Task FindFirstValue_WhitespaceValue_SkipsToNextCandidate()
    {
        var row = new Dictionary<string, object>
        {
            ["OMWPRJID"] = "   ",
            ["PROJECT_NAME"] = "PRJ3"
        };

        var value = JdeClient.FindFirstValue(row, "OMWPRJID", "PROJECT");

        await Assert.That(value).IsEqualTo("PRJ3");
    }

    [Test]
    public async Task ResolveOmwExportPath_NullOrWhitespace_ReturnsNull()
    {
        await Assert.That(JdeClient.ResolveOmwExportPath(null, "PRJ1")).IsNull();
        await Assert.That(JdeClient.ResolveOmwExportPath("   ", "PRJ1")).IsNull();
    }

    [Test]
    public async Task ResolveOmwExportPath_DirectoryOnly_AppendsFileName()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SpecLens_{Guid.NewGuid():N}");
        var expected = Path.GetFullPath(Path.Combine(directory, "PRJ_PRJ1_60_99.par"));

        var result = JdeClient.ResolveOmwExportPath(directory, "PRJ1");

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task ResolveOmwExportPath_ParFile_ReturnsFullPath()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"SpecLens_{Guid.NewGuid():N}.par");
        var expected = Path.GetFullPath(filePath);

        var result = JdeClient.ResolveOmwExportPath(filePath, "PRJ1");

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task ResolveOmwExportPath_NonParExtension_Throws()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"SpecLens_{Guid.NewGuid():N}.txt");

        var exception = await Assert.That(() => JdeClient.ResolveOmwExportPath(filePath, "PRJ1"))
            .ThrowsExactly<ArgumentException>();

        await Assert.That(exception.ParamName).IsEqualTo("outputPath");
    }

    [Test]
    public async Task EnsureOmwExportFile_MissingFile_Throws()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"SpecLens_{Guid.NewGuid():N}.par");

        var exception = await Assert.That(() => JdeClient.EnsureOmwExportFile(filePath))
            .ThrowsExactly<JdeApiException>();

        await Assert.That(exception.ApiFunction).IsEqualTo("ExportProjectToParAsync");
    }

    [Test]
    public async Task MapProjects_SkipsMissingProjectName_AndSorts()
    {
        var result = new JdeQueryResult
        {
            Rows = new List<Dictionary<string, object>>
            {
                new() { ["OMWPRJID"] = "BETA", ["OMWDESC"] = "Second" },
                new() { ["OMWPRJID"] = " ALPHA ", ["OMWPS"] = "28" },
                new() { ["OMWPRJID"] = "   " }
            }
        };

        var projects = JdeClient.MapProjects(result);

        await Assert.That(projects.Count).IsEqualTo(2);
        await Assert.That(projects[0].ProjectName).IsEqualTo("ALPHA");
        await Assert.That(projects[1].ProjectName).IsEqualTo("BETA");
    }

    [Test]
    public async Task MapProjectObjects_MapsAndSkipsInvalidRows()
    {
        var result = new JdeQueryResult
        {
            Rows = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["OMWPRJID"] = "PRJ1",
                    ["OMWOBJID"] = "OBJ!VER123456789",
                    ["OMWOT"] = "TBLE",
                    ["PATHCD"] = "DV920"
                },
                new()
                {
                    ["OMWPRJID"] = "PRJ2",
                    ["OMWOBJID"] = "OBJ2"
                }
            }
        };

        var objects = JdeClient.MapProjectObjects(result);

        await Assert.That(objects.Count).IsEqualTo(1);
        await Assert.That(objects[0].ObjectName).IsEqualTo("OBJ");
        await Assert.That(objects[0].VersionName).IsEqualTo("VER1234567");
        await Assert.That(objects[0].PathCode).IsEqualTo("DV920");
    }

    [Test]
    public async Task MapDataSources_DedupesAndSorts()
    {
        var result = new JdeQueryResult
        {
            Rows = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["DATP"] = "PathA",
                    ["SRVR"] = "ServerA",
                    ["DATB"] = "DBA"
                },
                new()
                {
                    ["DATASOURCE"] = "DataSourceB",
                    ["DSDBPATH"] = "PathB"
                },
                new()
                {
                    ["DATASOURCE"] = "datasourceb",
                    ["DSDBPATH"] = "PathC"
                }
            }
        };

        var items = JdeClient.MapDataSources(result);

        await Assert.That(items.Count).IsEqualTo(2);
        await Assert.That(items[0].Name).IsEqualTo("DataSourceB");
        await Assert.That(items[1].Name).IsEqualTo("PathA");
    }

    [Test]
    public async Task MapPathCodes_DedupesTrimsAndSorts()
    {
        var result = new JdeQueryResult
        {
            Rows = new List<Dictionary<string, object>>
            {
                new() { ["EMPATHCD"] = " PY920 " },
                new() { ["PATHCD"] = "DV920" },
                new() { ["PATHCODE"] = "py920" },
                new() { ["PATHCD"] = "" }
            }
        };

        var pathCodes = JdeClient.MapPathCodes(result);

        await Assert.That(pathCodes.Count).IsEqualTo(2);
        await Assert.That(pathCodes[0]).IsEqualTo("DV920");
        await Assert.That(pathCodes[1]).IsEqualTo("PY920");
    }

    [Test]
    public async Task FilterProjectsByUser_FiltersToUserSet()
    {
        var projects = new List<JdeProjectInfo>
        {
            new() { ProjectName = "PRJ1" },
            new() { ProjectName = "PRJ2" }
        };
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "prj2" };

        var result = JdeClient.FilterProjectsByUser(projects, allowed);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].ProjectName).IsEqualTo("PRJ2");
    }

    [Test]
    public async Task FilterProjectsByUser_EmptySet_ReturnsEmpty()
    {
        var projects = new List<JdeProjectInfo>
        {
            new() { ProjectName = "PRJ1" }
        };

        var result = JdeClient.FilterProjectsByUser(projects, new HashSet<string>());

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task MapUserDefinedCodeTypes_MapsRows()
    {
        var result = new JdeQueryResult
        {
            Rows = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["SY"] = "00",
                    ["RT"] = "CA",
                    ["DL01"] = "Category",
                    ["CDL"] = "2"
                }
            }
        };

        var codes = JdeClient.MapUserDefinedCodeTypes(result);

        await Assert.That(codes.Count).IsEqualTo(1);
        await Assert.That(codes[0].ProductCode).IsEqualTo("00");
        await Assert.That(codes[0].UserDefinedCodeType).IsEqualTo("CA");
    }

    [Test]
    public async Task MapUserDefinedCodes_MapsRows()
    {
        var result = new JdeQueryResult
        {
            Rows = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["SY"] = "00",
                    ["RT"] = "CA",
                    ["KY"] = "01",
                    ["DL01"] = "Description",
                    ["DL02"] = "Desc2",
                    ["SPHD"] = "S",
                    ["HRDC"] = "Y"
                }
            }
        };

        var codes = JdeClient.MapUserDefinedCodes(result);

        await Assert.That(codes.Count).IsEqualTo(1);
        await Assert.That(codes[0].ProductCode).IsEqualTo("00");
        await Assert.That(codes[0].UserDefinedCodeType).IsEqualTo("CA");
        await Assert.That(codes[0].Code).IsEqualTo("01");
    }
}
