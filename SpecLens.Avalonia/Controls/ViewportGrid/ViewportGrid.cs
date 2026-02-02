using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ViewportGrid.Core;
using ViewportGrid.Core.Interfaces;
using ViewportGrid.Core.Models;
using ViewportGrid.Data.Caching;

namespace SpecLens.Avalonia.Controls;

public sealed class ViewportGrid : Control, ILogicalScrollable, ICustomHitTest
{
    public static readonly StyledProperty<IGridDataProvider?> DataProviderProperty =
        AvaloniaProperty.Register<ViewportGrid, IGridDataProvider?>(nameof(DataProvider));

    public static readonly StyledProperty<IReadOnlyList<ColumnMetadata>?> ColumnsProperty =
        AvaloniaProperty.Register<ViewportGrid, IReadOnlyList<ColumnMetadata>?>(nameof(Columns));

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<ViewportGrid, double>(nameof(RowHeight), 24);

    public static readonly StyledProperty<int> FrozenColumnCountProperty =
        AvaloniaProperty.Register<ViewportGrid, int>(nameof(FrozenColumnCount), 0);

    public static readonly StyledProperty<int> TotalRowCountProperty =
        AvaloniaProperty.Register<ViewportGrid, int>(nameof(TotalRowCount), -1);

    public static readonly StyledProperty<IBrush?> GridLineBrushProperty =
        AvaloniaProperty.Register<ViewportGrid, IBrush?>(nameof(GridLineBrush), Brushes.LightGray);

    public static readonly StyledProperty<double> GridLineThicknessProperty =
        AvaloniaProperty.Register<ViewportGrid, double>(nameof(GridLineThickness), 1);

    public static readonly StyledProperty<IBrush?> FrozenColumnBrushProperty =
        AvaloniaProperty.Register<ViewportGrid, IBrush?>(nameof(FrozenColumnBrush));

    public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
        AvaloniaProperty.Register<ViewportGrid, IBrush?>(nameof(SelectionBrush), Brushes.LightBlue);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<ViewportGrid, IBrush?>(nameof(Foreground), Brushes.Black);

    public static readonly StyledProperty<FontFamily?> FontFamilyProperty =
        AvaloniaProperty.Register<ViewportGrid, FontFamily?>(nameof(FontFamily), new FontFamily("Segoe UI"));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<ViewportGrid, double>(nameof(FontSize), 12);

    public static readonly StyledProperty<FontStyle> FontStyleProperty =
        AvaloniaProperty.Register<ViewportGrid, FontStyle>(nameof(FontStyle), FontStyle.Normal);

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        AvaloniaProperty.Register<ViewportGrid, FontWeight>(nameof(FontWeight), FontWeight.Normal);

    private readonly ViewportController _controller = new();
    private BlockCache? _cache;
    private IReadOnlyList<ColumnMetadata> _orderedColumns = Array.Empty<ColumnMetadata>();
    private CancellationTokenSource? _fetchCts;
    private Vector _offset;
    private Size _extent;
    private Size _viewport;
    private Size _scrollSize = new(1, 1);
    private Size _pageScrollSize = new(1, 1);
    private bool _isSynchronizing;
    private bool _allowHorizontalScroll = true;
    private bool _allowVerticalScroll = true;
    private bool _isSelecting;
    private TopLevel? _topLevel;
    private int _selectionStartRow = -1;
    private int _selectionStartColumn = -1;
    private int _selectionEndRow = -1;
    private int _selectionEndColumn = -1;

    public ViewportGrid()
    {
        ClipToBounds = true;
        Focusable = true;
        _controller.ViewportChanged += OnViewportChanged;
    }

    public IGridDataProvider? DataProvider
    {
        get => GetValue(DataProviderProperty);
        set => SetValue(DataProviderProperty, value);
    }

    public IReadOnlyList<ColumnMetadata>? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public int FrozenColumnCount
    {
        get => GetValue(FrozenColumnCountProperty);
        set => SetValue(FrozenColumnCountProperty, value);
    }

    public int TotalRowCount
    {
        get => GetValue(TotalRowCountProperty);
        set => SetValue(TotalRowCountProperty, value);
    }

    public IBrush? GridLineBrush
    {
        get => GetValue(GridLineBrushProperty);
        set => SetValue(GridLineBrushProperty, value);
    }

