using ViewportGrid.Core.Models;

namespace SpecLens.Avalonia.Models;

public sealed class ViewportHeaderColumn
{
    public ViewportHeaderColumn(ColumnMetadata column, ColumnFilter filter)
    {
        Column = column;
        Filter = filter;
    }

    public ColumnMetadata Column { get; }
    public ColumnFilter Filter { get; }
    public double Width => Column.Width;
    public string Name => Column.Id;
}
