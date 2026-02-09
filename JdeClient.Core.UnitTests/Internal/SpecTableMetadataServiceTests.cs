using System.Runtime.InteropServices;
using JdeClient.Core;
using JdeClient.Core.Internal;
using JdeClient.Core.Interop;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.UnitTests.Internal;

public class SpecTableMetadataServiceTests
{
    [Test]
    public async Task CreateColumn_ReturnsNull_WhenNamesMissing()
    {
        var column = SpecTableMetadataService.CreateColumn(string.Empty, string.Empty, 0, 0, 0);
        await Assert.That(column is null).IsTrue();
    }

    [Test]
    public async Task CreateColumn_PrefersDictionaryItem()
    {
        var column = SpecTableMetadataService.CreateColumn("SQL", "DICT", 7, 10, 2);

        await Assert.That(column is not null).IsTrue();
        await Assert.That(column!.Name).IsEqualTo("DICT");
        await Assert.That(column.DataDictionaryItem).IsEqualTo("DICT");
        await Assert.That(column.SqlName).IsEqualTo("SQL");
        await Assert.That(column.DataType).IsEqualTo(7);
        await Assert.That(column.Length).IsEqualTo(10);
        await Assert.That(column.Decimals).IsEqualTo(2);
    }

    [Test]
    public async Task CreateColumn_UsesSqlName_WhenDictionaryMissing()
    {
        var column = SpecTableMetadataService.CreateColumn("SQL", string.Empty, 5, 8, 0);

        await Assert.That(column is not null).IsTrue();
        await Assert.That(column!.Name).IsEqualTo("SQL");
    }

    [Test]
    public async Task TryGetPrimaryIndexFromIndexes_ReturnsPrimaryKeys()
    {
        var indexes = new List<GLOBALINDEX>
        {
            CreateIndex(id: 5, numCols: 2, isPrimary: true, name: "PRIMARY", keys: new[] { "AN8", "KCOO" })
        };

        var success = SpecTableMetadataService.TryGetPrimaryIndexFromIndexes(indexes, out int indexId, out var keys);

        await Assert.That(success).IsTrue();
        await Assert.That(indexId).IsEqualTo(5);
        await Assert.That(keys.Count).IsEqualTo(2);
        await Assert.That(keys[0]).IsEqualTo("AN8");
        await Assert.That(keys[1]).IsEqualTo("KCOO");
    }

