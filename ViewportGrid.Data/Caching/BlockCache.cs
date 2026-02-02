using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ViewportGrid.Core.Interfaces;
using ViewportGrid.Core.Models;

namespace ViewportGrid.Data.Caching;

public sealed class BlockCache
{
    private readonly IGridDataProvider _dataProvider;
    private readonly Dictionary<BlockKey, CacheEntry> _cache = new();
    private readonly LinkedList<BlockKey> _lru = new();
    private readonly object _sync = new();
    private readonly int _rowBlockSize;
    private readonly int _columnBlockSize;
    private readonly int _maxBlocks;

    public BlockCache(IGridDataProvider dataProvider, int rowBlockSize = 100, int columnBlockSize = 20, int maxBlocks = 50)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        if (rowBlockSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowBlockSize));
        }
        if (columnBlockSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnBlockSize));
        }
        if (maxBlocks <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBlocks));
        }

        _rowBlockSize = rowBlockSize;
        _columnBlockSize = columnBlockSize;
        _maxBlocks = maxBlocks;
    }

    public int RowBlockSize => _rowBlockSize;
    public int ColumnBlockSize => _columnBlockSize;
    public int MaxBlocks => _maxBlocks;

    public void Invalidate()
    {
        lock (_sync)
        {
            _cache.Clear();
            _lru.Clear();
        }
    }

    public bool TryGetBlock(BlockKey key, out CellBlock? block)
    {
        lock (_sync)
        {
            if (_cache.TryGetValue(key, out var entry) && entry.FetchTask.IsCompletedSuccessfully)
            {
                block = entry.FetchTask.Result;
                Touch(entry);
                return true;
            }
        }

        block = null;
        return false;
    }

    public Task<CellBlock> GetBlockAsync(BlockKey key, CancellationToken ct = default)
    {
        Task<CellBlock> task;
        lock (_sync)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                Touch(entry);
                task = entry.FetchTask;
            }
            else
            {
                task = FetchBlockInternalAsync(key);
                var node = _lru.AddFirst(key);
                entry = new CacheEntry(task, node);
                _cache[key] = entry;
                TrimCacheLocked();
            }
        }

        return ct.CanBeCanceled ? task.WaitAsync(ct) : task;
    }

    public async Task<IReadOnlyList<CellBlock>> GetBlocksAsync(
        int startRow,
        int rowCount,
        int startColumn,
        int columnCount,
        CancellationToken ct = default)
    {
        if (rowCount <= 0 || columnCount <= 0)
        {
            return Array.Empty<CellBlock>();
        }

        int rowStartBlock = startRow / _rowBlockSize;
        int rowEndBlock = (startRow + rowCount - 1) / _rowBlockSize;
        int columnStartBlock = startColumn / _columnBlockSize;
        int columnEndBlock = (startColumn + columnCount - 1) / _columnBlockSize;

        var tasks = new List<Task<CellBlock>>();
        for (int rowBlock = rowStartBlock; rowBlock <= rowEndBlock; rowBlock++)
        {
            for (int columnBlock = columnStartBlock; columnBlock <= columnEndBlock; columnBlock++)
            {
                tasks.Add(GetBlockAsync(new BlockKey(rowBlock, columnBlock), ct));
            }
        }

        var blocks = await Task.WhenAll(tasks).ConfigureAwait(false);
        return blocks;
    }

    private async Task<CellBlock> FetchBlockInternalAsync(BlockKey key)
    {
        try
        {
            int startRow = key.RowBlock * _rowBlockSize;
            int startColumn = key.ColumnBlock * _columnBlockSize;
            int rowCount = Math.Min(_rowBlockSize, Math.Max(0, _dataProvider.TotalRowCount - startRow));
            int columnCount = Math.Min(_columnBlockSize, Math.Max(0, _dataProvider.TotalColumnCount - startColumn));

            if (rowCount == 0 || columnCount == 0)
            {
                return new CellBlock
                {
                    StartRow = startRow,
                    StartColumn = startColumn,
                    RowCount = 0,
                    ColumnCount = 0,
                    Data = new object?[0, 0]
                };
            }

            return await _dataProvider.FetchBlockAsync(startRow, rowCount, startColumn, columnCount)
                .ConfigureAwait(false);
        }
        catch
        {
            Remove(key);
            throw;
        }
    }

    private void Touch(CacheEntry entry)
    {
        _lru.Remove(entry.Node);
        _lru.AddFirst(entry.Node);
    }

    private void TrimCacheLocked()
    {
        while (_cache.Count > _maxBlocks && _lru.Last != null)
        {
            var last = _lru.Last;
            if (last == null)
            {
                return;
            }

            _lru.RemoveLast();
            _cache.Remove(last.Value);
        }
    }

    private void Remove(BlockKey key)
    {
        lock (_sync)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                _lru.Remove(entry.Node);
                _cache.Remove(key);
            }
        }
    }

    private sealed class CacheEntry
    {
        public CacheEntry(Task<CellBlock> fetchTask, LinkedListNode<BlockKey> node)
        {
            FetchTask = fetchTask;
            Node = node;
        }

        public Task<CellBlock> FetchTask { get; }
        public LinkedListNode<BlockKey> Node { get; }
    }
}
