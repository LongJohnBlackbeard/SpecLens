using System.Runtime.InteropServices;
using JdeClient.Core;
using JdeClient.Core.Internal;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.Internal;

public class SpecBusinessViewMetadataServiceTests
{
    [Test]
    public async Task NormalizeText_RemovesNullAndTrims()
    {
        var value = SpecBusinessViewMetadataService.NormalizeText("  ABC\0  ");
        await Assert.That(value).IsEqualTo("ABC");
    }

    [Test]
    public async Task NormalizeNid_UsesFallbackWhenEmpty()
    {
        var value = SpecBusinessViewMetadataService.NormalizeNid(new NID(string.Empty), "FALLBACK");
        await Assert.That(value).IsEqualTo("FALLBACK");
    }

    [Test]
    public async Task NormalizeNid_ReturnsValueWhenPresent()
    {
        var value = SpecBusinessViewMetadataService.NormalizeNid(new NID("TEST"), "FALLBACK");
        await Assert.That(value).IsEqualTo("TEST");
    }

    [Test]
    public async Task NormalizeText_Whitespace_ReturnsEmpty()
    {
        var value = SpecBusinessViewMetadataService.NormalizeText("   ");
        await Assert.That(value).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task FormatJoinOperator_MapsKnownAndUnknownValues()
    {
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinOperator(0)).IsEqualTo("=");
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinOperator(9)).IsEqualTo("op 9");
    }

    [Test]
    public async Task FormatJoinType_MapsKnownAndUnknownValues()
    {
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinType(0)).IsEqualTo("Inner");
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinType(9)).IsEqualTo("Type 9");
    }

    [Test]
    public async Task GetBusinessViewInfo_EmptyName_ReturnsNull()
    {
        var service = new SpecBusinessViewMetadataService(new HUSER(), new JdeClientOptions());
        var result = service.GetBusinessViewInfo(" ");
        await Assert.That(result is null).IsTrue();
    }

    [Test]
    public async Task FormatJoinOperator_AllKnownValues()
    {
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinOperator(1)).IsEqualTo("<");
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinOperator(2)).IsEqualTo(">");
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinOperator(3)).IsEqualTo("<=");
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinOperator(4)).IsEqualTo(">=");
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinOperator(5)).IsEqualTo("!=");
    }

    [Test]
    public async Task FormatJoinType_AllKnownValues()
    {
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinType(1)).IsEqualTo("Left Outer");
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinType(2)).IsEqualTo("Right Outer");
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinType(3)).IsEqualTo("Outer");
        await Assert.That(SpecBusinessViewMetadataService.FormatJoinType(4)).IsEqualTo("Left Outer (SQL92)");
    }

    [Test]
    public async Task Dispose_DoubleSafe()
    {
        var service = new SpecBusinessViewMetadataService(new HUSER(), new JdeClientOptions());
        service.Dispose();
        service.Dispose(); // Should not throw
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task ReadTables_EmptyPtr_ReturnsEmptyList()
    {
        var tables = new List<JdeBusinessViewTable>();
        SpecBusinessViewMetadataService.ReadTables(IntPtr.Zero, 5, tables);
        await Assert.That(tables.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReadTables_ZeroCount_ReturnsEmptyList()
    {
        var tables = new List<JdeBusinessViewTable>();
        IntPtr fakePtr = Marshal.AllocHGlobal(100);
        try
        {
            SpecBusinessViewMetadataService.ReadTables(fakePtr, 0, tables);
            await Assert.That(tables.Count).IsEqualTo(0);
        }
        finally
        {
            Marshal.FreeHGlobal(fakePtr);
        }
    }

    [Test]
    public async Task ReadColumns_EmptyPtr_ReturnsEmptyList()
    {
        var columns = new List<JdeBusinessViewColumn>();
        SpecBusinessViewMetadataService.ReadColumns(IntPtr.Zero, 5, columns);
        await Assert.That(columns.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReadColumns_ZeroCount_ReturnsEmptyList()
    {
        var columns = new List<JdeBusinessViewColumn>();
        IntPtr fakePtr = Marshal.AllocHGlobal(100);
        try
        {
            SpecBusinessViewMetadataService.ReadColumns(fakePtr, 0, columns);
            await Assert.That(columns.Count).IsEqualTo(0);
        }
        finally
        {
            Marshal.FreeHGlobal(fakePtr);
        }
    }

    [Test]
    public async Task ReadJoins_EmptyPtr_ReturnsEmptyList()
    {
        var joins = new List<JdeBusinessViewJoin>();
        SpecBusinessViewMetadataService.ReadJoins(IntPtr.Zero, 5, joins);
        await Assert.That(joins.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ReadJoins_ZeroCount_ReturnsEmptyList()
    {
        var joins = new List<JdeBusinessViewJoin>();
        IntPtr fakePtr = Marshal.AllocHGlobal(100);
        try
        {
            SpecBusinessViewMetadataService.ReadJoins(fakePtr, 0, joins);
            await Assert.That(joins.Count).IsEqualTo(0);
        }
        finally
        {
            Marshal.FreeHGlobal(fakePtr);
        }
    }

    [Test]
    public async Task ReadTables_ValidData_ParsesCorrectly()
    {
        int tableSize = Marshal.SizeOf<BOB_TABLE>();
        IntPtr tablesPtr = Marshal.AllocHGlobal(tableSize);
        try
        {
            var table = new BOB_TABLE
            {
                idFormatNum = new ID(1),
                idPrimaryIndex = new ID(5),
                nNumInstances = 2,
                szTable = new NID("F0101"),
                nativeAlignmentPadding = IntPtr.Zero
            };
            Marshal.StructureToPtr(table, tablesPtr, false);

            var tables = new List<JdeBusinessViewTable>();
            SpecBusinessViewMetadataService.ReadTables(tablesPtr, 1, tables);

            await Assert.That(tables.Count).IsEqualTo(1);
            await Assert.That(tables[0].TableName).IsEqualTo("F0101");
            await Assert.That(tables[0].InstanceCount).IsEqualTo(2);
            await Assert.That(tables[0].PrimaryIndexId).IsEqualTo(5);
        }
        finally
        {
            Marshal.FreeHGlobal(tablesPtr);
        }
    }

    [Test]
    public async Task ReadTables_BlankName_SkipsEntry()
    {
        int tableSize = Marshal.SizeOf<BOB_TABLE>();
        IntPtr tablesPtr = Marshal.AllocHGlobal(tableSize);
        try
        {
            var table = new BOB_TABLE
            {
                idFormatNum = new ID(1),
                idPrimaryIndex = new ID(5),
                nNumInstances = 1,
                szTable = new NID(""),
                nativeAlignmentPadding = IntPtr.Zero
            };
            Marshal.StructureToPtr(table, tablesPtr, false);

            var tables = new List<JdeBusinessViewTable>();
            SpecBusinessViewMetadataService.ReadTables(tablesPtr, 1, tables);

            await Assert.That(tables.Count).IsEqualTo(0);
        }
        finally
        {
            Marshal.FreeHGlobal(tablesPtr);
        }
    }

    [Test]
    public async Task ReadColumns_ValidData_ParsesCorrectly()
    {
        int columnSize = Marshal.SizeOf<BOB_COLUMN>();
        IntPtr columnsPtr = Marshal.AllocHGlobal(columnSize);
        try
        {
            var column = new BOB_COLUMN
            {
                idFormatNum = new ID(1),
                idInstance = new ID(0),
                iFlags = 0,
                nSeq = 1,
                cType = 'C',
                idEvType = new ID(9),
                cClass = 'A',
                idLength = new ID(8),
                nDecimals = 0,
                nDispDecimals = 0,
                idHelpText = new ID(0),
                szTable = new NID("F0101"),
                szDict = new NID("AN8"),
                nativeAlignmentPadding = IntPtr.Zero
            };
            Marshal.StructureToPtr(column, columnsPtr, false);

            var columns = new List<JdeBusinessViewColumn>();
            SpecBusinessViewMetadataService.ReadColumns(columnsPtr, 1, columns);

            await Assert.That(columns.Count).IsEqualTo(1);
            await Assert.That(columns[0].DataItem).IsEqualTo("AN8");
            await Assert.That(columns[0].TableName).IsEqualTo("F0101");
            await Assert.That(columns[0].Sequence).IsEqualTo(1);
            await Assert.That(columns[0].DataType).IsEqualTo(9);
            await Assert.That(columns[0].Length).IsEqualTo(8);
            await Assert.That(columns[0].TypeCode).IsEqualTo('C');
            await Assert.That(columns[0].ClassCode).IsEqualTo('A');
        }
        finally
        {
            Marshal.FreeHGlobal(columnsPtr);
        }
    }

    [Test]
    public async Task ReadColumns_BlankDataItem_SkipsEntry()
    {
        int columnSize = Marshal.SizeOf<BOB_COLUMN>();
        IntPtr columnsPtr = Marshal.AllocHGlobal(columnSize);
        try
        {
            var column = new BOB_COLUMN
            {
                idFormatNum = new ID(1),
                idInstance = new ID(0),
                szTable = new NID("F0101"),
                szDict = new NID(""),
                nativeAlignmentPadding = IntPtr.Zero
            };
            Marshal.StructureToPtr(column, columnsPtr, false);

            var columns = new List<JdeBusinessViewColumn>();
            SpecBusinessViewMetadataService.ReadColumns(columnsPtr, 1, columns);

            await Assert.That(columns.Count).IsEqualTo(0);
        }
        finally
        {
            Marshal.FreeHGlobal(columnsPtr);
        }
    }

    [Test]
    public async Task ReadJoins_ValidData_ParsesCorrectly()
    {
        int joinSize = Marshal.SizeOf<BOB_JOIN>();
        IntPtr joinsPtr = Marshal.AllocHGlobal(joinSize);
        try
        {
            var join = new BOB_JOIN
            {
                idFormatNum = new ID(1),
                idFInstance = new ID(0),
                idPInstance = new ID(0),
                chOperator = 0,
                chType = 1,
                szFTable = new NID("F0101"),
                szFDict = new NID("AN8"),
                szPTable = new NID("F0116"),
                szPDict = new NID("AN8"),
                nativeAlignmentPadding = IntPtr.Zero
            };
            Marshal.StructureToPtr(join, joinsPtr, false);

            var joins = new List<JdeBusinessViewJoin>();
            SpecBusinessViewMetadataService.ReadJoins(joinsPtr, 1, joins);

            await Assert.That(joins.Count).IsEqualTo(1);
            await Assert.That(joins[0].ForeignTable).IsEqualTo("F0101");
            await Assert.That(joins[0].ForeignColumn).IsEqualTo("AN8");
            await Assert.That(joins[0].PrimaryTable).IsEqualTo("F0116");
            await Assert.That(joins[0].PrimaryColumn).IsEqualTo("AN8");
            await Assert.That(joins[0].JoinOperator).IsEqualTo("=");
            await Assert.That(joins[0].JoinType).IsEqualTo("Left Outer");
        }
        finally
        {
            Marshal.FreeHGlobal(joinsPtr);
        }
    }

    [Test]
    public async Task GetBusinessViewInfoCore_FailedResult_ReturnsNull()
    {
        bool freed = false;

        var result = SpecBusinessViewMetadataService.GetBusinessViewInfoCore(
            "V0101A",
            (NID nid, out IntPtr ptr) => { ptr = IntPtr.Zero; return JDEDB_FAILED; },
            _ => freed = true,
            null);

        await Assert.That(result is null).IsTrue();
        await Assert.That(freed).IsFalse();
    }

    [Test]
    public async Task GetBusinessViewInfoCore_NullBobPtr_ReturnsNull()
    {
        var result = SpecBusinessViewMetadataService.GetBusinessViewInfoCore(
            "V0101A",
            (NID nid, out IntPtr ptr) => { ptr = IntPtr.Zero; return JDEDB_PASSED; },
            _ => { },
            null);

        await Assert.That(result is null).IsTrue();
    }

    [Test]
    public async Task GetBusinessViewInfoCore_NullHeaderPtr_ReturnsNull()
    {
        // Allocate a BOB structure with lpHeader = IntPtr.Zero
        int bobSize = Marshal.SizeOf<BOB>();
        IntPtr bobPtr = Marshal.AllocHGlobal(bobSize);
        bool freed = false;
        try
        {
            var bob = new BOB
            {
                idVersion = new ID(1),
                lpHeader = IntPtr.Zero,
                lpTables = IntPtr.Zero,
                lpDBRefs = IntPtr.Zero,
                lpColumns = IntPtr.Zero,
                lpJoins = IntPtr.Zero
            };
            Marshal.StructureToPtr(bob, bobPtr, false);

            IntPtr capturedBobPtr = bobPtr;
            var result = SpecBusinessViewMetadataService.GetBusinessViewInfoCore(
                "V0101A",
                (NID nid, out IntPtr ptr) => { ptr = capturedBobPtr; return JDEDB_PASSED; },
                ptr => freed = true,
                null);

            await Assert.That(result is null).IsTrue();
            await Assert.That(freed).IsTrue();
        }
        finally
        {
            Marshal.FreeHGlobal(bobPtr);
        }
    }

    [Test]
    public async Task GetBusinessViewInfoCore_ValidBob_ReturnsInfo()
    {
        // Allocate BOB_HEADER
        int headerSize = Marshal.SizeOf<BOB_HEADER>();
        IntPtr headerPtr = Marshal.AllocHGlobal(headerSize);

        // Allocate a BOB_TABLE
        int tableSize = Marshal.SizeOf<BOB_TABLE>();
        IntPtr tablesPtr = Marshal.AllocHGlobal(tableSize);

        // Allocate BOB
        int bobSize = Marshal.SizeOf<BOB>();
        IntPtr bobPtr = Marshal.AllocHGlobal(bobSize);
        bool freed = false;

        try
        {
            var header = new BOB_HEADER
            {
                szView = new NID("V0101A"),
                szDescription = "Address Book View",
                szSystemCode = "01",
                nTableCount = 1,
                nColumnCount = 0,
                nJoinCount = 0
            };
            Marshal.StructureToPtr(header, headerPtr, false);

            var table = new BOB_TABLE
            {
                idFormatNum = new ID(1),
                idPrimaryIndex = new ID(1),
                nNumInstances = 1,
                szTable = new NID("F0101"),
                nativeAlignmentPadding = IntPtr.Zero
            };
            Marshal.StructureToPtr(table, tablesPtr, false);

            var bob = new BOB
            {
                idVersion = new ID(1),
                lpHeader = headerPtr,
                lpTables = tablesPtr,
                lpDBRefs = IntPtr.Zero,
                lpColumns = IntPtr.Zero,
                lpJoins = IntPtr.Zero
            };
            Marshal.StructureToPtr(bob, bobPtr, false);

            IntPtr capturedBobPtr = bobPtr;
            var result = SpecBusinessViewMetadataService.GetBusinessViewInfoCore(
                "V0101A",
                (NID nid, out IntPtr ptr) => { ptr = capturedBobPtr; return JDEDB_PASSED; },
                ptr => freed = true,
                null);

            await Assert.That(result).IsNotNull();
            await Assert.That(result!.ViewName).IsEqualTo("V0101A");
            await Assert.That(result.Description).IsEqualTo("Address Book View");
            await Assert.That(result.SystemCode).IsEqualTo("01");
            await Assert.That(result.Tables.Count).IsEqualTo(1);
            await Assert.That(result.Tables[0].TableName).IsEqualTo("F0101");
            await Assert.That(freed).IsTrue();
        }
        finally
        {
            Marshal.FreeHGlobal(bobPtr);
            Marshal.FreeHGlobal(tablesPtr);
            Marshal.FreeHGlobal(headerPtr);
        }
    }
}
