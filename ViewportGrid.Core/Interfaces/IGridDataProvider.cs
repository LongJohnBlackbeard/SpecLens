using System.Threading;
using System.Threading.Tasks;
using ViewportGrid.Core.Models;

namespace ViewportGrid.Core.Interfaces;

public interface IGridDataProvider
{
    int TotalRowCount { get; }
    int TotalColumnCount { get; }

    Task<CellBlock> FetchBlockAsync(
        int startRow,
        int rowCount,
        int startColumn,
        int columnCount,
        CancellationToken ct = default);
}
