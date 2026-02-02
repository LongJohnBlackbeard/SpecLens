using System.Linq;
using System.Runtime.InteropServices;
using JdeClient.Core.Internal;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.Internal;

public class F9860QueryEngineTests
{
    [Test]
    public async Task NormalizeText_NullOrWhitespace_ReturnsEmpty()
    {
        await Assert.That(F9860QueryEngine.NormalizeText(null)).IsEqualTo(string.Empty);
        await Assert.That(F9860QueryEngine.NormalizeText("   ")).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task NormalizeText_RemovesNullTerminatorsAndTrims()
    {
        var value = F9860QueryEngine.NormalizeText("  ABC\0   ");
        await Assert.That(value).IsEqualTo("ABC");
    }

    [Test]
    public async Task BuildLikePattern_ReplacesAsterisksWithPercent()
    {
        var value = F9860QueryEngine.BuildLikePattern("A*B*C");
        await Assert.That(value).IsEqualTo("A%B%C");
    }

    [Test]
    public async Task ShouldFilterByObjectType_RespectsAllAndUnknown()
    {
        await Assert.That(F9860QueryEngine.ShouldFilterByObjectType(null)).IsFalse();
        await Assert.That(F9860QueryEngine.ShouldFilterByObjectType(JdeObjectType.All)).IsFalse();
        await Assert.That(F9860QueryEngine.ShouldFilterByObjectType(JdeObjectType.Unknown)).IsFalse();
        await Assert.That(F9860QueryEngine.ShouldFilterByObjectType(JdeObjectType.Table)).IsTrue();
    }

    [Test]
    public async Task CreateFilters_BuildsExpectedFilters()
    {
        var filters = F9860QueryEngine.CreateFilters(JdeObjectType.Table, "AB*", "Desc");

        await Assert.That(filters.Count).IsEqualTo(3);

        var nameFilter = filters.Single(filter => filter.Column == F9860Structures.Columns.OBNM);
        await Assert.That(nameFilter.Comparison).IsEqualTo(JDEDB_CMP_LK);
        await Assert.That(nameFilter.Value).IsEqualTo("AB%");

        var descriptionFilter = filters.Single(filter => filter.Column == F9860Structures.Columns.MD);
        await Assert.That(descriptionFilter.Comparison).IsEqualTo(JDEDB_CMP_EQ);
        await Assert.That(descriptionFilter.Value).IsEqualTo("Desc");

        var typeFilter = filters.Single(filter => filter.Column == F9860Structures.Columns.FUNO);
        await Assert.That(typeFilter.Value).IsEqualTo(F9860Structures.ObjectTypes.Table);
    }

    [Test]
    public async Task CreateFilters_EmptyInputs_ReturnsNone()
    {
        var filters = F9860QueryEngine.CreateFilters(null, null, null);
        await Assert.That(filters.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuildColumnLengthMap_AddsNamesAndIsCaseInsensitive()
    {
        var columns = new[]
        {
            new JdeColumn { Name = "AN8", DataDictionaryItem = "DDAN8", SqlName = "SQLAN8", Length = 10 },
            new JdeColumn { Name = "MCU", DataDictionaryItem = string.Empty, SqlName = "SQLMCU", Length = 5 }
        };

        var map = F9860QueryEngine.BuildColumnLengthMap(columns);

        await Assert.That(map["an8"]).IsEqualTo(10);
        await Assert.That(map["DDAN8"]).IsEqualTo(10);
        await Assert.That(map["sqlan8"]).IsEqualTo(10);
        await Assert.That(map["MCU"]).IsEqualTo(5);
        await Assert.That(map["SQLMCU"]).IsEqualTo(5);
    }

    [Test]
    public async Task BuildColumnLengthMap_SkipsEmptyOrZeroLengthColumns()
    {
        var columns = new[]
        {
            new JdeColumn { Name = "AN8", Length = 0 },
            new JdeColumn { Name = string.Empty, DataDictionaryItem = "DD", Length = 5 }
        };

        var map = F9860QueryEngine.BuildColumnLengthMap(columns);

        await Assert.That(map.Count).IsEqualTo(1);
        await Assert.That(map["DD"]).IsEqualTo(5);
    }

    [Test]
    public async Task InitializeEnvironmentCore_UsesGlobalWhenValid()
    {
        var result = F9860QueryEngine.InitializeEnvironmentCore(
            () => (JDEDB_PASSED, new HENV { Handle = new IntPtr(1) }),
            () => new HENV { Handle = IntPtr.Zero },
            () => (JDEDB_PASSED, new HENV { Handle = new IntPtr(2) }),
            null);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Source).IsEqualTo(F9860QueryEngine.EnvironmentSource.Global);
        await Assert.That(result.OwnsEnv).IsFalse();
    }

    [Test]
    public async Task InitializeEnvironmentCore_UsesLocalWhenGlobalInvalid()
    {
        int localCalls = 0;
        var result = F9860QueryEngine.InitializeEnvironmentCore(
            () => (JDEDB_FAILED, new HENV { Handle = IntPtr.Zero }),
            () =>
            {
                localCalls++;
                return new HENV { Handle = new IntPtr(3) };
            },
            () => (JDEDB_PASSED, new HENV { Handle = new IntPtr(4) }),
            null);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Source).IsEqualTo(F9860QueryEngine.EnvironmentSource.Local);
        await Assert.That(result.OwnsEnv).IsFalse();
        await Assert.That(localCalls).IsEqualTo(1);
    }

    [Test]
    public async Task InitializeEnvironmentCore_UsesInitWhenLocalInvalid()
    {
        var result = F9860QueryEngine.InitializeEnvironmentCore(
            () => (JDEDB_FAILED, new HENV { Handle = IntPtr.Zero }),
            () => new HENV { Handle = IntPtr.Zero },
            () => (JDEDB_PASSED, new HENV { Handle = new IntPtr(5) }),
            null);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Source).IsEqualTo(F9860QueryEngine.EnvironmentSource.Initialized);
        await Assert.That(result.OwnsEnv).IsTrue();
    }

