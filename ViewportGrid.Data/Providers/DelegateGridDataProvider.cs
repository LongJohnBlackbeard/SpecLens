using System;
using System.Threading;
using System.Threading.Tasks;
using ViewportGrid.Core.Interfaces;
using ViewportGrid.Core.Models;

namespace ViewportGrid.Data.Providers;

public sealed class DelegateGridDataProvider : IGridDataProvider
{
    private readonly Func<int> _rowCountProvider;
    private readonly Func<int> _columnCountProvider;
    private readonly Func<int, int, int, int, CancellationToken, Task<CellBlock>> _fetchBlock;

    public DelegateGridDataProvider(
        Func<int> rowCountProvider,
        Func<int> columnCountProvider,
        Func<int, int, int, int, CancellationToken, Task<CellBlock>> fetchBlock)
    {
        _rowCountProvider = rowCountProvider ?? throw new ArgumentNullException(nameof(rowCountProvider));
        _columnCountProvider = columnCountProvider ?? throw new ArgumentNullException(nameof(columnCountProvider));
        _fetchBlock = fetchBlock ?? throw new ArgumentNullException(nameof(fetchBlock));
    }

    public int TotalRowCount => _rowCountProvider();
    public int TotalColumnCount => _columnCountProvider();

    public Task<CellBlock> FetchBlockAsync(
        int startRow,
        int rowCount,
        int startColumn,
        int columnCount,
        CancellationToken ct = default)
    {
        return _fetchBlock(startRow, rowCount, startColumn, columnCount, ct);
    }
}
