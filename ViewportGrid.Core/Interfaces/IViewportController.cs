using System;
using System.Collections.Generic;
using ViewportGrid.Core.Models;

namespace ViewportGrid.Core.Interfaces;

public interface IViewportController
{
    ViewportState CurrentState { get; }

    void SetColumns(IReadOnlyList<ColumnMetadata> columns);
    void SetFrozenColumnCount(int count);
    void SetRowHeight(double height);
    void SetTotalRowCount(int count);

    void ScrollTo(int row, int column);
    void ScrollDelta(double deltaX, double deltaY);
    void Resize(double width, double height);

    event EventHandler<ViewportState>? ViewportChanged;
}
