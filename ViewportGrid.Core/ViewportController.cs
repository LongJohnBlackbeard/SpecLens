using System;
using System.Collections.Generic;
using System.Linq;
using ViewportGrid.Core.Interfaces;
using ViewportGrid.Core.Models;

namespace ViewportGrid.Core;

public sealed class ViewportController : IViewportController
{
    private readonly List<ColumnMetadata> _columns = new();
    private double _rowHeight = 24;
    private int _totalRowCount;
    private int _frozenColumnCount;
    private double _horizontalOffset;
    private double _verticalOffset;
    private double _viewportWidth;
    private double _viewportHeight;

    public ViewportController()
    {
        CurrentState = new ViewportState
        {
            FirstVisibleRow = 0,
            VisibleRowCount = 0,
            FirstVisibleColumn = 0,
            VisibleColumnCount = 0,
            FrozenColumnCount = 0,
            HorizontalOffset = 0,
            VerticalOffset = 0,
            ViewportWidth = 0,
            ViewportHeight = 0
        };
    }

    public ViewportState CurrentState { get; private set; }

    public event EventHandler<ViewportState>? ViewportChanged;

    public void SetColumns(IReadOnlyList<ColumnMetadata> columns)
    {
        _columns.Clear();
        _columns.AddRange(columns.OrderBy(c => c.DisplayIndex));
        _frozenColumnCount = Math.Clamp(_frozenColumnCount, 0, _columns.Count);
        ClampOffsets();
        UpdateState();
    }

    public void SetFrozenColumnCount(int count)
    {
        _frozenColumnCount = Math.Clamp(count, 0, _columns.Count);
        ClampOffsets();
        UpdateState();
    }

    public void SetRowHeight(double height)
    {
        if (height <= 0)
        {
            return;
        }

        _rowHeight = height;
        ClampOffsets();
        UpdateState();
    }

    public void SetTotalRowCount(int count)
    {
        _totalRowCount = Math.Max(0, count);
        ClampOffsets();
        UpdateState();
    }

    public void ScrollTo(int row, int column)
    {
        _verticalOffset = Math.Max(0, row) * _rowHeight;
        _horizontalOffset = GetColumnStart(column);
        ClampOffsets();
        UpdateState();
    }

    public void ScrollDelta(double deltaX, double deltaY)
    {
        _horizontalOffset += deltaX;
        _verticalOffset += deltaY;
        ClampOffsets();
        UpdateState();
    }

    public void Resize(double width, double height)
    {
        _viewportWidth = Math.Max(0, width);
        _viewportHeight = Math.Max(0, height);
        ClampOffsets();
        UpdateState();
    }

    private void ClampOffsets()
    {
        double maxVerticalOffset = Math.Max(0, _totalRowCount * _rowHeight - _viewportHeight);
        _verticalOffset = Math.Clamp(_verticalOffset, 0, maxVerticalOffset);

        double maxHorizontalOffset = Math.Max(0, GetScrollableWidth() - GetScrollableViewportWidth());
        _horizontalOffset = Math.Clamp(_horizontalOffset, 0, maxHorizontalOffset);
    }

    private void UpdateState()
    {
        var rowState = CalculateRows();
        var columnState = CalculateColumns();

        var next = new ViewportState
        {
            FirstVisibleRow = rowState.FirstRow,
            VisibleRowCount = rowState.VisibleCount,
            FirstVisibleColumn = columnState.FirstColumn,
            VisibleColumnCount = columnState.VisibleCount,
            FrozenColumnCount = _frozenColumnCount,
            HorizontalOffset = _horizontalOffset,
            VerticalOffset = _verticalOffset,
            ViewportWidth = _viewportWidth,
            ViewportHeight = _viewportHeight
        };

        if (!Equals(next, CurrentState))
        {
            CurrentState = next;
            ViewportChanged?.Invoke(this, next);
        }
    }

    private (int FirstRow, int VisibleCount) CalculateRows()
    {
        if (_totalRowCount == 0 || _rowHeight <= 0 || _viewportHeight <= 0)
        {
            return (0, 0);
        }

        int first = (int)Math.Floor(_verticalOffset / _rowHeight);
        if (first < 0)
        {
            first = 0;
        }
        if (first >= _totalRowCount)
        {
            return (_totalRowCount - 1, 1);
        }

        int visible = (int)Math.Ceiling(_viewportHeight / _rowHeight) + 1;
        visible = Math.Max(1, visible);
        visible = Math.Min(_totalRowCount - first, visible);
        return (first, visible);
    }

    private (int FirstColumn, int VisibleCount) CalculateColumns()
    {
        if (_columns.Count == 0 || _viewportWidth <= 0)
        {
            return (_frozenColumnCount, 0);
        }

        int frozenCount = Math.Clamp(_frozenColumnCount, 0, _columns.Count);
        double frozenWidth = GetFrozenWidth();
        double scrollViewportWidth = Math.Max(0, _viewportWidth - frozenWidth);
        if (scrollViewportWidth <= 0 || frozenCount >= _columns.Count)
        {
            return (frozenCount, 0);
        }

        double scrollOffset = _horizontalOffset;
        double current = 0;
        int first = frozenCount;

        for (int i = frozenCount; i < _columns.Count; i++)
        {
            double next = current + _columns[i].Width;
            if (next > scrollOffset)
            {
                first = i;
                break;
            }

            current = next;
        }

        double visibleWidth = 0;
        int visibleCount = 0;
        for (int i = first; i < _columns.Count; i++)
        {
            visibleWidth += _columns[i].Width;
            visibleCount++;
            if (visibleWidth >= scrollViewportWidth)
            {
                break;
            }
        }

        return (first, visibleCount);
    }

    private double GetFrozenWidth()
    {
        int frozenCount = Math.Clamp(_frozenColumnCount, 0, _columns.Count);
        double width = 0;
        for (int i = 0; i < frozenCount; i++)
        {
            width += _columns[i].Width;
        }

        return width;
    }

    private double GetScrollableWidth()
    {
        int frozenCount = Math.Clamp(_frozenColumnCount, 0, _columns.Count);
        double width = 0;
        for (int i = frozenCount; i < _columns.Count; i++)
        {
            width += _columns[i].Width;
        }

        return width;
    }

    private double GetScrollableViewportWidth()
    {
        return Math.Max(0, _viewportWidth - GetFrozenWidth());
    }

    private double GetColumnStart(int columnIndex)
    {
        if (_columns.Count == 0)
        {
            return 0;
        }

        int frozenCount = Math.Clamp(_frozenColumnCount, 0, _columns.Count);
        if (columnIndex <= frozenCount)
        {
            return 0;
        }

        double offset = 0;
        for (int i = frozenCount; i < _columns.Count && i < columnIndex; i++)
        {
            offset += _columns[i].Width;
        }

        return offset;
    }
}
