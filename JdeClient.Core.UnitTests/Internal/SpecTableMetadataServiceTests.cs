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
