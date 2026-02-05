using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using ReactiveUI.Avalonia;
using SpecLens.Avalonia.Controls;
using SpecLens.Avalonia.Models;
using SpecLens.Avalonia.Services;
using SpecLens.Avalonia.ViewModels;
using ViewportGridControl = SpecLens.Avalonia.Controls.ViewportGrid;

namespace SpecLens.Avalonia.Views;

public partial class QueryTabView : ReactiveUserControl<QueryTabViewModel>
{
    private QueryTabViewModel? _viewModel;
    private IDataDictionaryInfoService? _ddInfoService;
    private IDataTemplate? _headerTemplate;
    private ScrollViewer? _scrollViewer;
    private ScrollBar? _horizontalScrollBar;
    private Thumb? _horizontalThumb;
    private ContextMenu? _headerContextMenu;
    private MenuItem? _sendToStartItem;
    private MenuItem? _lockColumnItem;
    private MenuItem? _unlockColumnItem;
    private DataGridColumn? _contextMenuColumn;
    private ViewportHeaderColumn? _contextMenuHeaderColumn;
    private DataGrid? _queryGrid;
    private DispatcherTimer? _snapTimer;
    private DispatcherTimer? _scrollPollTimer;
    private ViewportGridControl? _viewportGrid;
    private ItemsControl? _scrollableHeaderItems;
    private ItemsControl? _frozenHeaderItems;
    private TranslateTransform? _headerScrollTransform;
    private Canvas? _resizeGuideHost;
    private Border? _resizeGuideLine;
    private Border? _dragGuideLine;
    private Control? _resizeHeaderControl;
    private Control? _dragHeaderControl;
    private long _lastScrollChangeTimestamp = Stopwatch.GetTimestamp();
    private bool _isSnapping;
    private double _lastHorizontalOffset;
    private const double SnapDelayMs = 140;
    private bool _isThumbDragging;
    private bool _isHeaderResizing;
    private bool _resizeViaPointer;
    private string? _resizeColumnName;
    private double _resizeCurrentWidth;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private bool _isHeaderDragging;
    private bool _isHeaderDragActive;
    private string? _dragColumnName;
    private bool _dragFromFrozen;
    private Point _dragStartPoint;
    private int _dragCurrentIndex = -1;
    private int _dragTargetIndex = -1;
    private const double MinHeaderWidth = 40;
    private const double ResizeGripSize = 6;
    private const double DragThreshold = 6;