    public double GridLineThickness
    {
        get => GetValue(GridLineThicknessProperty);
        set => SetValue(GridLineThicknessProperty, value);
    }

    public IBrush? FrozenColumnBrush
    {
        get => GetValue(FrozenColumnBrushProperty);
        set => SetValue(FrozenColumnBrushProperty, value);
    }

    public IBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public FontFamily? FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontStyle FontStyle
    {
        get => GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public event EventHandler? ScrollInvalidated;
    public event EventHandler<ViewportGridSelectionChangedEventArgs>? SelectionChanged;

    public Size Extent => _extent;
    public Size Viewport => _viewport;

    public Vector Offset
    {
        get => _offset;
        set => SetOffset(value);
    }

    public Size ScrollSize => _scrollSize;
    public Size PageScrollSize => _pageScrollSize;

    public bool CanHorizontallyScroll
    {
        get => _allowHorizontalScroll && _extent.Width > _viewport.Width + 0.5;
        set
        {
            if (_allowHorizontalScroll == value)
            {
                return;
            }

            _allowHorizontalScroll = value;
            NotifyScrollInvalidated();
        }
    }

    public bool CanVerticallyScroll
    {
        get => _allowVerticalScroll && _extent.Height > _viewport.Height + 0.5;
        set
        {
            if (_allowVerticalScroll == value)
            {
                return;
            }

            _allowVerticalScroll = value;
            NotifyScrollInvalidated();
        }
    }

    public bool IsLogicalScrollEnabled => true;

    bool ICustomHitTest.HitTest(Point point)
    {
        var root = this.GetVisualRoot() as Visual;
        if (root == null)
        {
            return false;
        }

        var localPoint = root.TranslatePoint(point, this);
        if (localPoint == null)
        {
            return false;
        }

        return new Rect(Bounds.Size).Contains(localPoint.Value);
    }

    public void RefreshMetrics()
    {
        UpdateMetrics();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachRootHandlers();
        UpdateMetrics();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachRootHandlers();
        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _fetchCts = null;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateViewportSize(finalSize);
        return finalSize;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? Bounds.Width : availableSize.Width;
        double height = double.IsInfinity(availableSize.Height) ? Bounds.Height : availableSize.Height;
        if (double.IsInfinity(width) || width <= 0)
        {
            width = 1;
        }
        if (double.IsInfinity(height) || height <= 0)
        {
            height = Math.Max(1, RowHeight);
        }
        return new Size(width, height);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateViewportSize(e.NewSize);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (e.Handled)
        {
            return;
        }

        var delta = e.Delta;
        if (delta.X == 0 && delta.Y == 0)
        {
            return;
        }

        double stepY = Math.Max(1, RowHeight);
        double stepX = GetHorizontalScrollStepWidth();
        double deltaX = 0;
        double deltaY = 0;

        if (Math.Abs(delta.X) > 0.01)
        {
            deltaX = -delta.X * stepX;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            deltaX = -delta.Y * stepX;
        }
        else
        {
            deltaY = -delta.Y * stepY;
        }

        if (deltaX == 0 && deltaY == 0)
        {
            return;
        }

        Offset = new Vector(_offset.X + deltaX, _offset.Y + deltaY);
        e.Handled = true;
    }

    private void AttachRootHandlers()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (ReferenceEquals(_topLevel, topLevel))
        {
            return;
        }

        DetachRootHandlers();
        _topLevel = topLevel;
        if (_topLevel == null)
        {
            return;
        }

        _topLevel.AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, true);
        _topLevel.AddHandler(PointerMovedEvent, OnRootPointerMoved, RoutingStrategies.Tunnel, true);
        _topLevel.AddHandler(PointerReleasedEvent, OnRootPointerReleased, RoutingStrategies.Tunnel, true);
        _topLevel.AddHandler(InputElement.PointerCaptureLostEvent, OnRootPointerCaptureLost, RoutingStrategies.Tunnel, true);
    }

    private void DetachRootHandlers()
    {
        if (_topLevel == null)
        {
            return;
        }

        _topLevel.RemoveHandler(PointerPressedEvent, OnRootPointerPressed);
        _topLevel.RemoveHandler(PointerMovedEvent, OnRootPointerMoved);
        _topLevel.RemoveHandler(PointerReleasedEvent, OnRootPointerReleased);
        _topLevel.RemoveHandler(InputElement.PointerCaptureLostEvent, OnRootPointerCaptureLost);
        _topLevel = null;
    }

    private bool TryGetLocalPoint(PointerEventArgs e, out Point point)
    {
        point = e.GetCurrentPoint(this).Position;
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return false;
        }

        return point.X >= 0 && point.Y >= 0 && point.X <= Bounds.Width && point.Y <= Bounds.Height;
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ShouldHandlePointerEvent())
        {
            return;
        }

        if (IsOverScrollBar(e))
        {
            return;
        }

        if (!TryGetLocalPoint(e, out var localPoint))
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (!TryGetCellFromPoint(localPoint, out int row, out int column))
        {
            ClearSelection();
            return;
        }

        _selectionStartRow = row;
        _selectionStartColumn = column;
        _selectionEndRow = row;
        _selectionEndColumn = column;
        _isSelecting = true;
        Focus();
        InvalidateVisual();
        RaiseSelectionChanged(row, column);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnRootPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!ShouldHandlePointerEvent())
        {
            return;
        }

        if (!_isSelecting)
        {
            return;
        }

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (!TryGetLocalPoint(e, out var localPoint))
        {
            return;
        }

        if (!TryGetCellFromPoint(localPoint, out int row, out int column))
        {
            return;
        }

        if (row == _selectionEndRow && column == _selectionEndColumn)
        {
            return;
        }

        _selectionEndRow = row;
        _selectionEndColumn = column;
        InvalidateVisual();
        RaiseSelectionChanged(row, column);
        e.Handled = true;
    }

    private void OnRootPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ShouldHandlePointerEvent())
        {
            return;
        }

        if (!_isSelecting)
        {
            return;
        }

        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            _isSelecting = false;
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }
            e.Handled = true;
        }
    }

    private void OnRootPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!ShouldHandlePointerEvent())
        {
            return;
        }

        if (!_isSelecting)
        {
            return;
        }

        _isSelecting = false;
    }

    private bool ShouldHandlePointerEvent()
    {
        if (!IsVisible || !IsEnabled || !IsHitTestVisible)
        {
            return false;
        }

        foreach (var ancestor in this.GetVisualAncestors())
        {
            if (!ancestor.IsVisible)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsOverScrollBar(PointerEventArgs e)
    {
        if (_topLevel == null)
        {
            return false;
        }

        var point = e.GetCurrentPoint(_topLevel).Position;
        foreach (var element in InputExtensions.GetInputElementsAt(_topLevel, point))
        {
            if (element is ScrollBar || element is Thumb)
            {
                return true;
            }

            if (element is Visual visual && visual.FindAncestorOfType<ScrollBar>() != null)
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            CopySelectionToClipboard();
            e.Handled = true;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataProviderProperty)
        {
            ResetCache();
            UpdateMetrics();
        }
        else if (change.Property == ColumnsProperty)
        {
            _cache?.Invalidate();
            UpdateMetrics();
        }
        else if (change.Property == RowHeightProperty)
        {
            UpdateMetrics();
        }
        else if (change.Property == FrozenColumnCountProperty)
        {
            UpdateMetrics();
        }
        else if (change.Property == TotalRowCountProperty)
        {
            UpdateMetrics();
        }
        else if (change.Property == FrozenColumnBrushProperty)
        {
            InvalidateVisual();
        }
        else if (change.Property == SelectionBrushProperty)
        {
            InvalidateVisual();
        }
        else if (change.Property == ForegroundProperty
            || change.Property == FontFamilyProperty
            || change.Property == FontSizeProperty
            || change.Property == FontStyleProperty
            || change.Property == FontWeightProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var columns = _orderedColumns;
        if (columns.Count == 0)
        {
            return;
        }

        var state = _controller.CurrentState;
        if (state.VisibleRowCount <= 0)
        {
            return;
        }

        double rowHeight = RowHeight;
        if (rowHeight <= 0)
        {
            return;
        }

        int frozenCount = Math.Clamp(state.FrozenColumnCount, 0, columns.Count);
        int firstScrollableColumn = state.FirstVisibleColumn;
        int lastScrollableColumn = Math.Min(columns.Count, firstScrollableColumn + state.VisibleColumnCount);

        QueueFetchBlocks(state);

        var foreground = this.Foreground ?? Brushes.Black;
        var frozenBrush = FrozenColumnBrush;
        var selectionBrush = SelectionBrush;
        var fontFamily = this.FontFamily ?? new FontFamily("Segoe UI");
        var fontStyle = this.FontStyle;
        var fontWeight = this.FontWeight;
        double fontSize = this.FontSize;
        var typeface = new Typeface(fontFamily, fontStyle, fontWeight);
        var gridPen = CreateGridPen();

        double frozenWidth = 0;
        for (int i = 0; i < frozenCount; i++)
        {
            frozenWidth += columns[i].Width;
        }

        for (int rowOffset = 0; rowOffset < state.VisibleRowCount; rowOffset++)
        {
            int rowIndex = state.FirstVisibleRow + rowOffset;
            double y = rowOffset * rowHeight;

            double x = 0;
            for (int colIndex = 0; colIndex < frozenCount; colIndex++)
            {
                double width = columns[colIndex].Width;
                bool isSelected = IsCellSelected(rowIndex, colIndex);
                RenderCell(context, rowIndex, colIndex, x, y, width, rowHeight, typeface, fontSize, foreground, frozenBrush, selectionBrush, isSelected);
                x += width;
            }

            double scrollX = frozenWidth;
            for (int colIndex = firstScrollableColumn; colIndex < lastScrollableColumn; colIndex++)
            {
                double width = columns[colIndex].Width;
                bool isSelected = IsCellSelected(rowIndex, colIndex);
                RenderCell(context, rowIndex, colIndex, scrollX, y, width, rowHeight, typeface, fontSize, foreground, null, selectionBrush, isSelected);
                scrollX += width;
            }

            if (gridPen != null)
            {
                context.DrawLine(gridPen, new Point(0, y + rowHeight), new Point(Bounds.Width, y + rowHeight));
            }
        }

        if (gridPen != null)
        {
            double x = 0;
            for (int colIndex = 0; colIndex < frozenCount; colIndex++)
            {
                x += columns[colIndex].Width;
                context.DrawLine(gridPen, new Point(x, 0), new Point(x, Bounds.Height));
            }

            double scrollX = frozenWidth;
            for (int colIndex = firstScrollableColumn; colIndex < lastScrollableColumn; colIndex++)
            {
                scrollX += columns[colIndex].Width;
                context.DrawLine(gridPen, new Point(scrollX, 0), new Point(scrollX, Bounds.Height));
            }
        }
    }

    private void RenderCell(
        DrawingContext context,
        int rowIndex,
        int colIndex,
        double x,
        double y,
        double width,
        double height,
        Typeface typeface,
        double fontSize,
        IBrush foreground,
        IBrush? background,
        IBrush? selectionBrush,
        bool isSelected)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (background != null)
        {
            var rect = new Rect(x, y, width, height);
            context.DrawRectangle(background, null, rect);
        }

        if (isSelected && selectionBrush != null)
        {
            var rect = new Rect(x, y, width, height);
            context.DrawRectangle(selectionBrush, null, rect);
        }

        string text = string.Empty;
        if (TryGetCellValue(rowIndex, colIndex, out var value) && value != null)
        {
            text = value switch
            {
                string s => s,
                _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
            };
        }

        if (text.Length == 0)
        {
            return;
        }

        const double padding = 4;
        double maxWidth = Math.Max(0, width - padding * 2);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            foreground)
        {
            MaxTextWidth = maxWidth,
            MaxTextHeight = height
        };

        double textY = y + Math.Max(0, (height - fontSize) / 2);
        context.DrawText(formatted, new Point(x + padding, textY));
    }

    private bool TryGetCellValue(int rowIndex, int colIndex, out object? value)
    {
        value = null;
        if (_cache == null)
        {
            return false;
        }

        int rowBlock = rowIndex / _cache.RowBlockSize;
        int columnBlock = colIndex / _cache.ColumnBlockSize;
        if (!_cache.TryGetBlock(new BlockKey(rowBlock, columnBlock), out var block) || block == null)
        {
            return false;
        }

        if (rowIndex < block.StartRow || rowIndex >= block.StartRow + block.RowCount)
        {
            return false;
        }

        if (colIndex < block.StartColumn || colIndex >= block.StartColumn + block.ColumnCount)
        {
            return false;
        }

        int rowOffset = rowIndex - block.StartRow;
        int columnOffset = colIndex - block.StartColumn;
        value = block.Data[rowOffset, columnOffset];
        return true;
    }

    private int ResolveTotalRowCount()
    {
        if (TotalRowCount >= 0)
        {
            return TotalRowCount;
        }

        return DataProvider?.TotalRowCount ?? 0;
    }

    private void UpdateOrderedColumns()
    {
        var columns = Columns;
        if (columns == null || columns.Count == 0)
        {
            _orderedColumns = Array.Empty<ColumnMetadata>();
            return;
        }

        var ordered = new List<ColumnMetadata>(columns);
        ordered.Sort(static (left, right) => left.DisplayIndex.CompareTo(right.DisplayIndex));
        for (int i = 0; i < ordered.Count; i++)
        {
            double width = NormalizeWidth(ordered[i].Width);
            if (!width.Equals(ordered[i].Width))
            {
                ordered[i] = ordered[i] with { Width = width };
            }
        }
        _orderedColumns = ordered;
    }

    private Pen? CreateGridPen()
    {
        if (GridLineBrush == null || GridLineThickness <= 0)
        {
            return null;
        }

        return new Pen(GridLineBrush, GridLineThickness);
    }

    private void UpdateMetrics()
    {
        UpdateOrderedColumns();
        var columns = _orderedColumns;
        _controller.SetColumns(columns);
        _controller.SetFrozenColumnCount(Math.Clamp(FrozenColumnCount, 0, columns.Count));
        _controller.SetRowHeight(RowHeight);
        _controller.SetTotalRowCount(ResolveTotalRowCount());
        _controller.Resize(Bounds.Width, Bounds.Height);
        var state = _controller.CurrentState;
        UpdateScrollMetrics(state);
        QueueFetchBlocks(state);
        InvalidateVisual();
    }

    private void UpdateViewportSize(Size size)
    {
        _controller.Resize(size.Width, size.Height);
    }

    private void SetOffset(Vector value)
    {
        var next = CoerceOffset(value);
        if (_offset == next)
        {
            return;
        }

        _offset = next;
        UpdateControllerFromOffset();
        NotifyScrollInvalidated();
    }

    private Vector CoerceOffset(Vector offset)
    {
        double maxX = Math.Max(0, _extent.Width - _viewport.Width);
        double maxY = Math.Max(0, _extent.Height - _viewport.Height);
        var snapped = SnapOffset(offset);
        double x = Math.Clamp(snapped.X, 0, maxX);
        double y = Math.Clamp(snapped.Y, 0, maxY);
        return new Vector(x, y);
    }

    private void UpdateControllerFromOffset()
    {
        if (_isSynchronizing)
        {
            return;
        }

        var state = _controller.CurrentState;
        double deltaX = _offset.X - state.HorizontalOffset;
        double deltaY = _offset.Y - state.VerticalOffset;
        if (Math.Abs(deltaX) < 0.01 && Math.Abs(deltaY) < 0.01)
        {
            return;
        }

        _isSynchronizing = true;
        _controller.ScrollDelta(deltaX, deltaY);
        _isSynchronizing = false;
    }

    private void OnViewportChanged(object? sender, ViewportState state)
    {
        if (_isSynchronizing)
        {
            return;
        }

        _isSynchronizing = true;
        UpdateScrollMetrics(state);
        SyncOffsetFromController(state);
        _isSynchronizing = false;

        QueueFetchBlocks(state);
        InvalidateVisual();
    }

    private void UpdateScrollMetrics(ViewportState state)
    {
        int totalRows = ResolveTotalRowCount();
        double rowHeight = Math.Max(1, RowHeight);
        var columns = _orderedColumns;
        int totalColumns = columns.Count;
        int frozenCount = Math.Clamp(state.FrozenColumnCount, 0, totalColumns);
        double frozenWidth = GetFrozenWidth(columns, frozenCount);
        double scrollableWidth = GetScrollableWidth(columns, frozenCount);
        double viewportWidth = Math.Max(0, state.ViewportWidth - frozenWidth);
        double viewportHeight = Math.Max(0, state.ViewportHeight);

        _extent = new Size(scrollableWidth, totalRows * rowHeight);
        _viewport = new Size(viewportWidth, viewportHeight);
        _scrollSize = new Size(GetHorizontalScrollStepWidth(), rowHeight);
        _pageScrollSize = new Size(Math.Max(1, viewportWidth), Math.Max(1, viewportHeight));

        NotifyScrollInvalidated();
    }

    private void SyncOffsetFromController(ViewportState state)
    {
        var next = CoerceOffset(new Vector(state.HorizontalOffset, state.VerticalOffset));
        if (_offset != next)
        {
            _offset = next;
            NotifyScrollInvalidated();
        }
    }

    private void ResetCache()
    {
        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _fetchCts = null;
        _cache = DataProvider == null ? null : new BlockCache(DataProvider);
    }

    private void QueueFetchBlocks(ViewportState state)
    {
        if (_cache == null || DataProvider == null)
        {
            return;
        }

        if (state.VisibleRowCount <= 0)
        {
            return;
        }

        _fetchCts?.Cancel();
        _fetchCts?.Dispose();
        _fetchCts = new CancellationTokenSource();
        var ct = _fetchCts.Token;

        _ = FetchBlocksAsync(state, ct);
    }

    private async Task FetchBlocksAsync(ViewportState state, CancellationToken ct)
    {
        try
        {
            var tasks = new List<Task<IReadOnlyList<CellBlock>>>();
            if (state.FrozenColumnCount > 0)
            {
                tasks.Add(_cache!.GetBlocksAsync(
                    state.FirstVisibleRow,
                    state.VisibleRowCount,
                    0,
                    state.FrozenColumnCount,
                    ct));
            }

            if (state.VisibleColumnCount > 0)
            {
                tasks.Add(_cache!.GetBlocksAsync(
                    state.FirstVisibleRow,
                    state.VisibleRowCount,
                    state.FirstVisibleColumn,
                    state.VisibleColumnCount,
                    ct));
            }

            if (tasks.Count == 0)
            {
                return;
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
        }
    }

    public void RaiseScrollInvalidated(EventArgs e)
    {
        ScrollInvalidated?.Invoke(this, e);
    }

    public bool BringIntoView(Control target, Rect rect)
    {
        return false;
    }

    public Control? GetControlInDirection(NavigationDirection direction, Control? from)
    {
        return null;
    }

    private void NotifyScrollInvalidated()
    {
        RaiseScrollInvalidated(EventArgs.Empty);
    }

    private bool TryGetCellFromPoint(Point point, out int rowIndex, out int columnIndex)
    {
        rowIndex = -1;
        columnIndex = -1;

        var columns = _orderedColumns;
        if (columns.Count == 0)
        {
            return false;
        }

        int totalRows = ResolveTotalRowCount();
        if (totalRows <= 0)
        {
            return false;
        }

        double rowHeight = RowHeight;
        if (rowHeight <= 0)
        {
            return false;
        }

        double y = point.Y + _offset.Y;
        int row = (int)Math.Floor(y / rowHeight);
        if (row < 0 || row >= totalRows)
        {
            return false;
        }

        int frozenCount = Math.Clamp(FrozenColumnCount, 0, columns.Count);
        double frozenWidth = GetFrozenWidth(columns, frozenCount);
        if (point.X < frozenWidth)
        {
            double x = point.X;
            double current = 0;
            for (int i = 0; i < frozenCount; i++)
            {
                double width = NormalizeWidth(columns[i].Width);
                if (x < current + width)
                {
                    rowIndex = row;
                    columnIndex = i;
                    return true;
                }

                current += width;
            }

            return false;
        }

        double scrollX = point.X - frozenWidth + _offset.X;
        double scrollCurrent = 0;
        for (int i = frozenCount; i < columns.Count; i++)
        {
            double width = NormalizeWidth(columns[i].Width);
            if (scrollX < scrollCurrent + width)
            {
                rowIndex = row;
                columnIndex = i;
                return true;
            }

            scrollCurrent += width;
        }

        return false;
    }

    private bool IsCellSelected(int rowIndex, int columnIndex)
    {
        if (_selectionStartRow < 0 || _selectionStartColumn < 0)
        {
            return false;
        }

        int startRow = Math.Min(_selectionStartRow, _selectionEndRow);
        int endRow = Math.Max(_selectionStartRow, _selectionEndRow);
        int startColumn = Math.Min(_selectionStartColumn, _selectionEndColumn);
        int endColumn = Math.Max(_selectionStartColumn, _selectionEndColumn);

        return rowIndex >= startRow && rowIndex <= endRow
            && columnIndex >= startColumn && columnIndex <= endColumn;
    }

    public void ClearSelection()
    {
        _selectionStartRow = -1;
        _selectionStartColumn = -1;
        _selectionEndRow = -1;
        _selectionEndColumn = -1;
        _isSelecting = false;
        InvalidateVisual();
    }

    public void CopySelectionToClipboard()
    {
        if (_selectionStartRow < 0 || _selectionStartColumn < 0)
        {
            return;
        }

        int startRow = Math.Min(_selectionStartRow, _selectionEndRow);
        int endRow = Math.Max(_selectionStartRow, _selectionEndRow);
        int startColumn = Math.Min(_selectionStartColumn, _selectionEndColumn);
        int endColumn = Math.Max(_selectionStartColumn, _selectionEndColumn);

        if (endRow < startRow || endColumn < startColumn)
        {
            return;
        }

        var sb = new System.Text.StringBuilder();
        for (int row = startRow; row <= endRow; row++)
        {
            for (int col = startColumn; col <= endColumn; col++)
            {
                if (TryGetCellValue(row, col, out var value) && value != null)
                {
                    sb.Append(value switch
                    {
                        string s => s,
                        _ => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty
                    });
                }

                if (col < endColumn)
                {
                    sb.Append('\t');
                }
            }

            if (row < endRow)
            {
                sb.AppendLine();
            }
        }

        var text = sb.ToString();
        if (text.Length == 0)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            _ = clipboard.SetTextAsync(text);
        }
    }

    private Vector SnapOffset(Vector offset)
    {
        double snappedX = SnapHorizontalOffset(offset.X);
        double snappedY = SnapVerticalOffset(offset.Y);
        return new Vector(snappedX, snappedY);
    }

    private double SnapVerticalOffset(double offset)
    {
        double rowHeight = RowHeight;
        if (rowHeight <= 0)
        {
            return offset;
        }

        int rowIndex = (int)Math.Floor(offset / rowHeight);
        return rowIndex * rowHeight;
    }

    private double SnapHorizontalOffset(double offset)
    {
        var columns = _orderedColumns;
        if (columns.Count == 0)
        {
            return 0;
        }

        int frozenCount = Math.Clamp(FrozenColumnCount, 0, columns.Count);
        if (frozenCount >= columns.Count)
        {
            return 0;
        }

        double current = 0;
        for (int i = frozenCount; i < columns.Count; i++)
        {
            double width = NormalizeWidth(columns[i].Width);
            double next = current + width;
            if (offset < next)
            {
                return current;
            }

            current = next;
        }

        return current;
    }

    private double GetFrozenWidth(IReadOnlyList<ColumnMetadata> columns, int frozenCount)
    {
        double width = 0;
        for (int i = 0; i < frozenCount; i++)
        {
            width += NormalizeWidth(columns[i].Width);
        }

        return width;
    }

    private double GetScrollableWidth(IReadOnlyList<ColumnMetadata> columns, int frozenCount)
    {
        double width = 0;
        for (int i = frozenCount; i < columns.Count; i++)
        {
            width += NormalizeWidth(columns[i].Width);
        }

        return width;
    }

    private double GetHorizontalScrollStepWidth()
    {
        var columns = _orderedColumns;
        if (columns.Count == 0)
        {
            return 1;
        }

        int frozenCount = Math.Clamp(FrozenColumnCount, 0, columns.Count);
        int startIndex = Math.Clamp(_controller.CurrentState.FirstVisibleColumn, frozenCount, columns.Count - 1);
        if (startIndex < frozenCount || startIndex >= columns.Count)
        {
            return 1;
        }

        return Math.Max(1, NormalizeWidth(columns[startIndex].Width));
    }

    private static double NormalizeWidth(double width)
    {
        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
        {
            return 1;
        }

        return width;
    }

    private void RaiseSelectionChanged(int rowIndex, int columnIndex)
    {
        SelectionChanged?.Invoke(this, new ViewportGridSelectionChangedEventArgs(rowIndex, columnIndex));
    }
}

public sealed class ViewportGridSelectionChangedEventArgs : EventArgs
{
    public ViewportGridSelectionChangedEventArgs(int rowIndex, int columnIndex)
    {
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
    }

    public int RowIndex { get; }
    public int ColumnIndex { get; }
}