    [Test]
    public async Task InitializeEnvironmentCore_ReturnsFailureWhenInitFails()
    {
        var result = F9860QueryEngine.InitializeEnvironmentCore(
            () => (JDEDB_FAILED, new HENV { Handle = IntPtr.Zero }),
            () => new HENV { Handle = IntPtr.Zero },
            () => (JDEDB_FAILED, new HENV { Handle = IntPtr.Zero }),
            null);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Source).IsEqualTo(F9860QueryEngine.EnvironmentSource.Failed);
    }

    [Test]
    public async Task InitializeUserContextCore_ReturnsSuccessWhenValid()
    {
        var env = new HENV { Handle = new IntPtr(7) };
        var result = F9860QueryEngine.InitializeUserContextCore(
            env,
            _ => (JDEDB_PASSED, new HUSER { Handle = new IntPtr(8) }),
            null);

        await Assert.That(result.Status).IsEqualTo(F9860QueryEngine.UserInitStatus.Success);
        await Assert.That(result.OwnsUser).IsTrue();
    }

    [Test]
    public async Task InitializeUserContextCore_ReturnsFailedResult()
    {
        var env = new HENV { Handle = new IntPtr(7) };
        var result = F9860QueryEngine.InitializeUserContextCore(
            env,
            _ => (JDEDB_FAILED, new HUSER { Handle = IntPtr.Zero }),
            null);

        await Assert.That(result.Status).IsEqualTo(F9860QueryEngine.UserInitStatus.FailedResult);
    }

    [Test]
    public async Task InitializeUserContextCore_ReturnsInvalidHandle()
    {
        var env = new HENV { Handle = new IntPtr(7) };
        var result = F9860QueryEngine.InitializeUserContextCore(
            env,
            _ => (JDEDB_PASSED, new HUSER { Handle = IntPtr.Zero }),
            null);

        await Assert.That(result.Status).IsEqualTo(F9860QueryEngine.UserInitStatus.InvalidHandle);
    }

    [Test]
    public async Task ReadValueFromPointer_ReturnsFixedValue()
    {
        IntPtr buffer = Marshal.AllocHGlobal(8);
        try
        {
            var bytes = System.Text.Encoding.Unicode.GetBytes("AB");
            Marshal.Copy(bytes, 0, buffer, bytes.Length);

            var value = F9860QueryEngine.ReadValueFromPointer(buffer, columnLength: 2, log: null);
            await Assert.That(value).IsEqualTo("AB");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task ReadValueFromPointer_FallsBackToUnicode()
    {
        IntPtr buffer = Marshal.StringToHGlobalUni("Hello");
        try
        {
            var value = F9860QueryEngine.ReadValueFromPointer(buffer, columnLength: 0, log: null);
            await Assert.That(value).IsEqualTo("Hello");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task ReadUnicodeAndAnsiValue_ReturnEmptyWhenPointerEmpty()
    {
        IntPtr buffer = Marshal.AllocHGlobal(2);
        try
        {
            Marshal.WriteInt16(buffer, 0);

            var unicode = F9860QueryEngine.ReadUnicodeValue(buffer, log: null);
            var ansi = F9860QueryEngine.ReadAnsiValue(buffer, log: null);

            await Assert.That(unicode).IsEqualTo(string.Empty);
            await Assert.That(ansi).IsEqualTo(string.Empty);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task AddPatternFilter_IgnoresWhitespace()
    {
        var filters = new List<(string Column, string Value, int Comparison)>();
        F9860QueryEngine.AddPatternFilter(filters, F9860Structures.Columns.OBNM, "   ");
        await Assert.That(filters.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TryGetObjectTypeCode_ReturnsFalse_ForAllAndUnknown()
    {
        var allResult = F9860QueryEngine.TryGetObjectTypeCode(JdeObjectType.All, out var allCode);
        await Assert.That(allResult).IsFalse();
        await Assert.That(allCode).IsEqualTo(string.Empty);

        var unknownResult = F9860QueryEngine.TryGetObjectTypeCode(JdeObjectType.Unknown, out var unknownCode);
        await Assert.That(unknownResult).IsFalse();
        await Assert.That(unknownCode).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ShouldSkipObject_ReturnsExpected()
    {
        var info = new JdeObjectInfo { ObjectType = F9860Structures.ObjectTypes.Table };

        await Assert.That(F9860QueryEngine.ShouldSkipObject(info, null)).IsFalse();
        await Assert.That(F9860QueryEngine.ShouldSkipObject(info, JdeObjectType.Table)).IsFalse();
        await Assert.That(F9860QueryEngine.ShouldSkipObject(info, JdeObjectType.BusinessFunction)).IsTrue();
    }

    [Test]
    public async Task ShouldStopAfterMax_RespectsMaxResults()
    {
        await Assert.That(F9860QueryEngine.ShouldStopAfterMax(5, 0)).IsFalse();
        await Assert.That(F9860QueryEngine.ShouldStopAfterMax(4, 5)).IsFalse();
        await Assert.That(F9860QueryEngine.ShouldStopAfterMax(5, 5)).IsTrue();
    }

    [Test]
    public async Task MatchesObjectType_ReturnsExpectedMatch()
    {
        await Assert.That(F9860QueryEngine.MatchesObjectType("TBLE", JdeObjectType.Table)).IsTrue();
        await Assert.That(F9860QueryEngine.MatchesObjectType("BSFN", JdeObjectType.Table)).IsFalse();
    }
}