    public QueryTabView()
    {
        InitializeComponent();
        this.WhenActivated(disposables =>
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            _viewModel = viewModel;
            _ddInfoService = viewModel.DataDictionaryInfo;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Disposable.Create(() => _viewModel.PropertyChanged -= OnViewModelPropertyChanged)
                .DisposeWith(disposables);

            if (_viewModel.Filters is INotifyCollectionChanged filters)
            {
                filters.CollectionChanged += OnFiltersChanged;
                Disposable.Create(() => filters.CollectionChanged -= OnFiltersChanged)
                    .DisposeWith(disposables);
            }

            RebuildColumns();
            AttachViewportGrid();

            Disposable.Create(() =>
            {
                DetachViewportGrid();
                _viewModel = null;
                _ddInfoService = null;
            }).DisposeWith(disposables);
        });
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryTabViewModel.QueryColumnWidth))
        {
            UpdateColumnWidths();
        }
        else if (e.PropertyName == nameof(QueryTabViewModel.QueryResults)
            || e.PropertyName == nameof(QueryTabViewModel.RowCount) && _viewModel?.RowCount == 0)
        {
            _viewportGrid?.ClearSelection();
            if (_queryGrid != null)
            {
                _queryGrid.SelectedItem = null;
                _queryGrid.SelectedIndex = -1;
                _queryGrid.SelectedItems?.Clear();
            }
        }
    }

    private void OnFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildColumns();
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachViewportGrid();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        DetachViewportGrid();
    }

    private void AttachViewportGrid()
    {
        var grid = this.FindControl<ViewportGridControl>("ViewportGridControl");
        if (!ReferenceEquals(_viewportGrid, grid))
        {
            if (_viewportGrid != null)
            {
                _viewportGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
                _viewportGrid.SelectionChanged -= OnViewportGridSelectionChanged;
                _viewportGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            }

            _viewportGrid = grid;
            if (_viewportGrid != null)
            {
                _viewportGrid.ScrollInvalidated += OnViewportScrollInvalidated;
                _viewportGrid.SelectionChanged += OnViewportGridSelectionChanged;
                _viewportGrid.AddHandler(PointerPressedEvent, OnViewportGridPointerPressed, RoutingStrategies.Tunnel, true);
            }
        }

        _scrollableHeaderItems = this.FindControl<ItemsControl>("ScrollableHeaderItems");
        _frozenHeaderItems = this.FindControl<ItemsControl>("FrozenHeaderItems");
        _resizeGuideHost = this.FindControl<Canvas>("ResizeGuideHost");
        _resizeGuideLine = this.FindControl<Border>("ResizeGuideLine");
        _dragGuideLine = this.FindControl<Border>("DragGuideLine");
        if (_scrollableHeaderItems != null)
        {
            _headerScrollTransform = _scrollableHeaderItems.RenderTransform as TranslateTransform;
            if (_headerScrollTransform == null)
            {
                _headerScrollTransform = new TranslateTransform();
                _scrollableHeaderItems.RenderTransform = _headerScrollTransform;
            }
        }

        UpdateHeaderScrollOffset();
    }

    private void DetachViewportGrid()
    {
        if (_viewportGrid != null)
        {
            _viewportGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
            _viewportGrid.SelectionChanged -= OnViewportGridSelectionChanged;
            _viewportGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            _viewportGrid = null;
        }

        _scrollableHeaderItems = null;
        _frozenHeaderItems = null;
        _headerScrollTransform = null;
        _resizeGuideHost = null;
        _resizeGuideLine = null;
        _resizeHeaderControl = null;
        _dragGuideLine = null;
        _dragHeaderControl = null;
    }

    private void OnViewportScrollInvalidated(object? sender, EventArgs e)
    {
        UpdateHeaderScrollOffset();
    }

    private void OnViewportGridSelectionChanged(object? sender, ViewportGridSelectionChangedEventArgs e)
    {
        if (_viewModel == null || _ddInfoService == null)
        {
            return;
        }

        var columns = _viewModel.ViewportColumns;
        if (e.ColumnIndex < 0 || e.ColumnIndex >= columns.Count)
        {
            return;
        }

        string columnName = columns[e.ColumnIndex].Id;
        _ = UpdateDataDictionaryInfoAsync(columnName, "Query");
    }

    private void OnViewportGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var contextMenu = _viewportGrid?.ContextMenu;
        if (contextMenu == null)
        {
            return;
        }

        var point = e.GetCurrentPoint(sender as Visual ?? this);
        if (!point.Properties.IsRightButtonPressed)
        {
            return;
        }

        contextMenu.Open(_viewportGrid);
        e.Handled = true;
    }

    private void UpdateHeaderScrollOffset()
    {
        if (_viewportGrid == null || _headerScrollTransform == null)
        {
            return;
        }

        _headerScrollTransform.X = -_viewportGrid.Offset.X;
    }

    private void RebuildColumns()
    {
        if (_viewModel == null)
        {
            return;
        }

        if (this.FindControl<DataGrid>("QueryResultsGrid") is not DataGrid grid)
        {
            return;
        }

        var headerTemplate = GetHeaderTemplate();
        grid.Columns.Clear();

        var filters = _viewModel.Filters
            .Where(filter => !string.IsNullOrWhiteSpace(filter.Name))
            .ToList();
        if (filters.Count == 0)
        {
            return;
        }

        var filterLookup = filters.ToDictionary(filter => filter.Name, StringComparer.OrdinalIgnoreCase);
        var orderedNames = _viewModel.GetOrderedColumns(filters.Select(filter => filter.Name).ToList());

        foreach (var name in orderedNames)
        {
            if (!filterLookup.TryGetValue(name, out var filter))
            {
                continue;
            }

            var column = new DataGridTextColumn
            {
                Binding = new Binding($"[{filter.Name}]") { Mode = BindingMode.OneWay },
                Width = new DataGridLength(_viewModel.QueryColumnWidth),
                Header = filter,
                HeaderTemplate = headerTemplate
            };

            grid.Columns.Add(column);
        }

        if (grid.FrozenColumnCount > grid.Columns.Count)
        {
            grid.FrozenColumnCount = Math.Max(0, grid.Columns.Count);
        }
    }

    private void UpdateColumnWidths()
    {
        if (_viewModel == null)
        {
            return;
        }

        if (this.FindControl<DataGrid>("QueryResultsGrid") is not DataGrid grid)
        {
            return;
        }

        var width = new DataGridLength(_viewModel.QueryColumnWidth);
        foreach (var column in grid.Columns)
        {
            column.Width = width;
        }
    }

    private IDataTemplate? GetHeaderTemplate()
    {
        if (_headerTemplate != null)
        {
            return _headerTemplate;
        }

        if (TryGetResource("FilterHeaderTemplate", ActualThemeVariant, out var template)
            && template is IDataTemplate dataTemplate)
        {
            _headerTemplate = dataTemplate;
        }

        return _headerTemplate;
    }

    private void OnQueryGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (e.Source is Control source && source.FindAncestorOfType<TextBox>() != null)
        {
            return;
        }

        var header = (e.Source as Control)?.FindAncestorOfType<DataGridColumnHeader>();
        if (header == null)
        {
            return;
        }

        if (header.Content is ColumnFilter filter)
        {
            _viewModel.ToggleSort(filter.Name);
            e.Handled = true;
            return;
        }

        if (header.DataContext is DataGridColumn column && column.Header is ColumnFilter headerFilter)
        {
            _viewModel.ToggleSort(headerFilter.Name);
            e.Handled = true;
        }
    }

    private void OnViewportHeaderDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (e.Source is Control source && source.FindAncestorOfType<TextBox>() != null)
        {
            return;
        }

        if (sender is Control control && control.DataContext is ViewportHeaderColumn header)
        {
            _viewModel.ToggleSort(header.Name);
            e.Handled = true;
            return;
        }

        if (e.Source is Control element && element.DataContext is ViewportHeaderColumn fallback)
        {
            _viewModel.ToggleSort(fallback.Name);
            e.Handled = true;
        }
    }

    private void OnViewportHeaderResizeStarted(object? sender, VectorEventArgs e)
    {
        if (_viewModel == null || sender is not Thumb thumb)
        {
            return;
        }

        if (thumb.DataContext is not ViewportHeaderColumn header)
        {
            return;
        }

        _isHeaderResizing = true;
        _resizeViaPointer = false;
        _resizeColumnName = header.Name;
        _resizeCurrentWidth = header.Width;
        _resizeHeaderControl = thumb.FindAncestorOfType<Border>() ?? thumb.FindAncestorOfType<Control>();
        if (_resizeHeaderControl != null)
        {
            ShowResizeGuide(_resizeHeaderControl, _resizeCurrentWidth);
        }
        e.Handled = true;
    }

    private void OnViewportHeaderResizeDelta(object? sender, VectorEventArgs e)
    {
        if (!_isHeaderResizing || _viewModel == null || string.IsNullOrWhiteSpace(_resizeColumnName))
        {
            return;
        }

        _resizeCurrentWidth = Math.Max(MinHeaderWidth, _resizeCurrentWidth + e.Vector.X);
        if (_resizeHeaderControl != null)
        {
            ShowResizeGuide(_resizeHeaderControl, _resizeCurrentWidth);
        }
        e.Handled = true;
    }

    private void OnViewportHeaderResizeCompleted(object? sender, VectorEventArgs e)
    {
        if (!_isHeaderResizing)
        {
            return;
        }

        if (_viewModel != null && !string.IsNullOrWhiteSpace(_resizeColumnName))
        {
            _viewModel.SetViewportColumnWidth(_resizeColumnName, _resizeCurrentWidth);
        }

        _isHeaderResizing = false;
        _resizeViaPointer = false;
        _resizeColumnName = null;
        _resizeHeaderControl = null;
        HideResizeGuide();
        e.Handled = true;
    }

    private void OnQueryGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(sender as Visual ?? this);
        if (!point.Properties.IsRightButtonPressed)
        {
            return;
        }

        if (e.Source is Control source && source.FindAncestorOfType<TextBox>() != null)
        {
            return;
        }

        var header = (e.Source as Control)?.FindAncestorOfType<DataGridColumnHeader>();
        if (header == null)
        {
            return;
        }

        var column = GetColumnFromHeader(header);
        if (column == null)
        {
            return;
        }

        _contextMenuHeaderColumn = null;
        _contextMenuColumn = column;
        EnsureHeaderContextMenu();
        UpdateHeaderContextMenuState();
        _headerContextMenu?.Open(header);
        e.Handled = true;
    }

    private void OnQueryGridCurrentCellChanged(object? sender, EventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (sender is not DataGrid grid || grid.CurrentColumn == null)
        {
            return;
        }

        if (grid.CurrentColumn.Header is ColumnFilter filter)
        {
            _ = UpdateDataDictionaryInfoAsync(filter.Name, "Query");
        }
    }

    private void OnViewportHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        var point = e.GetCurrentPoint(sender as Visual ?? this);
        if (point.Properties.IsRightButtonPressed)
        {
            if (e.Source is Control source && source.FindAncestorOfType<TextBox>() != null)
            {
                return;
            }

            Control? contextMenuTarget = sender as Control;
            if (sender is Control control && control.DataContext is ViewportHeaderColumn contextHeader)
            {
                _contextMenuHeaderColumn = contextHeader;
                contextMenuTarget = control;
            }
            else if (e.Source is Control element && element.DataContext is ViewportHeaderColumn fallback)
            {
                _contextMenuHeaderColumn = fallback;
                contextMenuTarget ??= element;
            }
            else
            {
                return;
            }

            _contextMenuColumn = null;
            EnsureHeaderContextMenu();
            UpdateHeaderContextMenuState();
            _headerContextMenu?.Open(contextMenuTarget ?? this);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source is Control input && input.FindAncestorOfType<TextBox>() != null)
        {
            return;
        }

        if (!TryGetHeaderContext(sender, e, out var header, out var headerControl, out var itemsControl, out var isFrozen))
        {
            return;
        }

        var localPoint = e.GetPosition(headerControl);
        if (IsNearRightEdge(headerControl, localPoint))
        {
            StartHeaderResize(header, headerControl, localPoint);
            e.Pointer.Capture(headerControl);
            e.Handled = true;
            return;
        }

        StartHeaderDrag(header, headerControl, itemsControl, isFrozen, e);
        e.Pointer.Capture(headerControl);
    }

    private void OnViewportHeaderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (!_isHeaderResizing && !_isHeaderDragging && e.Source is Control source &&
            source.FindAncestorOfType<TextBox>() != null)
        {
            return;
        }

        if (!TryGetHeaderContext(sender, e, out var header, out var headerControl, out var itemsControl, out var isFrozen))
        {
            return;
        }

        var localPoint = e.GetPosition(headerControl);
        if (_isHeaderResizing && _resizeViaPointer && !string.IsNullOrWhiteSpace(_resizeColumnName))
        {
            double delta = localPoint.X - _resizeStartX;
            double width = Math.Max(MinHeaderWidth, _resizeStartWidth + delta);
            _resizeCurrentWidth = width;
            ShowResizeGuide(_resizeHeaderControl ?? headerControl, _resizeCurrentWidth);
            e.Handled = true;
            return;
        }

        if (_isHeaderDragging && !string.IsNullOrWhiteSpace(_dragColumnName) && itemsControl != null)
        {
            var position = e.GetPosition(itemsControl);
            if (!_isHeaderDragActive && Math.Abs(position.X - _dragStartPoint.X) < DragThreshold)
            {
                return;
            }

            if (!_isHeaderDragActive)
            {
                _isHeaderDragActive = true;
                _dragHeaderControl ??= headerControl;
                SetDragVisualState(_dragHeaderControl, true);
            }

            int targetIndex = GetTargetColumnIndex(itemsControl, position.X, isFrozen);
            if (targetIndex >= 0)
            {
                _dragTargetIndex = targetIndex;
                _dragCurrentIndex = targetIndex;
                ShowDragGuide(itemsControl, targetIndex, isFrozen);
            }

            e.Handled = true;
            return;
        }

        if (IsNearRightEdge(headerControl, localPoint))
        {
            headerControl.Cursor = new Cursor(StandardCursorType.SizeWestEast);
        }
        else
        {
            headerControl.Cursor = new Cursor(StandardCursorType.Arrow);
        }
    }

    private void OnViewportHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isHeaderResizing && !_isHeaderDragging)
        {
            return;
        }

        bool shouldHandle = _isHeaderResizing || _isHeaderDragActive;
        if (_isHeaderResizing && _viewModel != null && !string.IsNullOrWhiteSpace(_resizeColumnName))
        {
            _viewModel.SetViewportColumnWidth(_resizeColumnName, _resizeCurrentWidth);
        }

        if (_isHeaderDragActive && _viewModel != null && !string.IsNullOrWhiteSpace(_dragColumnName) && _dragTargetIndex >= 0)
        {
            _viewModel.MoveColumnToIndex(_dragColumnName, _dragTargetIndex);
        }

        _isHeaderResizing = false;
        _resizeViaPointer = false;
        _isHeaderDragging = false;
        _isHeaderDragActive = false;
        _resizeColumnName = null;
        _dragColumnName = null;
        _dragCurrentIndex = -1;
        _dragTargetIndex = -1;
        _resizeHeaderControl = null;
        SetDragVisualState(_dragHeaderControl, false);
        _dragHeaderControl = null;
        HideResizeGuide();
        HideDragGuide();
        if (e.Pointer.Captured is not null)
        {
            e.Pointer.Capture(null);
        }

        if (shouldHandle)
        {
            e.Handled = true;
        }
    }

    private bool TryGetHeaderContext(
        object? sender,
        PointerEventArgs e,
        out ViewportHeaderColumn header,
        out Control headerControl,
        out ItemsControl? itemsControl,
        out bool isFrozen)
    {
        header = null!;
        headerControl = null!;
        itemsControl = null;
        isFrozen = false;

        if (sender is Control control && control.DataContext is ViewportHeaderColumn senderHeader)
        {
            header = senderHeader;
            headerControl = control;
        }
        else if (e.Source is Control source && source.DataContext is ViewportHeaderColumn sourceHeader)
        {
            header = sourceHeader;
            headerControl = source;
        }
        else if (e.Source is Control element && element.FindAncestorOfType<Control>() is Control ancestor &&
                 ancestor.DataContext is ViewportHeaderColumn ancestorHeader)
        {
            header = ancestorHeader;
            headerControl = ancestor;
        }
        else
        {
            return false;
        }

        itemsControl = headerControl.FindAncestorOfType<ItemsControl>();
        if (itemsControl != null)
        {
            isFrozen = ReferenceEquals(itemsControl, _frozenHeaderItems);
        }

        return true;
    }

    private void StartHeaderResize(ViewportHeaderColumn header, Control headerControl, Point localPoint)
    {
        _isHeaderResizing = true;
        _resizeViaPointer = true;
        _resizeColumnName = header.Name;
        _resizeStartX = localPoint.X;
        _resizeStartWidth = header.Width;
        _resizeCurrentWidth = header.Width;
        headerControl.Cursor = new Cursor(StandardCursorType.SizeWestEast);
        _resizeHeaderControl = headerControl;
        ShowResizeGuide(headerControl, _resizeCurrentWidth);
    }

    private void ShowResizeGuide(Control headerControl, double width)
    {
        if (_resizeGuideHost == null || _resizeGuideLine == null)
        {
            return;
        }

        var origin = headerControl.TranslatePoint(new Point(0, 0), _resizeGuideHost);
        if (origin == null)
        {
            return;
        }

        double x = origin.Value.X + width;
        if (x < 0)
        {
            x = 0;
        }

        Canvas.SetLeft(_resizeGuideLine, x - 0.5);
        Canvas.SetTop(_resizeGuideLine, 0);
        double height = _resizeGuideHost.Bounds.Height;
        if (height <= 0 && _resizeGuideHost.Parent is Control parent)
        {
            height = parent.Bounds.Height;
        }
        if (height > 0)
        {
            _resizeGuideLine.Height = height;
        }
        _resizeGuideLine.IsVisible = true;
    }

    private void HideResizeGuide()
    {
        if (_resizeGuideLine != null)
        {
            _resizeGuideLine.IsVisible = false;
        }
    }

    private void ShowDragGuide(ItemsControl itemsControl, int targetIndex, bool isFrozen)
    {
        if (_resizeGuideHost == null || _dragGuideLine == null || _viewModel == null)
        {
            return;
        }

        var origin = itemsControl.TranslatePoint(new Point(0, 0), _resizeGuideHost);
        if (origin == null)
        {
            return;
        }

        var headers = isFrozen ? _viewModel.FrozenHeaderColumns : _viewModel.ScrollableHeaderColumns;
        if (headers.Count == 0)
        {
            return;
        }

        int frozenCount = Math.Clamp(_viewModel.FrozenColumnCount, 0, _viewModel.ViewportColumns.Count);
        int localIndex = isFrozen ? targetIndex : Math.Clamp(targetIndex - frozenCount, 0, headers.Count);
        double x = origin.Value.X;
        for (int i = 0; i < localIndex && i < headers.Count; i++)
        {
            x += Math.Max(1, headers[i].Width);
        }

        Canvas.SetLeft(_dragGuideLine, x - (_dragGuideLine.Width / 2));
        Canvas.SetTop(_dragGuideLine, 0);
        double height = _resizeGuideHost.Bounds.Height;
        if (height <= 0 && _resizeGuideHost.Parent is Control parent)
        {
            height = parent.Bounds.Height;
        }
        if (height > 0)
        {
            _dragGuideLine.Height = height;
        }
        _dragGuideLine.IsVisible = true;
    }

    private void HideDragGuide()
    {
        if (_dragGuideLine != null)
        {
            _dragGuideLine.IsVisible = false;
        }
    }

    private static void SetDragVisualState(Control? headerControl, bool isDragging)
    {
        if (headerControl == null)
        {
            return;
        }

        headerControl.Opacity = isDragging ? 0.65 : 1;
        headerControl.Cursor = isDragging
            ? new Cursor(StandardCursorType.SizeAll)
            : new Cursor(StandardCursorType.Arrow);
    }

    private void StartHeaderDrag(ViewportHeaderColumn header, Control headerControl, ItemsControl? itemsControl, bool isFrozen, PointerPressedEventArgs e)
    {
        _isHeaderDragging = true;
        _isHeaderDragActive = false;
        _dragColumnName = header.Name;
        _dragFromFrozen = isFrozen;
        _dragCurrentIndex = GetColumnIndex(header.Name);
        _dragTargetIndex = _dragCurrentIndex;
        _dragHeaderControl = headerControl;
        _dragStartPoint = itemsControl != null ? e.GetPosition(itemsControl) : e.GetPosition(this);
    }

    private bool IsNearRightEdge(Control headerControl, Point localPoint)
    {
        if (headerControl.Bounds.Width <= 0)
        {
            return false;
        }

        return headerControl.Bounds.Width - localPoint.X <= ResizeGripSize;
    }

    private int GetTargetColumnIndex(ItemsControl itemsControl, double x, bool isFrozen)
    {
        if (_viewModel == null)
        {
            return -1;
        }

        var headers = isFrozen ? _viewModel.FrozenHeaderColumns : _viewModel.ScrollableHeaderColumns;
        if (headers.Count == 0)
        {
            return -1;
        }

        double current = 0;
        int localIndex = headers.Count - 1;
        for (int i = 0; i < headers.Count; i++)
        {
            double width = Math.Max(1, headers[i].Width);
            double midpoint = current + (width / 2);
            if (x < midpoint)
            {
                localIndex = i;
                break;
            }

            current += width;
        }

        int frozenCount = Math.Clamp(_viewModel.FrozenColumnCount, 0, _viewModel.ViewportColumns.Count);
        if (isFrozen)
        {
            return Math.Clamp(localIndex, 0, Math.Max(0, frozenCount - 1));
        }

        int target = frozenCount + localIndex;
        return Math.Clamp(target, frozenCount, Math.Max(frozenCount, _viewModel.ViewportColumns.Count - 1));
    }

    private int GetColumnIndex(string columnName)
    {
        if (_viewModel == null)
        {
            return -1;
        }

        var columns = _viewModel.ViewportColumns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i].Id, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void OnQueryGridAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        _queryGrid = grid;
        _lastScrollChangeTimestamp = Stopwatch.GetTimestamp();
        grid.TemplateApplied += OnQueryGridTemplateApplied;
        grid.LayoutUpdated += OnQueryGridLayoutUpdated;
        Dispatcher.UIThread.Post(() => AttachScrollViewer(grid), DispatcherPriority.Loaded);
        EnsureScrollPolling();
    }

    private void OnQueryGridDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            grid.TemplateApplied -= OnQueryGridTemplateApplied;
            grid.LayoutUpdated -= OnQueryGridLayoutUpdated;
        }

        _queryGrid = null;
        DetachScrollViewer();
        _scrollPollTimer?.Stop();
    }

    private void OnQueryGridTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            AttachScrollViewer(grid);
            AttachHorizontalScrollBarFromTemplate(e);
        }
    }

    private void OnQueryGridLayoutUpdated(object? sender, EventArgs e)
    {
        if (_queryGrid == null)
        {
            return;
        }

        if (_horizontalScrollBar == null || _horizontalThumb == null)
        {
            AttachScrollViewer(_queryGrid);
        }
    }

    private void AttachScrollViewer(DataGrid grid)
    {
        var scrollViewer = FindDescendant<ScrollViewer>(grid);
        if (scrollViewer != null && !ReferenceEquals(_scrollViewer, scrollViewer))
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnGridScrollChanged;
                _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
            }

            _scrollViewer = scrollViewer;
            _scrollViewer.ScrollChanged += OnGridScrollChanged;
            _scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
            _lastHorizontalOffset = _scrollViewer.Offset.X;
        }

        AttachHorizontalScrollBar(grid);
        EnsureSnapTimer();
        _scrollPollTimer?.Start();
    }

    private void DetachScrollViewer()
    {
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged -= OnGridScrollChanged;
            _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
            _scrollViewer = null;
        }

        DetachHorizontalScrollBar();
        _snapTimer?.Stop();
    }

    private void AttachHorizontalScrollBar(DataGrid grid)
    {
        var scrollBar = FindDescendants<ScrollBar>(grid)
            .FirstOrDefault(bar => bar.Orientation == Orientation.Horizontal);
        if (scrollBar == null)
        {
            return;
        }

        AttachHorizontalScrollBar(scrollBar);
    }

    private void DetachHorizontalScrollBar()
    {
        if (_horizontalScrollBar == null)
        {
            return;
        }

        _horizontalScrollBar.ValueChanged -= OnHorizontalScrollBarValueChanged;
        _horizontalScrollBar.Scroll -= OnHorizontalScrollBarScroll;
        _horizontalScrollBar.TemplateApplied -= OnHorizontalScrollBarTemplateApplied;
        DetachHorizontalThumb();
        _horizontalScrollBar = null;
    }

    private void OnHorizontalScrollBarTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (e.NameScope != null)
        {
            var thumb = e.NameScope.Find<Thumb>("PART_Thumb");
            if (thumb != null)
            {
                AttachHorizontalThumb(thumb);
                return;
            }
        }

        EnsureHorizontalThumb();
    }

    private void EnsureHorizontalThumb()
    {
        if (_horizontalScrollBar == null)
        {
            return;
        }

        var thumb = FindDescendant<Thumb>(_horizontalScrollBar);
        if (thumb != null)
        {
            AttachHorizontalThumb(thumb);
        }
    }

    private void AttachHorizontalThumb(Thumb thumb)
    {
        if (ReferenceEquals(_horizontalThumb, thumb))
        {
            return;
        }

        DetachHorizontalThumb();
        _horizontalThumb = thumb;
        _horizontalThumb.DragStarted += OnHorizontalThumbDragStarted;
        _horizontalThumb.DragCompleted += OnHorizontalThumbDragCompleted;
        _horizontalThumb.DragDelta += OnHorizontalThumbDragDelta;
    }

    private void DetachHorizontalThumb()
    {
        if (_horizontalThumb == null)
        {
            return;
        }

        _horizontalThumb.DragStarted -= OnHorizontalThumbDragStarted;
        _horizontalThumb.DragCompleted -= OnHorizontalThumbDragCompleted;
        _horizontalThumb.DragDelta -= OnHorizontalThumbDragDelta;
        _horizontalThumb = null;
    }

    private void EnsureSnapTimer()
    {
        if (_snapTimer != null)
        {
            return;
        }

        _snapTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(SnapDelayMs)
        };
        _snapTimer.Tick += OnSnapTimerTick;
    }

    private void EnsureScrollPolling()
    {
        if (_scrollPollTimer != null)
        {
            return;
        }

        _scrollPollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _scrollPollTimer.Tick += (_, _) =>
        {
            if (_scrollViewer == null || _isSnapping || _isThumbDragging)
            {
                return;
            }

            double offsetX = _scrollViewer.Offset.X;
            if (Math.Abs(offsetX - _lastHorizontalOffset) < 0.1)
            {
                return;
            }

            _lastHorizontalOffset = offsetX;
            _lastScrollChangeTimestamp = Stopwatch.GetTimestamp();
            EnsureSnapTimer();
            _snapTimer?.Stop();
            _snapTimer?.Start();
        };
    }

    private void OnGridScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_isSnapping)
        {
            return;
        }

        if (_isThumbDragging)
        {
            return;
        }

        if (_scrollViewer == null)
        {
            return;
        }

        double currentOffsetX = _scrollViewer.Offset.X;
        if (Math.Abs(currentOffsetX - _lastHorizontalOffset) < 0.1)
        {
            return;
        }

        _lastHorizontalOffset = currentOffsetX;
        _lastScrollChangeTimestamp = Stopwatch.GetTimestamp();
        EnsureSnapTimer();
        _snapTimer?.Stop();
        _snapTimer?.Start();
    }

    private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_isSnapping)
        {
            return;
        }

        if (_isThumbDragging)
        {
            return;
        }

        if (e.Property != ScrollViewer.OffsetProperty)
        {
            return;
        }

        var offset = (Vector)e.NewValue!;
        if (Math.Abs(offset.X - _lastHorizontalOffset) < 0.1)
        {
            return;
        }

        _lastHorizontalOffset = offset.X;
        _lastScrollChangeTimestamp = Stopwatch.GetTimestamp();
        EnsureSnapTimer();
        _snapTimer?.Stop();
        _snapTimer?.Start();
    }

    private void OnHorizontalScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSnapping)
        {
            return;
        }

        _lastHorizontalOffset = e.NewValue;
        if (_isThumbDragging)
        {
            return;
        }

        _lastScrollChangeTimestamp = Stopwatch.GetTimestamp();
        EnsureSnapTimer();
        _snapTimer?.Stop();
        _snapTimer?.Start();
    }

    private void OnHorizontalScrollBarScroll(object? sender, ScrollEventArgs e)
    {
        _lastHorizontalOffset = e.NewValue;
        if (e.ScrollEventType == ScrollEventType.EndScroll)
        {
            Dispatcher.UIThread.Post(SnapToNearestColumn, DispatcherPriority.Background);
        }
    }

    private void OnHorizontalThumbDragStarted(object? sender, VectorEventArgs e)
    {
        _isThumbDragging = true;
        _snapTimer?.Stop();
    }

    private void OnHorizontalThumbDragDelta(object? sender, VectorEventArgs e)
    {
        if (_horizontalScrollBar == null)
        {
            return;
        }

        _lastHorizontalOffset = _horizontalScrollBar.Value;
    }

    private void OnHorizontalThumbDragCompleted(object? sender, VectorEventArgs e)
    {
        _isThumbDragging = false;
        Dispatcher.UIThread.Post(SnapToNearestColumn, DispatcherPriority.Background);
    }

    private void OnSnapTimerTick(object? sender, EventArgs e)
    {
        if (_isSnapping)
        {
            _snapTimer?.Stop();
            return;
        }

        if (Stopwatch.GetElapsedTime(_lastScrollChangeTimestamp).TotalMilliseconds < SnapDelayMs)
        {
            return;
        }

        _snapTimer?.Stop();
        SnapToNearestColumn();
    }

    private void SnapToNearestColumn()
    {
        if (!TryGetQueryGrid(out var grid))
        {
            return;
        }

        if (grid.Columns.Count == 0)
        {
            return;
        }

        double offset = GetHorizontalOffset();
        double target = GetNearestColumnStart(grid, offset);
        if (Math.Abs(offset - target) <= 0.5)
        {
            return;
        }

        _isSnapping = true;
        SetHorizontalOffset(target);
        _lastHorizontalOffset = target;
        _isSnapping = false;
    }

    private void OnQueryColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        UpdateColumnOrder(grid);
        if (grid.FrozenColumnCount > grid.Columns.Count)
        {
            grid.FrozenColumnCount = Math.Max(0, grid.Columns.Count);
        }
    }

    private void OnGoToColumnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.SelectedItem is not string columnName)
        {
            return;
        }

        if (_viewModel?.UseViewportGrid == true)
        {
            if (_viewportGrid == null)
            {
                AttachViewportGrid();
            }

            if (_viewportGrid == null)
            {
                return;
            }

            var columns = _viewModel.ViewportColumns;
            if (columns.Count == 0)
            {
                return;
            }

            int targetIndex = -1;
            for (int i = 0; i < columns.Count; i++)
            {
                if (string.Equals(columns[i].Id, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    targetIndex = i;
                    break;
                }
            }

            if (targetIndex < 0)
            {
                return;
            }

            int frozenCount = Math.Clamp(_viewModel.FrozenColumnCount, 0, columns.Count);
            if (targetIndex < frozenCount)
            {
                _viewportGrid.Offset = new Vector(0, _viewportGrid.Offset.Y);
            }
            else
            {
                double offset = 0;
                for (int i = frozenCount; i < targetIndex; i++)
                {
                    double width = columns[i].Width;
                    if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
                    {
                        width = 1;
                    }

                    offset += width;
                }

                _viewportGrid.Offset = new Vector(offset, _viewportGrid.Offset.Y);
            }

            _viewportGrid.Focus();
            Dispatcher.UIThread.Post(() => combo.SelectedIndex = -1, DispatcherPriority.Background);
            return;
        }

        if (!TryGetQueryGrid(out var grid))
        {
            return;
        }

        var target = grid.Columns.FirstOrDefault(column =>
            string.Equals(GetColumnName(column), columnName, StringComparison.OrdinalIgnoreCase));
        if (target == null)
        {
            return;
        }

        if (grid.ItemsSource is System.Collections.IList items && items.Count > 0)
        {
            var item = items[0];
            grid.ScrollIntoView(item, target);
            grid.UpdateLayout();
            ScrollColumnToLeft(target);
            grid.Focus();
        }

        Dispatcher.UIThread.Post(() => combo.SelectedIndex = -1, DispatcherPriority.Background);
    }

    private void OnGoToDropDownOpened(object? sender, EventArgs e)
    {
        SetGridHitTestingEnabled(false);
    }

    private void OnGoToDropDownClosed(object? sender, EventArgs e)
    {
        SetGridHitTestingEnabled(true);
    }

    private void SetGridHitTestingEnabled(bool isEnabled)
    {
        if (_viewportGrid == null)
        {
            _viewportGrid = this.FindControl<ViewportGridControl>("ViewportGridControl");
        }

        _viewportGrid?.SetValue(IsHitTestVisibleProperty, isEnabled);

        if (_queryGrid == null)
        {
            _queryGrid = this.FindControl<DataGrid>("QueryResultsGrid");
        }

        _queryGrid?.SetValue(IsHitTestVisibleProperty, isEnabled);
    }

    private void EnsureHeaderContextMenu()
    {
        if (_headerContextMenu != null)
        {
            return;
        }

        _sendToStartItem = new MenuItem { Header = "Send to Start" };
        _sendToStartItem.Click += OnSendToStartClick;

        _lockColumnItem = new MenuItem { Header = "Lock Column" };
        _lockColumnItem.Click += OnLockColumnClick;

        _unlockColumnItem = new MenuItem { Header = "Unlock Column" };
        _unlockColumnItem.Click += OnUnlockColumnClick;

        _headerContextMenu = new ContextMenu();
        _headerContextMenu.Items.Add(_sendToStartItem);
        _headerContextMenu.Items.Add(_lockColumnItem);
        _headerContextMenu.Items.Add(_unlockColumnItem);
    }

    private void UpdateHeaderContextMenuState()
    {
        if (_contextMenuColumn == null || _sendToStartItem == null || _lockColumnItem == null || _unlockColumnItem == null)
        {
            if (_contextMenuHeaderColumn == null || _sendToStartItem == null || _lockColumnItem == null || _unlockColumnItem == null)
            {
                return;
            }
        }

        if (_contextMenuHeaderColumn != null)
        {
            if (_viewModel == null)
            {
                return;
            }

            var columns = _viewModel.ViewportColumns;
            int frozenCount = Math.Clamp(_viewModel.FrozenColumnCount, 0, columns.Count);
            int index = GetViewportColumnIndex(_contextMenuHeaderColumn.Name, columns);
            bool headerIsFrozen = index >= 0 && index < frozenCount;
            _sendToStartItem.IsVisible = !headerIsFrozen;
            _lockColumnItem.IsVisible = !headerIsFrozen;
            _unlockColumnItem.IsVisible = headerIsFrozen;
            return;
        }

        if (!TryGetQueryGrid(out var grid))
        {
            return;
        }

        bool gridIsFrozen = _contextMenuColumn!.DisplayIndex < grid.FrozenColumnCount;
        _sendToStartItem.IsVisible = !gridIsFrozen;
        _lockColumnItem.IsVisible = !gridIsFrozen;
        _unlockColumnItem.IsVisible = gridIsFrozen;
    }

    private void OnSendToStartClick(object? sender, RoutedEventArgs e)
    {
        if (_contextMenuHeaderColumn != null)
        {
            _viewModel?.SendColumnToStart(_contextMenuHeaderColumn.Name);
            return;
        }

        if (_contextMenuColumn == null)
        {
            return;
        }

        SendColumnToStart(_contextMenuColumn);
    }

    private void OnLockColumnClick(object? sender, RoutedEventArgs e)
    {
        if (_contextMenuHeaderColumn != null)
        {
            _viewModel?.LockColumn(_contextMenuHeaderColumn.Name);
            return;
        }

        if (_contextMenuColumn == null)
        {
            return;
        }

        LockColumn(_contextMenuColumn);
    }

    private void OnUnlockColumnClick(object? sender, RoutedEventArgs e)
    {
        if (_contextMenuHeaderColumn != null)
        {
            _viewModel?.UnlockColumn(_contextMenuHeaderColumn.Name);
            return;
        }

        if (_contextMenuColumn == null)
        {
            return;
        }

        UnlockColumn(_contextMenuColumn);
    }

    private void OnViewportCopyClick(object? sender, RoutedEventArgs e)
    {
        _viewportGrid?.CopySelectionToClipboard();
    }

    private void OnGridEnterKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel == null)
        {
            return;
        }

        if (_viewModel.IsQueryRunning)
        {
            if (((ICommand)_viewModel.StopQueryCommand).CanExecute(null))
            {
                ((ICommand)_viewModel.StopQueryCommand).Execute(null);
            }
        }
        else
        {
            if (((ICommand)_viewModel.RunQueryCommand).CanExecute(null))
            {
                ((ICommand)_viewModel.RunQueryCommand).Execute(null);
            }
        }

        e.Handled = true;
    }

    private async void OnFilterTextBoxGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        string? columnName = null;
        if (sender is Control control)
        {
            if (control.DataContext is ColumnFilter filter)
            {
                columnName = filter.Name;
            }
            else if (control.DataContext is ViewportHeaderColumn header)
            {
                columnName = header.Name;
            }
        }

        if (!string.IsNullOrWhiteSpace(columnName))
        {
            await UpdateDataDictionaryInfoAsync(columnName, "Filter");
        }
    }

    private async Task UpdateDataDictionaryInfoAsync(string columnName, string source)
    {
        if (_ddInfoService == null || _viewModel == null)
        {
            return;
        }

        if (_viewModel.TryGetDataDictionaryItem(columnName, out var dataItem))
        {
            await _ddInfoService.ShowForDataItemAsync(dataItem, columnName, source);
        }
        else
        {
            _ddInfoService.Clear();
        }
    }

    private void SendColumnToStart(DataGridColumn column)
    {
        if (!TryGetQueryGrid(out var grid))
        {
            return;
        }

        if (column.DisplayIndex < grid.FrozenColumnCount)
        {
            return;
        }

        int targetIndex = grid.FrozenColumnCount > 0 ? grid.FrozenColumnCount : 0;
        MoveColumn(grid, column, targetIndex);
        UpdateColumnOrder(grid);
    }

    private void LockColumn(DataGridColumn column)
    {
        if (!TryGetQueryGrid(out var grid))
        {
            return;
        }

        int targetIndex = grid.FrozenColumnCount;
        MoveColumn(grid, column, targetIndex);
        grid.FrozenColumnCount = targetIndex + 1;
        UpdateColumnOrder(grid);
    }

    private void UnlockColumn(DataGridColumn column)
    {
        if (!TryGetQueryGrid(out var grid))
        {
            return;
        }

        int frozenCount = grid.FrozenColumnCount;
        if (frozenCount <= 0 || column.DisplayIndex >= frozenCount)
        {
            return;
        }

        int lastFrozenIndex = frozenCount - 1;
        MoveColumn(grid, column, lastFrozenIndex);
        grid.FrozenColumnCount = frozenCount - 1;
        UpdateColumnOrder(grid);
    }

    private static void MoveColumn(DataGrid grid, DataGridColumn column, int targetIndex)
    {
        var orderedColumns = grid.Columns
            .OrderBy(c => c.DisplayIndex)
            .ToList();

        int currentIndex = orderedColumns.IndexOf(column);
        if (currentIndex < 0)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, orderedColumns.Count - 1);
        if (currentIndex == targetIndex)
        {
            return;
        }

        orderedColumns.RemoveAt(currentIndex);
        orderedColumns.Insert(targetIndex, column);

        for (int i = 0; i < orderedColumns.Count; i++)
        {
            orderedColumns[i].DisplayIndex = i;
        }
    }

    private void UpdateColumnOrder(DataGrid grid)
    {
        if (_viewModel == null)
        {
            return;
        }

        var ordered = grid.Columns
            .OrderBy(column => column.DisplayIndex)
            .Select(GetColumnName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        _viewModel.UpdateColumnOrder(ordered);
    }

    private static double GetNearestColumnStart(DataGrid grid, double offset)
    {
        double current = 0;
        double lastStart = 0;
        int frozenCount = grid.FrozenColumnCount;

        foreach (var column in grid.Columns.OrderBy(c => c.DisplayIndex))
        {
            if (column.DisplayIndex < frozenCount)
            {
                continue;
            }

            double nextStart = current + column.ActualWidth;
            if (offset <= nextStart)
            {
                return (offset - current) <= (nextStart - offset) ? current : nextStart;
            }

            lastStart = current;
            current = nextStart;
        }

        return Math.Max(0, lastStart);
    }

    private void ScrollColumnToLeft(DataGridColumn target)
    {
        if (!TryGetQueryGrid(out var grid))
        {
            return;
        }

        int frozenCount = grid.FrozenColumnCount;
        if (target.DisplayIndex < frozenCount)
        {
            SetHorizontalOffset(0);
            return;
        }

        double offset = 0;
        foreach (var column in grid.Columns.OrderBy(c => c.DisplayIndex))
        {
            if (column.DisplayIndex < frozenCount)
            {
                continue;
            }

            if (ReferenceEquals(column, target))
            {
                break;
            }

            offset += column.ActualWidth;
        }

        double targetOffset = Math.Max(0, offset - 8);
        if (Math.Abs(GetHorizontalOffset() - targetOffset) > 0.5)
        {
            SetHorizontalOffset(targetOffset);
        }
    }

    private ScrollViewer? GetScrollViewer(DataGrid grid)
    {
        _scrollViewer ??= FindDescendant<ScrollViewer>(grid);
        return _scrollViewer;
    }

    private double GetHorizontalOffset()
    {
        if (_scrollViewer != null)
        {
            return _scrollViewer.Offset.X;
        }

        if (_horizontalScrollBar != null)
        {
            return _horizontalScrollBar.Value;
        }

        return 0;
    }

    private void SetHorizontalOffset(double value)
    {
        if (_scrollViewer != null)
        {
            _scrollViewer.Offset = new Vector(value, _scrollViewer.Offset.Y);
            return;
        }

        if (_horizontalScrollBar != null)
        {
            _horizontalScrollBar.Value = value;
        }
    }

    private DataGridColumn? GetColumnFromHeader(DataGridColumnHeader header)
    {
        if (header.DataContext is DataGridColumn column)
        {
            return column;
        }

        if (TryGetQueryGrid(out var grid))
        {
            return grid.Columns.FirstOrDefault(c => ReferenceEquals(c.Header, header.Content));
        }

        return null;
    }

    private bool TryGetQueryGrid(out DataGrid grid)
    {
        grid = this.FindControl<DataGrid>("QueryResultsGrid")!;
        return grid != null;
    }

    private static string GetColumnName(DataGridColumn column)
    {
        if (column.Header is ColumnFilter filter)
        {
            return filter.Name;
        }

        return column.Header?.ToString() ?? string.Empty;
    }

    private static int GetViewportColumnIndex(string columnName, IReadOnlyList<ViewportGrid.Core.Models.ColumnMetadata> columns)
    {
        for (int i = 0; i < columns.Count; i++)
        {
            if (string.Equals(columns[i].Id, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static T? FindDescendant<T>(Visual root) where T : Visual
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static IEnumerable<T> FindDescendants<T>(Visual root) where T : Visual
    {
        foreach (var child in root.GetVisualChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindDescendants<T>(child))
            {
                yield return nested;
            }
        }
    }
    private void AttachHorizontalScrollBarFromTemplate(TemplateAppliedEventArgs e)
    {
        if (e.NameScope == null)
        {
            return;
        }

        var scrollBar = e.NameScope.Find<ScrollBar>("PART_HorizontalScrollbar")
            ?? e.NameScope.Find<ScrollBar>("PART_HorizontalScrollBar");
        if (scrollBar == null)
        {
            return;
        }

        AttachHorizontalScrollBar(scrollBar);
    }

    private void AttachHorizontalScrollBar(ScrollBar scrollBar)
    {
        if (ReferenceEquals(_horizontalScrollBar, scrollBar))
        {
            EnsureHorizontalThumb();
            return;
        }

        DetachHorizontalScrollBar();
        _horizontalScrollBar = scrollBar;
        _horizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;
        _horizontalScrollBar.Scroll += OnHorizontalScrollBarScroll;
        _horizontalScrollBar.TemplateApplied += OnHorizontalScrollBarTemplateApplied;
        EnsureHorizontalThumb();
    }
}