    [Test]
    public async Task TryGetPrimaryIndexFromIndexes_ReturnsFalse_WhenNoPrimary()
    {
        var indexes = new List<GLOBALINDEX>
        {
            CreateIndex(id: 2, numCols: 1, isPrimary: false, name: "IDX", keys: new[] { "AN8" })
        };

        var success = SpecTableMetadataService.TryGetPrimaryIndexFromIndexes(indexes, out _, out var keys);

        await Assert.That(success).IsFalse();
        await Assert.That(keys.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TryGetPrimaryIndexFromIndexes_ReturnsFalse_WhenPrimaryMissingDetails()
    {
        var index = CreateIndex(id: 3, numCols: 1, isPrimary: true, name: "IDX", keys: Array.Empty<string>());
        index.lpGlobalIndexDetail = Array.Empty<GLOBALINDEXDETAIL>();

        var success = SpecTableMetadataService.TryGetPrimaryIndexFromIndexes(new List<GLOBALINDEX> { index }, out _, out var keys);

        await Assert.That(success).IsFalse();
        await Assert.That(keys.Count).IsEqualTo(0);
    }

    [Test]
    public async Task BuildIndexInfos_UsesFallbackName_WhenMissing()
    {
        var indexes = new List<GLOBALINDEX>
        {
            CreateIndex(id: 9, numCols: 1, isPrimary: false, name: string.Empty, keys: new[] { "AN8" })
        };

        var results = SpecTableMetadataService.BuildIndexInfos(indexes);

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Name).IsEqualTo("Index 9");
        await Assert.That(results[0].IsPrimary).IsFalse();
        await Assert.That(results[0].KeyColumns.Count).IsEqualTo(1);
    }

    [Test]
    public async Task BuildIndexInfos_SkipsIndexesWithoutKeys()
    {
        var index = CreateIndex(id: 4, numCols: 1, isPrimary: false, name: "IDX", keys: Array.Empty<string>());
        index.lpGlobalIndexDetail = Array.Empty<GLOBALINDEXDETAIL>();

        var results = SpecTableMetadataService.BuildIndexInfos(new List<GLOBALINDEX> { index });

        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetColumns_EmptyName_ReturnsEmpty()
    {
        var service = new SpecTableMetadataService(new HUSER(), new JdeClientOptions());
        var columns = service.GetColumns(" ");
        await Assert.That(columns.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetIndexes_EmptyName_ReturnsEmpty()
    {
        var service = new SpecTableMetadataService(new HUSER(), new JdeClientOptions());
        var indexes = service.GetIndexes(string.Empty);
        await Assert.That(indexes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TryGetPrimaryIndex_EmptyName_ReturnsFalse()
    {
        var service = new SpecTableMetadataService(new HUSER(), new JdeClientOptions());
        var success = service.TryGetPrimaryIndex(string.Empty, out _, out _);
        await Assert.That(success).IsFalse();
    }

    [Test]
    public async Task Normalize_Whitespace_ReturnsEmpty()
    {
        await Assert.That(SpecTableMetadataService.Normalize("   ")).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Normalize_Null_ReturnsEmpty()
    {
        await Assert.That(SpecTableMetadataService.Normalize(null)).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task Normalize_NullTerminators_Trimmed()
    {
        await Assert.That(SpecTableMetadataService.Normalize("F0101\0\0")).IsEqualTo("F0101");
    }

    [Test]
    public async Task Normalize_TrailingSpaces_Trimmed()
    {
        await Assert.That(SpecTableMetadataService.Normalize("F0101   ")).IsEqualTo("F0101");
    }

    [Test]
    public async Task IsHeaderValid_PackedHeader_ValidHeader()
    {
        var header = new TABLECACHE_HEADER
        {
            nNumCols = 5,
            lpColumns = new IntPtr(1)
        };
        await Assert.That(SpecTableMetadataService.IsHeaderValid(header)).IsTrue();
    }

    [Test]
    public async Task IsHeaderValid_PackedHeader_ZeroColumns_ReturnsFalse()
    {
        var header = new TABLECACHE_HEADER
        {
            nNumCols = 0,
            lpColumns = new IntPtr(1)
        };
        await Assert.That(SpecTableMetadataService.IsHeaderValid(header)).IsFalse();
    }

    [Test]
    public async Task IsHeaderValid_PackedHeader_NullColumns_ReturnsFalse()
    {
        var header = new TABLECACHE_HEADER
        {
            nNumCols = 5,
            lpColumns = IntPtr.Zero
        };
        await Assert.That(SpecTableMetadataService.IsHeaderValid(header)).IsFalse();
    }

    [Test]
    public async Task IsHeaderValid_NativeHeader_ValidHeader()
    {
        var header = new TABLECACHE_HEADER_NATIVE
        {
            nNumCols = 3,
            lpColumns = new IntPtr(1)
        };
        await Assert.That(SpecTableMetadataService.IsHeaderValid(header)).IsTrue();
    }

    [Test]
    public async Task IsHeaderValid_NativeHeader_ZeroColumns_ReturnsFalse()
    {
        var header = new TABLECACHE_HEADER_NATIVE
        {
            nNumCols = 0,
            lpColumns = new IntPtr(1)
        };
        await Assert.That(SpecTableMetadataService.IsHeaderValid(header)).IsFalse();
    }

    [Test]
    public async Task ReadColumnCache_PackedLayout()
    {
        int cacheSize = Marshal.SizeOf<COLUMNCACHE_HEADER>();
        IntPtr cachePtr = Marshal.AllocHGlobal(cacheSize);
        try
        {
            var cache = new COLUMNCACHE_HEADER
            {
                szDict = new NID("AN8"),
                idEverestType = 9,
                nLength = 8,
                nDecimals = 0,
                nDispDecimals = 0
            };
            Marshal.StructureToPtr(cache, cachePtr, false);

            var result = SpecTableMetadataService.ReadColumnCache(cachePtr, useNativeLayout: false);
            await Assert.That(result.DictItem).IsEqualTo("AN8");
            await Assert.That(result.EvdType).IsEqualTo(9);
            await Assert.That(result.Length).IsEqualTo(8);
            await Assert.That(result.Decimals).IsEqualTo(0);
        }
        finally
        {
            Marshal.FreeHGlobal(cachePtr);
        }
    }

    [Test]
    public async Task ReadColumnCache_NativeLayout()
    {
        int cacheSize = Marshal.SizeOf<COLUMNCACHE_HEADER_NATIVE>();
        IntPtr cachePtr = Marshal.AllocHGlobal(cacheSize);
        try
        {
            var cache = new COLUMNCACHE_HEADER_NATIVE
            {
                szDict = new NID("MCU"),
                idEverestType = 2,
                nLength = 12,
                nDecimals = 0,
                nDispDecimals = 0
            };
            Marshal.StructureToPtr(cache, cachePtr, false);

            var result = SpecTableMetadataService.ReadColumnCache(cachePtr, useNativeLayout: true);
            await Assert.That(result.DictItem).IsEqualTo("MCU");
            await Assert.That(result.EvdType).IsEqualTo(2);
            await Assert.That(result.Length).IsEqualTo(12);
        }
        finally
        {
            Marshal.FreeHGlobal(cachePtr);
        }
    }

    [Test]
    public async Task BuildIndexInfos_NamedIndex_UsesActualName()
    {
        var indexes = new List<GLOBALINDEX>
        {
            CreateIndex(id: 1, numCols: 2, isPrimary: true, name: "F0101_PK", keys: new[] { "AN8", "KCOO" })
        };

        var results = SpecTableMetadataService.BuildIndexInfos(indexes);

        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Name).IsEqualTo("F0101_PK");
        await Assert.That(results[0].IsPrimary).IsTrue();
        await Assert.That(results[0].KeyColumns.Count).IsEqualTo(2);
    }

    [Test]
    public async Task TryGetPrimaryIndexFromIndexes_MultipleIndexes_FindsPrimary()
    {
        var indexes = new List<GLOBALINDEX>
        {
            CreateIndex(id: 1, numCols: 1, isPrimary: false, name: "IDX1", keys: new[] { "KCOO" }),
            CreateIndex(id: 2, numCols: 2, isPrimary: true, name: "PRIMARY", keys: new[] { "AN8", "KCOO" }),
            CreateIndex(id: 3, numCols: 1, isPrimary: false, name: "IDX3", keys: new[] { "MCU" })
        };

        var success = SpecTableMetadataService.TryGetPrimaryIndexFromIndexes(indexes, out int indexId, out var keys);

        await Assert.That(success).IsTrue();
        await Assert.That(indexId).IsEqualTo(2);
        await Assert.That(keys.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Dispose_DoubleSafe()
    {
        var service = new SpecTableMetadataService(new HUSER(), new JdeClientOptions());
        service.Dispose();
        service.Dispose(); // Should not throw
        await Assert.That(true).IsTrue();
    }

    [Test]
    public async Task TryReadHeader_PackedLayout_ValidHeader()
    {
        int headerSize = Marshal.SizeOf<TABLECACHE_HEADER>();
        IntPtr tablePtr = Marshal.AllocHGlobal(headerSize);
        try
        {
            var packed = new TABLECACHE_HEADER
            {
                szTable = new NID("F0101"),
                nNumCols = 5,
                nNumIndex = 1,
                lpColumns = new IntPtr(0x1000),
                lpGlobalIndex = new IntPtr(0x2000)
            };
            Marshal.StructureToPtr(packed, tablePtr, false);

            var service = new SpecTableMetadataService(new HUSER(), new JdeClientOptions());
            var success = service.TryReadHeader(tablePtr, "F0101", out var header);

            await Assert.That(success).IsTrue();
            await Assert.That(header.NumCols).IsEqualTo((ushort)5);
            await Assert.That(header.NumIndex).IsEqualTo((ushort)1);
        }
        finally
        {
            Marshal.FreeHGlobal(tablePtr);
        }
    }

    [Test]
    public async Task TryReadHeader_BothInvalid_ReturnsFalse()
    {
        // Allocate enough memory for the larger of the two header types
        int packedSize = Marshal.SizeOf<TABLECACHE_HEADER>();
        int nativeSize = Marshal.SizeOf<TABLECACHE_HEADER_NATIVE>();
        int size = Math.Max(packedSize, nativeSize);
        IntPtr tablePtr = Marshal.AllocHGlobal(size);
        try
        {
            // Zero out memory - both layouts will have nNumCols=0
            var zero = new byte[size];
            Marshal.Copy(zero, 0, tablePtr, size);

            var service = new SpecTableMetadataService(new HUSER(), new JdeClientOptions());
            var success = service.TryReadHeader(tablePtr, "F0101", out _);

            await Assert.That(success).IsFalse();
        }
        finally
        {
            Marshal.FreeHGlobal(tablePtr);
        }
    }

    private static GLOBALINDEX CreateIndex(int id, ushort numCols, bool isPrimary, string? name, string[] keys)
    {
        var details = new GLOBALINDEXDETAIL[20];
        for (int i = 0; i < keys.Length && i < details.Length; i++)
        {
            details[i] = new GLOBALINDEXDETAIL
            {
                szDict = new NID(keys[i]),
                cSort = 0
            };
        }

        return new GLOBALINDEX
        {
            idIndex = new ID(id),
            nPrimary = (ushort)(isPrimary ? 1 : 0),
            nUnique = 0,
            szIndexName = name ?? string.Empty,
            nNumCols = numCols,
            lpGlobalIndexDetail = details
        };
    }
}
