using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using JdeClient.Core;
using JdeClient.Core.Internal;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.Internal;

public class F9860QueryEngineTests
{
    [Test]
    public async Task Constructor_NullOptions_Throws()
    {
        var exception = await Assert.That(() => new F9860QueryEngine(null!))
            .ThrowsExactly<ArgumentNullException>();

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.ParamName).IsEqualTo("options");
    }

    [Test]
    public async Task Constructor_ValidOptions_Creates()
    {
        var engine = new F9860QueryEngine(new JdeClientOptions());
        await Assert.That(engine).IsNotNull();
    }

    [Test]
    public async Task HandleProperties_ExposeBackedFields()
    {
        var engine = new F9860QueryEngine(new JdeClientOptions());
        SetPrivateField(engine, "_hUser", new HUSER { Handle = new IntPtr(11) });
        SetPrivateField(engine, "_hEnv", new HENV { Handle = new IntPtr(22) });

        await Assert.That(engine.UserHandle.Handle).IsEqualTo(new IntPtr(11));
        await Assert.That(engine.EnvironmentHandle.Handle).IsEqualTo(new IntPtr(22));
    }

    [Test]
    public async Task Initialize_WhenAlreadyInitialized_ReturnsWithoutInterop()
    {
        var engine = new F9860QueryEngine(new JdeClientOptions());
        SetPrivateField(engine, "_isInitialized", true);

        engine.Initialize();

        await Assert.That(GetPrivateField<bool>(engine, "_isInitialized")).IsTrue();
    }

    [Test]
    public async Task Dispose_WhenInitializedButNotOwned_ClearsInitializedState()
    {
        var engine = new F9860QueryEngine(new JdeClientOptions());
        SetPrivateField(engine, "_isInitialized", true);
        SetPrivateField(engine, "_ownsUser", false);
        SetPrivateField(engine, "_ownsEnv", false);
        SetPrivateField(engine, "_hUser", new HUSER { Handle = new IntPtr(1) });
        SetPrivateField(engine, "_hEnv", new HENV { Handle = new IntPtr(2) });

        engine.Dispose();

        await Assert.That(GetPrivateField<bool>(engine, "_isInitialized")).IsFalse();
        await Assert.That(GetPrivateField<bool>(engine, "_disposed")).IsTrue();
    }

    [Test]
    public async Task Dispose_DoubleDispose_DoesNotThrow()
    {
        var engine = new F9860QueryEngine(new JdeClientOptions());

        engine.Dispose();
        engine.Dispose();

        await Assert.That(GetPrivateField<bool>(engine, "_disposed")).IsTrue();
    }

    [Test]
    public async Task DebugLog_Enabled_WritesMessage()
    {
        var messages = new List<string>();
        var options = new JdeClientOptions
        {
            EnableDebug = true,
            LogSink = messages.Add
        };
        var engine = new F9860QueryEngine(options);

        InvokePrivateVoid(engine, "DebugLog", "hello");

        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(messages[0]).IsEqualTo("hello");
    }

    [Test]
    public async Task DebugLog_Disabled_DoesNotWriteMessage()
    {
        var messages = new List<string>();
        var options = new JdeClientOptions
        {
            EnableDebug = false,
            LogSink = messages.Add
        };
        var engine = new F9860QueryEngine(options);

        InvokePrivateVoid(engine, "DebugLog", "hello");

        await Assert.That(messages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task F9860Layout_WhenDisabled_ReturnsNull()
    {
        var engine = new F9860QueryEngine(new JdeClientOptions { UseRowLayoutF9860 = false });
        var property = typeof(F9860QueryEngine).GetProperty("F9860Layout", BindingFlags.Instance | BindingFlags.NonPublic);
        var value = property!.GetValue(engine);

        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task FreeEnvIfOwned_WhenNotOwned_DoesNothing()
    {
        var engine = new F9860QueryEngine(new JdeClientOptions());
        SetPrivateField(engine, "_ownsEnv", false);

        InvokePrivateVoid(engine, "FreeEnvIfOwned");

        await Assert.That(GetPrivateField<bool>(engine, "_ownsEnv")).IsFalse();
    }

    [Test]
    public async Task HandleFetchResult_NoMoreDataWithRecords_BreaksWithoutInterop()
    {
        var engine = new F9860QueryEngine(new JdeClientOptions());
        var args = new object?[]
        {
            JDEDB_NO_MORE_DATA,
            new HREQUEST(),
            null,
            0,
            new List<JdeObjectInfo>(),
            1,
            0,
            0
        };

        var outcome = (F9860QueryEngine.FetchOutcome)InvokePrivate(engine, "HandleFetchResult", args)!;

        await Assert.That(outcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Break);
        await Assert.That((int)args[5]!).IsEqualTo(1);
    }

    [Test]
    public async Task HandleFetchResult_Skipped_Continues()
    {
        var engine = new F9860QueryEngine(new JdeClientOptions());
        var args = new object?[]
        {
            JDEDB_SKIPPED,
            new HREQUEST(),
            null,
            0,
            new List<JdeObjectInfo>(),
            0,
            0,
            0
        };

        var outcome = (F9860QueryEngine.FetchOutcome)InvokePrivate(engine, "HandleFetchResult", args)!;

        await Assert.That(outcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Continue);
    }

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
    public async Task ShouldForceSystemTableFallback_NullOrWhitespace_ReturnsFalse()
    {
        await Assert.That(F9860QueryEngine.ShouldForceSystemTableFallback(null)).IsFalse();
        await Assert.That(F9860QueryEngine.ShouldForceSystemTableFallback("   ")).IsFalse();
    }

    [Test]
    public async Task ShouldForceSystemTableFallback_ObjectLibrarianPrefix_ReturnsTrue()
    {
        await Assert.That(F9860QueryEngine.ShouldForceSystemTableFallback("Object Librarian - PY920")).IsTrue();
        await Assert.That(F9860QueryEngine.ShouldForceSystemTableFallback("object librarian - dv920")).IsTrue();
    }

    [Test]
    public async Task ShouldForceSystemTableFallback_OtherDataSource_ReturnsFalse()
    {
        await Assert.That(F9860QueryEngine.ShouldForceSystemTableFallback("Central Objects - PY920")).IsFalse();
        await Assert.That(F9860QueryEngine.ShouldForceSystemTableFallback("JDEPY")).IsFalse();
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

    [Test]
    public async Task MatchesObjectType_AllEnumValues()
    {
        await Assert.That(F9860QueryEngine.MatchesObjectType("BSFN", JdeObjectType.BusinessFunction)).IsTrue();
        await Assert.That(F9860QueryEngine.MatchesObjectType("BL", JdeObjectType.BusinessFunctionLibrary)).IsTrue();
        await Assert.That(F9860QueryEngine.MatchesObjectType("UBE", JdeObjectType.Report)).IsTrue();
        await Assert.That(F9860QueryEngine.MatchesObjectType("APPL", JdeObjectType.Application)).IsTrue();
        await Assert.That(F9860QueryEngine.MatchesObjectType("DSTR", JdeObjectType.DataStructure)).IsTrue();
        await Assert.That(F9860QueryEngine.MatchesObjectType("GT", JdeObjectType.MediaObjectDataStructure)).IsTrue();
        await Assert.That(F9860QueryEngine.MatchesObjectType("BSVW", JdeObjectType.BusinessView)).IsTrue();
        await Assert.That(F9860QueryEngine.MatchesObjectType("TBLE", JdeObjectType.All)).IsTrue();
        await Assert.That(F9860QueryEngine.MatchesObjectType("TBLE", JdeObjectType.Unknown)).IsFalse();
    }

    [Test]
    public async Task ShouldApplyFilter_AllNull_ReturnsFalse()
    {
        await Assert.That(F9860QueryEngine.ShouldApplyFilter(null, null, null)).IsFalse();
    }

    [Test]
    public async Task ShouldApplyFilter_NameOnly_ReturnsTrue()
    {
        await Assert.That(F9860QueryEngine.ShouldApplyFilter(null, "F01*", null)).IsTrue();
    }

    [Test]
    public async Task ShouldApplyFilter_DescOnly_ReturnsTrue()
    {
        await Assert.That(F9860QueryEngine.ShouldApplyFilter(null, null, "Address")).IsTrue();
    }

    [Test]
    public async Task ShouldApplyFilter_TypeOnly_ReturnsTrue()
    {
        await Assert.That(F9860QueryEngine.ShouldApplyFilter(JdeObjectType.Table, null, null)).IsTrue();
    }

    [Test]
    public async Task ShouldApplyFilter_AllType_ReturnsFalse()
    {
        await Assert.That(F9860QueryEngine.ShouldApplyFilter(JdeObjectType.All, null, null)).IsFalse();
    }

    [Test]
    public async Task TryGetObjectTypeCode_AllNamedEnumValues()
    {
        await AssertTypeCode(JdeObjectType.Table, F9860Structures.ObjectTypes.Table);
        await AssertTypeCode(JdeObjectType.BusinessFunction, F9860Structures.ObjectTypes.BusinessFunction);
        await AssertTypeCode(JdeObjectType.BusinessFunctionLibrary, F9860Structures.ObjectTypes.BusinessFunctionLibrary);
        await AssertTypeCode(JdeObjectType.Report, F9860Structures.ObjectTypes.Report);
        await AssertTypeCode(JdeObjectType.Application, F9860Structures.ObjectTypes.Application);
        await AssertTypeCode(JdeObjectType.DataStructure, F9860Structures.ObjectTypes.DataStructure);
        await AssertTypeCode(JdeObjectType.MediaObjectDataStructure, F9860Structures.ObjectTypes.MediaObjectDataStructure);
        await AssertTypeCode(JdeObjectType.BusinessView, F9860Structures.ObjectTypes.BusinessView);

        static async Task AssertTypeCode(JdeObjectType objectType, string expected)
        {
            var result = F9860QueryEngine.TryGetObjectTypeCode(objectType, out var code);
            await Assert.That(result).IsTrue();
            await Assert.That(code).IsEqualTo(expected);
        }
    }

    [Test]
    public async Task AddPatternFilter_ValueWithWildcard_UsesLike()
    {
        var filters = new List<(string Column, string Value, int Comparison)>();
        F9860QueryEngine.AddPatternFilter(filters, F9860Structures.Columns.OBNM, "F01*");

        await Assert.That(filters.Count).IsEqualTo(1);
        await Assert.That(filters[0].Value).IsEqualTo("F01%");
        await Assert.That(filters[0].Comparison).IsEqualTo(JDEDB_CMP_LK);
    }

    [Test]
    public async Task AddPatternFilter_ValueWithoutWildcard_UsesEqual()
    {
        var filters = new List<(string Column, string Value, int Comparison)>();
        F9860QueryEngine.AddPatternFilter(filters, F9860Structures.Columns.OBNM, "F0101");

        await Assert.That(filters.Count).IsEqualTo(1);
        await Assert.That(filters[0].Value).IsEqualTo("F0101");
        await Assert.That(filters[0].Comparison).IsEqualTo(JDEDB_CMP_EQ);
    }

    [Test]
    public async Task AddPatternFilter_NullValue_AddsNothing()
    {
        var filters = new List<(string Column, string Value, int Comparison)>();
        F9860QueryEngine.AddPatternFilter(filters, F9860Structures.Columns.OBNM, null);
        await Assert.That(filters.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReadJCharString_ValidPtr_ReturnsDecodedString()
    {
        IntPtr buffer = Marshal.AllocHGlobal(20);
        try
        {
            var bytes = System.Text.Encoding.Unicode.GetBytes("HELLO");
            Marshal.Copy(bytes, 0, buffer, bytes.Length);

            var value = F9860QueryEngine.ReadJCharString(buffer, 5);
            await Assert.That(value).IsEqualTo("HELLO");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task ReadJCharString_ZeroLength_ReturnsEmpty()
    {
        IntPtr buffer = Marshal.AllocHGlobal(4);
        try
        {
            var value = F9860QueryEngine.ReadJCharString(buffer, 0);
            await Assert.That(value).IsEqualTo(string.Empty);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task ReadJCharString_NullPtr_ReturnsEmpty()
    {
        var value = F9860QueryEngine.ReadJCharString(IntPtr.Zero, 5);
        await Assert.That(value).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task CreateColumnDbRef_SetsTableAndColumn()
    {
        var dbRef = F9860QueryEngine.CreateColumnDbRef("OBNM");
        await Assert.That(dbRef.szTable.Value).IsEqualTo("F9860");
        await Assert.That(dbRef.szDict.Value).IsEqualTo("OBNM");
        await Assert.That(dbRef.idInstance).IsEqualTo(0);
    }

    [Test]
    public async Task TryReadFixedValue_ValidLength_ReadsValue()
    {
        IntPtr buffer = Marshal.AllocHGlobal(20);
        try
        {
            var bytes = System.Text.Encoding.Unicode.GetBytes("AB");
            Marshal.Copy(bytes, 0, buffer, bytes.Length);

            var value = F9860QueryEngine.TryReadFixedValue(buffer, 2, null);
            await Assert.That(value).IsEqualTo("AB");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task TryReadFixedValue_ZeroLength_ReturnsEmpty()
    {
        IntPtr buffer = Marshal.AllocHGlobal(4);
        try
        {
            var value = F9860QueryEngine.TryReadFixedValue(buffer, 0, null);
            await Assert.That(value).IsEqualTo(string.Empty);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task TryReadFixedValue_LengthOver4096_ReturnsEmpty()
    {
        IntPtr buffer = Marshal.AllocHGlobal(4);
        try
        {
            var value = F9860QueryEngine.TryReadFixedValue(buffer, 5000, null);
            await Assert.That(value).IsEqualTo(string.Empty);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task LogBufferHex_DoesNotThrow()
    {
        IntPtr buffer = Marshal.AllocHGlobal(4);
        try
        {
            Marshal.WriteByte(buffer, 0, 0x41);
            Marshal.WriteByte(buffer, 1, 0);
            var messages = new List<string>();
            F9860QueryEngine.LogBufferHex(buffer, msg => messages.Add(msg));
            await Assert.That(messages.Count).IsEqualTo(1);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task LogValue_InvokesLog()
    {
        var messages = new List<string>();
        F9860QueryEngine.LogValue(msg => messages.Add(msg), "Test", "hello");
        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(messages[0].Contains("hello", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task ResolveDriverRequest_ReturnsSameHandle()
    {
        var hRequest = new HREQUEST { Handle = new IntPtr(42) };
        var result = F9860QueryEngine.ResolveDriverRequest(hRequest);
        await Assert.That(result.Handle).IsEqualTo(hRequest.Handle);
    }

    [Test]
    public async Task NormalizeText_OnlyNullChars_ReturnsEmpty()
    {
        var value = F9860QueryEngine.NormalizeText("\0\0\0");
        await Assert.That(value).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task BuildColumnLengthMap_DuplicateNames_LastWins()
    {
        var columns = new[]
        {
            new JdeColumn { Name = "AN8", Length = 10 },
            new JdeColumn { Name = "AN8", Length = 20 }
        };

        var map = F9860QueryEngine.BuildColumnLengthMap(columns);
        await Assert.That(map["AN8"]).IsEqualTo(20);
    }

    [Test]
    public async Task ExtractObjectInfoCore_AllColumnsReturned_ReturnsValidInfo()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [F9860Structures.Columns.OBNM] = "F0101",
            [F9860Structures.Columns.FUNO] = "TBLE",
            [F9860Structures.Columns.SY] = "01",
            [F9860Structures.Columns.MD] = "Address Book Master"
        };

        var result = F9860QueryEngine.ExtractObjectInfoCore(
            (col, _) => values.TryGetValue(col, out var v) ? v : string.Empty,
            _ => 10,
            null);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.ObjectName).IsEqualTo("F0101");
        await Assert.That(result.ObjectType).IsEqualTo("TBLE");
        await Assert.That(result.SystemCode).IsEqualTo("01");
        await Assert.That(result.Description).IsEqualTo("Address Book Master");
    }

    [Test]
    public async Task ExtractObjectInfoCore_EmptyObjectName_ReturnsNull()
    {
        var result = F9860QueryEngine.ExtractObjectInfoCore(
            (_, _) => string.Empty,
            _ => 10,
            null);

        await Assert.That(result is null).IsTrue();
    }

    [Test]
    public async Task ExtractObjectInfoCore_EmptyObjectType_ReturnsNull()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [F9860Structures.Columns.OBNM] = "F0101",
            [F9860Structures.Columns.FUNO] = "",
            [F9860Structures.Columns.SY] = "01",
            [F9860Structures.Columns.MD] = "Test"
        };

        var result = F9860QueryEngine.ExtractObjectInfoCore(
            (col, _) => values.TryGetValue(col, out var v) ? v : string.Empty,
            _ => 10,
            null);

        await Assert.That(result is null).IsTrue();
    }

    [Test]
    public async Task ExtractObjectInfoCore_ThrowingDelegate_ReturnsNull()
    {
        var result = F9860QueryEngine.ExtractObjectInfoCore(
            (_, _) => throw new InvalidOperationException("test error"),
            _ => 10,
            null);

        await Assert.That(result is null).IsTrue();
    }

    [Test]
    public async Task HandleFetchedObjectCore_NullObject_IncrementsStreak()
    {
        var results = new List<JdeObjectInfo>();
        int recordCount = 0;
        int nullStreak = 2;

        var outcome = F9860QueryEngine.HandleFetchedObjectCore(
            null,
            null,
            maxResults: 0,
            results,
            ref recordCount,
            ref nullStreak,
            null);

        await Assert.That(outcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Continue);
        await Assert.That(nullStreak).IsEqualTo(3);
        await Assert.That(recordCount).IsEqualTo(0);
    }

    [Test]
    public async Task HandleFetchedObjectCore_NullStreakAtLimit_Breaks()
    {
        var results = new List<JdeObjectInfo>();
        int recordCount = 0;
        int nullStreak = 999;

        var outcome = F9860QueryEngine.HandleFetchedObjectCore(
            null,
            null,
            maxResults: 0,
            results,
            ref recordCount,
            ref nullStreak,
            null);

        await Assert.That(outcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Break);
        await Assert.That(nullStreak).IsEqualTo(1000);
    }

    [Test]
    public async Task HandleFetchedObjectCore_SkippedType_DoesNotAdd()
    {
        var results = new List<JdeObjectInfo>();
        int recordCount = 0;
        int nullStreak = 0;
        var info = new JdeObjectInfo { ObjectName = "F0101", ObjectType = F9860Structures.ObjectTypes.Table };

        var outcome = F9860QueryEngine.HandleFetchedObjectCore(
            info,
            JdeObjectType.BusinessFunction,
            maxResults: 0,
            results,
            ref recordCount,
            ref nullStreak,
            null);

        await Assert.That(outcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Continue);
        await Assert.That(results.Count).IsEqualTo(0);
        await Assert.That(recordCount).IsEqualTo(0);
    }

    [Test]
    public async Task HandleFetchedObjectCore_AddsResultAndResetsStreak()
    {
        var results = new List<JdeObjectInfo>();
        int recordCount = 0;
        int nullStreak = 5;
        var info = new JdeObjectInfo { ObjectName = "F0101", ObjectType = F9860Structures.ObjectTypes.Table };

        var outcome = F9860QueryEngine.HandleFetchedObjectCore(
            info,
            null,
            maxResults: 0,
            results,
            ref recordCount,
            ref nullStreak,
            null);

        await Assert.That(outcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Continue);
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(recordCount).IsEqualTo(1);
        await Assert.That(nullStreak).IsEqualTo(0);
    }

    [Test]
    public async Task HandleFetchedObjectCore_RespectsMaxResults()
    {
        var results = new List<JdeObjectInfo>();
        int recordCount = 4;
        int nullStreak = 0;
        var info = new JdeObjectInfo { ObjectName = "F0101", ObjectType = F9860Structures.ObjectTypes.Table };

        var outcome = F9860QueryEngine.HandleFetchedObjectCore(
            info,
            null,
            maxResults: 5,
            results,
            ref recordCount,
            ref nullStreak,
            null);

        await Assert.That(outcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Break);
        await Assert.That(recordCount).IsEqualTo(5);
    }

    [Test]
    public async Task HandleFetchedObjectCore_LogsFirstThree()
    {
        var results = new List<JdeObjectInfo>();
        int recordCount = 0;
        int nullStreak = 0;
        var info = new JdeObjectInfo { ObjectName = "F0101", ObjectType = F9860Structures.ObjectTypes.Table };
        var messages = new List<string>();

        F9860QueryEngine.HandleFetchedObjectCore(
            info,
            null,
            maxResults: 0,
            results,
            ref recordCount,
            ref nullStreak,
            msg => messages.Add(msg));

        await Assert.That(messages.Count).IsEqualTo(1);

        messages.Clear();
        recordCount = 3;
        F9860QueryEngine.HandleFetchedObjectCore(
            info,
            null,
            maxResults: 0,
            results,
            ref recordCount,
            ref nullStreak,
            msg => messages.Add(msg));

        await Assert.That(messages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task HandleFetchFailureCore_UnderLimit_ReturnsTrue()
    {
        int failureCount = 0;

        var shouldContinue = F9860QueryEngine.HandleFetchFailureCore(ref failureCount, maxFailures: 3);

        await Assert.That(shouldContinue).IsTrue();
        await Assert.That(failureCount).IsEqualTo(1);
    }

    [Test]
    public async Task HandleFetchFailureCore_AtLimit_ReturnsFalse()
    {
        int failureCount = 2;

        var shouldContinue = F9860QueryEngine.HandleFetchFailureCore(ref failureCount, maxFailures: 3);

        await Assert.That(shouldContinue).IsFalse();
        await Assert.That(failureCount).IsEqualTo(3);
    }

    [Test]
    public async Task HandleFetchResultCore_NoMoreData_Breaks()
    {
        bool noMoreDataCalled = false;

        var outcome = F9860QueryEngine.HandleFetchResultCore(
            JDEDB_NO_MORE_DATA,
            () => F9860QueryEngine.FetchOutcome.Continue,
            () => true,
            () => noMoreDataCalled = true,
            null);

        await Assert.That(outcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Break);
        await Assert.That(noMoreDataCalled).IsTrue();
    }

    [Test]
    public async Task HandleFetchResultCore_Skipped_Continues()
    {
        var outcome = F9860QueryEngine.HandleFetchResultCore(
            JDEDB_SKIPPED,
            () => F9860QueryEngine.FetchOutcome.Break,
            () => false,
            null,
            null);

        await Assert.That(outcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Continue);
    }

    [Test]
    public async Task HandleFetchResultCore_FailureDelegates_ControlOutcome()
    {
        var continueOutcome = F9860QueryEngine.HandleFetchResultCore(
            -1,
            () => F9860QueryEngine.FetchOutcome.Break,
            () => true,
            null,
            null);

        var breakOutcome = F9860QueryEngine.HandleFetchResultCore(
            -1,
            () => F9860QueryEngine.FetchOutcome.Continue,
            () => false,
            null,
            null);

        await Assert.That(continueOutcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Continue);
        await Assert.That(breakOutcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Break);
    }

    [Test]
    public async Task HandleFetchResultCore_Success_ReturnsOutcome()
    {
        var outcome = F9860QueryEngine.HandleFetchResultCore(
            JDEDB_PASSED,
            () => F9860QueryEngine.FetchOutcome.Break,
            () => false,
            null,
            null);

        await Assert.That(outcome).IsEqualTo(F9860QueryEngine.FetchOutcome.Break);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return (T)field!.GetValue(target)!;
    }

    private static void InvokePrivateVoid(object target, string methodName, params object?[] args)
    {
        _ = InvokePrivate(target, methodName, args);
    }

    private static object? InvokePrivate(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        return method!.Invoke(target, args);
    }
}
