using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using ReactiveUI;
using SpecLens.Avalonia.Controls;
using SpecLens.Avalonia.Models;
using SpecLens.Avalonia.Services;
using SpecLens.Avalonia.ViewModels;
using ViewportGrid.Core.Models;
using ViewportGridControl = SpecLens.Avalonia.Controls.ViewportGrid;

namespace SpecLens.Avalonia.Views;

public partial class SpecsTabView : ReactiveUserControl<SpecsTabViewModel>
{
    private IDataDictionaryInfoService? _ddInfoService;
    private SpecsTabViewModel? _viewModel;
    private ViewportGridControl? _columnsGrid;
    private ViewportGridControl? _viewColumnsGrid;
    private ViewportGridControl? _indexesGrid;
    private ViewportGridControl? _viewTablesGrid;
    private ViewportGridControl? _viewJoinsGrid;
    private ItemsControl? _columnsHeaderItems;
    private ItemsControl? _viewColumnsHeaderItems;
    private ItemsControl? _indexesHeaderItems;
    private ItemsControl? _viewTablesHeaderItems;
    private ItemsControl? _viewJoinsHeaderItems;
    private TranslateTransform? _columnsHeaderTransform;
    private TranslateTransform? _viewColumnsHeaderTransform;
    private TranslateTransform? _indexesHeaderTransform;
    private TranslateTransform? _viewTablesHeaderTransform;
    private TranslateTransform? _viewJoinsHeaderTransform;
    private Canvas? _columnsResizeGuideHost;
    private Border? _columnsResizeGuideLine;
    private Canvas? _indexesResizeGuideHost;
    private Border? _indexesResizeGuideLine;
    private Canvas? _viewColumnsResizeGuideHost;
    private Border? _viewColumnsResizeGuideLine;
    private Canvas? _viewTablesResizeGuideHost;
    private Border? _viewTablesResizeGuideLine;
    private Canvas? _viewJoinsResizeGuideHost;
    private Border? _viewJoinsResizeGuideLine;
    private Control? _resizeHeaderControl;
    private bool _isHeaderResizing;
    private bool _resizeViaPointer;
    private string? _resizeColumnId;
    private double _resizeCurrentWidth;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private ResizeTarget _resizeTarget;
    private const double MinHeaderWidth = 60;
    private const double ResizeGripSize = 6;

    public SpecsTabView()
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
            _ddInfoService ??= App.GetService<IDataDictionaryInfoService>();

            Disposable.Create(() => _viewModel = null).DisposeWith(disposables);
        });
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachViewportGrids();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        DetachViewportGrids();
    }

    private void AttachViewportGrids()
    {
        var columnsGrid = this.FindControl<ViewportGridControl>("ColumnsViewportGrid");
        if (!ReferenceEquals(_columnsGrid, columnsGrid))
        {
            if (_columnsGrid != null)
            {
                _columnsGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
                _columnsGrid.SelectionChanged -= OnColumnsViewportSelectionChanged;
                _columnsGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            }

            _columnsGrid = columnsGrid;
            if (_columnsGrid != null)
            {
                _columnsGrid.ScrollInvalidated += OnViewportScrollInvalidated;
                _columnsGrid.SelectionChanged += OnColumnsViewportSelectionChanged;
                _columnsGrid.AddHandler(PointerPressedEvent, OnViewportGridPointerPressed, RoutingStrategies.Tunnel, true);
            }
        }

        var viewColumnsGrid = this.FindControl<ViewportGridControl>("ViewColumnsViewportGrid");
        if (!ReferenceEquals(_viewColumnsGrid, viewColumnsGrid))
        {
            if (_viewColumnsGrid != null)
            {
                _viewColumnsGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
                _viewColumnsGrid.SelectionChanged -= OnColumnsViewportSelectionChanged;
                _viewColumnsGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            }

            _viewColumnsGrid = viewColumnsGrid;
            if (_viewColumnsGrid != null)
            {
                _viewColumnsGrid.ScrollInvalidated += OnViewportScrollInvalidated;
                _viewColumnsGrid.SelectionChanged += OnColumnsViewportSelectionChanged;
                _viewColumnsGrid.AddHandler(PointerPressedEvent, OnViewportGridPointerPressed, RoutingStrategies.Tunnel, true);
            }
        }

        var indexesGrid = this.FindControl<ViewportGridControl>("IndexesViewportGrid");
        if (!ReferenceEquals(_indexesGrid, indexesGrid))
        {
            if (_indexesGrid != null)
            {
                _indexesGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
                _indexesGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            }

            _indexesGrid = indexesGrid;
            if (_indexesGrid != null)
            {
                _indexesGrid.ScrollInvalidated += OnViewportScrollInvalidated;
                _indexesGrid.AddHandler(PointerPressedEvent, OnViewportGridPointerPressed, RoutingStrategies.Tunnel, true);
            }
        }

        var viewTablesGrid = this.FindControl<ViewportGridControl>("ViewTablesViewportGrid");
        if (!ReferenceEquals(_viewTablesGrid, viewTablesGrid))
        {
            if (_viewTablesGrid != null)
            {
                _viewTablesGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
                _viewTablesGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            }

            _viewTablesGrid = viewTablesGrid;
            if (_viewTablesGrid != null)
            {
                _viewTablesGrid.ScrollInvalidated += OnViewportScrollInvalidated;
                _viewTablesGrid.AddHandler(PointerPressedEvent, OnViewportGridPointerPressed, RoutingStrategies.Tunnel, true);
            }
        }

        var viewJoinsGrid = this.FindControl<ViewportGridControl>("ViewJoinsViewportGrid");
        if (!ReferenceEquals(_viewJoinsGrid, viewJoinsGrid))
        {
            if (_viewJoinsGrid != null)
            {
                _viewJoinsGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
                _viewJoinsGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            }

            _viewJoinsGrid = viewJoinsGrid;
            if (_viewJoinsGrid != null)
            {
                _viewJoinsGrid.ScrollInvalidated += OnViewportScrollInvalidated;
                _viewJoinsGrid.AddHandler(PointerPressedEvent, OnViewportGridPointerPressed, RoutingStrategies.Tunnel, true);
            }
        }

        _columnsHeaderItems = this.FindControl<ItemsControl>("ColumnsHeaderItems");
        _columnsHeaderTransform = EnsureHeaderTransform(_columnsHeaderItems);

        _viewColumnsHeaderItems = this.FindControl<ItemsControl>("ViewColumnsHeaderItems");
        _viewColumnsHeaderTransform = EnsureHeaderTransform(_viewColumnsHeaderItems);

        _indexesHeaderItems = this.FindControl<ItemsControl>("IndexesHeaderItems");
        _indexesHeaderTransform = EnsureHeaderTransform(_indexesHeaderItems);

        _viewTablesHeaderItems = this.FindControl<ItemsControl>("ViewTablesHeaderItems");
        _viewTablesHeaderTransform = EnsureHeaderTransform(_viewTablesHeaderItems);

        _viewJoinsHeaderItems = this.FindControl<ItemsControl>("ViewJoinsHeaderItems");
        _viewJoinsHeaderTransform = EnsureHeaderTransform(_viewJoinsHeaderItems);

        _columnsResizeGuideHost = this.FindControl<Canvas>("ColumnsResizeGuideHost");
        _columnsResizeGuideLine = this.FindControl<Border>("ColumnsResizeGuideLine");
        _indexesResizeGuideHost = this.FindControl<Canvas>("IndexesResizeGuideHost");
        _indexesResizeGuideLine = this.FindControl<Border>("IndexesResizeGuideLine");
        _viewColumnsResizeGuideHost = this.FindControl<Canvas>("ViewColumnsResizeGuideHost");
        _viewColumnsResizeGuideLine = this.FindControl<Border>("ViewColumnsResizeGuideLine");
        _viewTablesResizeGuideHost = this.FindControl<Canvas>("ViewTablesResizeGuideHost");
        _viewTablesResizeGuideLine = this.FindControl<Border>("ViewTablesResizeGuideLine");
        _viewJoinsResizeGuideHost = this.FindControl<Canvas>("ViewJoinsResizeGuideHost");
        _viewJoinsResizeGuideLine = this.FindControl<Border>("ViewJoinsResizeGuideLine");

        UpdateHeaderScrollOffsets();
    }

    private void DetachViewportGrids()
    {
        if (_columnsGrid != null)
        {
            _columnsGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
            _columnsGrid.SelectionChanged -= OnColumnsViewportSelectionChanged;
            _columnsGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            _columnsGrid = null;
        }

        if (_viewColumnsGrid != null)
        {
            _viewColumnsGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
            _viewColumnsGrid.SelectionChanged -= OnColumnsViewportSelectionChanged;
            _viewColumnsGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            _viewColumnsGrid = null;
        }

        if (_indexesGrid != null)
        {
            _indexesGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
            _indexesGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            _indexesGrid = null;
        }

        if (_viewTablesGrid != null)
        {
            _viewTablesGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
            _viewTablesGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            _viewTablesGrid = null;
        }

        if (_viewJoinsGrid != null)
        {
            _viewJoinsGrid.ScrollInvalidated -= OnViewportScrollInvalidated;
            _viewJoinsGrid.RemoveHandler(PointerPressedEvent, OnViewportGridPointerPressed);
            _viewJoinsGrid = null;
        }

        _columnsHeaderItems = null;
        _viewColumnsHeaderItems = null;
        _indexesHeaderItems = null;
        _viewTablesHeaderItems = null;
        _viewJoinsHeaderItems = null;
        _columnsHeaderTransform = null;
        _viewColumnsHeaderTransform = null;
        _indexesHeaderTransform = null;
        _viewTablesHeaderTransform = null;
        _viewJoinsHeaderTransform = null;
        _columnsResizeGuideHost = null;
        _columnsResizeGuideLine = null;
        _indexesResizeGuideHost = null;
        _indexesResizeGuideLine = null;
        _viewColumnsResizeGuideHost = null;
        _viewColumnsResizeGuideLine = null;
        _viewTablesResizeGuideHost = null;
        _viewTablesResizeGuideLine = null;
        _viewJoinsResizeGuideHost = null;
        _viewJoinsResizeGuideLine = null;
        _resizeHeaderControl = null;
    }

    private static TranslateTransform? EnsureHeaderTransform(ItemsControl? itemsControl)
    {
        if (itemsControl == null)
        {
            return null;
        }

        if (itemsControl.RenderTransform is TranslateTransform transform)
        {
            return transform;
        }

        transform = new TranslateTransform();
        itemsControl.RenderTransform = transform;
        return transform;
    }

    private void OnViewportScrollInvalidated(object? sender, EventArgs e)
    {
        UpdateHeaderScrollOffsets();
    }

    private void UpdateHeaderScrollOffsets()
    {
        if (_columnsGrid != null && _columnsHeaderTransform != null)
        {
            _columnsHeaderTransform.X = -_columnsGrid.Offset.X;
        }

        if (_viewColumnsGrid != null && _viewColumnsHeaderTransform != null)
        {
            _viewColumnsHeaderTransform.X = -_viewColumnsGrid.Offset.X;
        }

        if (_indexesGrid != null && _indexesHeaderTransform != null)
        {
            _indexesHeaderTransform.X = -_indexesGrid.Offset.X;
        }

        if (_viewTablesGrid != null && _viewTablesHeaderTransform != null)
        {
            _viewTablesHeaderTransform.X = -_viewTablesGrid.Offset.X;
        }

        if (_viewJoinsGrid != null && _viewJoinsHeaderTransform != null)
        {
            _viewJoinsHeaderTransform.X = -_viewJoinsGrid.Offset.X;
        }
    }

    private void OnColumnsViewportSelectionChanged(object? sender, ViewportGridSelectionChangedEventArgs e)
    {
        if (_viewModel == null || _ddInfoService == null)
        {
            return;
        }

        if (e.RowIndex < 0 || e.RowIndex >= _viewModel.Columns.Count)
        {
            _ddInfoService.Clear();
            return;
        }

        if (_viewModel.Columns[e.RowIndex] is not SpecColumnDisplay column)
        {
            _ddInfoService.Clear();
            return;
        }

        string dataItem = !string.IsNullOrWhiteSpace(column.Alias) ? column.Alias : column.Name;
        if (string.IsNullOrWhiteSpace(dataItem))
        {
            _ddInfoService.Clear();
            return;
        }

        _ = _ddInfoService.ShowForDataItemAsync(dataItem, column.Name, "Specs");
    }

    private void OnViewportGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ViewportGridControl grid)
        {
            return;
        }

        var point = e.GetCurrentPoint(grid);
        if (!point.Properties.IsRightButtonPressed)
        {
            return;
        }

        var contextMenu = grid.ContextMenu;
        if (contextMenu == null)
        {
            return;
        }

        contextMenu.Open(grid);
        e.Handled = true;
    }

    private void OnViewportCopyMenuClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
        {
            return;
        }

        if (menuItem.Parent is ContextMenu menu && menu.PlacementTarget is ViewportGridControl grid)
        {
            grid.CopySelectionToClipboard();
            return;
        }

        _columnsGrid?.CopySelectionToClipboard();
    }

    private void OnSpecsHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel == null || sender is not Control headerControl)
        {
            return;
        }

        var point = e.GetCurrentPoint(headerControl);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (headerControl.DataContext is not ColumnMetadata column)
        {
            return;
        }

        if (!TryResolveResizeTarget(headerControl, out var target))
        {
            return;
        }

        var localPoint = e.GetPosition(headerControl);
        if (!IsNearRightEdge(headerControl, localPoint))
        {
            return;
        }

        _resizeTarget = target;
        _resizeColumnId = column.Id;
        _resizeStartX = localPoint.X;
        _resizeStartWidth = column.Width;
        _resizeCurrentWidth = column.Width;
        _isHeaderResizing = true;
        _resizeViaPointer = true;
        _resizeHeaderControl = headerControl;
        ShowResizeGuide(headerControl, _resizeCurrentWidth);
        headerControl.Cursor = new Cursor(StandardCursorType.SizeWestEast);
        e.Pointer.Capture(headerControl);
        e.Handled = true;
    }

    private void OnSpecsHeaderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel == null || sender is not Control headerControl)
        {
            return;
        }

        var localPoint = e.GetPosition(headerControl);
        if (_isHeaderResizing && _resizeViaPointer && !string.IsNullOrWhiteSpace(_resizeColumnId))
        {
            double delta = localPoint.X - _resizeStartX;
            double width = Math.Max(MinHeaderWidth, _resizeStartWidth + delta);
            _resizeCurrentWidth = width;
            ShowResizeGuide(_resizeHeaderControl ?? headerControl, _resizeCurrentWidth);
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

    private void OnSpecsHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isHeaderResizing)
        {
            return;
        }

        if (_viewModel != null && !string.IsNullOrWhiteSpace(_resizeColumnId))
        {
            switch (_resizeTarget)
            {
                case ResizeTarget.Columns:
                    _viewModel.SetColumnsColumnWidth(_resizeColumnId, _resizeCurrentWidth);
                    break;
                case ResizeTarget.Indexes:
                    _viewModel.SetIndexesColumnWidth(_resizeColumnId, _resizeCurrentWidth);
                    break;
                case ResizeTarget.ViewTables:
                    _viewModel.SetViewTablesColumnWidth(_resizeColumnId, _resizeCurrentWidth);
                    break;
                case ResizeTarget.ViewJoins:
                    _viewModel.SetViewJoinsColumnWidth(_resizeColumnId, _resizeCurrentWidth);
                    break;
            }
        }

        _isHeaderResizing = false;
        _resizeViaPointer = false;
        _resizeColumnId = null;
        _resizeTarget = ResizeTarget.None;
        _resizeHeaderControl = null;
        HideResizeGuide();
        if (e.Pointer.Captured is not null)
        {
            e.Pointer.Capture(null);
        }
        e.Handled = true;
    }

    private void OnSpecsHeaderResizeStarted(object? sender, VectorEventArgs e)
    {
        if (_viewModel == null || sender is not Thumb thumb)
        {
            return;
        }

        if (thumb.DataContext is not ColumnMetadata column)
        {
            return;
        }

        if (!TryResolveResizeTarget(thumb, out var target))
        {
            return;
        }

        _resizeTarget = target;
        _resizeColumnId = column.Id;
        _resizeCurrentWidth = column.Width;
        _isHeaderResizing = true;
        _resizeViaPointer = false;
        _resizeHeaderControl = thumb.FindAncestorOfType<Border>() ?? thumb.FindAncestorOfType<Control>();
        if (_resizeHeaderControl != null)
        {
            ShowResizeGuide(_resizeHeaderControl, _resizeCurrentWidth);
        }
        e.Handled = true;
    }

    private void OnSpecsHeaderResizeDelta(object? sender, VectorEventArgs e)
    {
        if (!_isHeaderResizing || _viewModel == null || string.IsNullOrWhiteSpace(_resizeColumnId))
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

    private void OnSpecsHeaderResizeCompleted(object? sender, VectorEventArgs e)
    {
        if (!_isHeaderResizing)
        {
            return;
        }

        if (_viewModel != null && !string.IsNullOrWhiteSpace(_resizeColumnId))
        {
            switch (_resizeTarget)
            {
                case ResizeTarget.Columns:
                    _viewModel.SetColumnsColumnWidth(_resizeColumnId, _resizeCurrentWidth);
                    break;
                case ResizeTarget.Indexes:
                    _viewModel.SetIndexesColumnWidth(_resizeColumnId, _resizeCurrentWidth);
                    break;
                case ResizeTarget.ViewTables:
                    _viewModel.SetViewTablesColumnWidth(_resizeColumnId, _resizeCurrentWidth);
                    break;
                case ResizeTarget.ViewJoins:
                    _viewModel.SetViewJoinsColumnWidth(_resizeColumnId, _resizeCurrentWidth);
                    break;
            }
        }

        _isHeaderResizing = false;
        _resizeViaPointer = false;
        _resizeColumnId = null;
        _resizeTarget = ResizeTarget.None;
        _resizeHeaderControl = null;
        HideResizeGuide();
        e.Handled = true;
    }

    private void ShowResizeGuide(Control headerControl, double width)
    {
        if (!TryGetResizeGuideElements(headerControl, _resizeTarget, out var host, out var line))
        {
            return;
        }

        var origin = headerControl.TranslatePoint(new Point(0, 0), host);
        if (origin == null)
        {
            return;
        }

        double x = origin.Value.X + width;
        if (x < 0)
        {
            x = 0;
        }

        Canvas.SetLeft(line, x - 0.5);
        Canvas.SetTop(line, 0);
        double height = host.Bounds.Height;
        if (height <= 0 && host.Parent is Control parent)
        {
            height = parent.Bounds.Height;
        }
        if (height > 0)
        {
            line.Height = height;
        }
        line.IsVisible = true;
    }

    private bool TryGetResizeGuideElements(Control headerControl, ResizeTarget target, out Canvas? host, out Border? line)
    {
        host = null;
        line = null;

        switch (target)
        {
            case ResizeTarget.Columns:
                if (headerControl.FindAncestorOfType<ItemsControl>() == _viewColumnsHeaderItems)
                {
                    host = _viewColumnsResizeGuideHost;
                    line = _viewColumnsResizeGuideLine;
                }
                else
                {
                    host = _columnsResizeGuideHost;
                    line = _columnsResizeGuideLine;
                }
                break;
            case ResizeTarget.Indexes:
                host = _indexesResizeGuideHost;
                line = _indexesResizeGuideLine;
                break;
            case ResizeTarget.ViewTables:
                host = _viewTablesResizeGuideHost;
                line = _viewTablesResizeGuideLine;
                break;
            case ResizeTarget.ViewJoins:
                host = _viewJoinsResizeGuideHost;
                line = _viewJoinsResizeGuideLine;
                break;
            case ResizeTarget.None:
            default:
                host = null;
                line = null;
                break;
        }

        return host != null && line != null;
    }

    private void HideResizeGuide()
    {
        HideResizeGuideLine(_columnsResizeGuideLine);
        HideResizeGuideLine(_indexesResizeGuideLine);
        HideResizeGuideLine(_viewColumnsResizeGuideLine);
        HideResizeGuideLine(_viewTablesResizeGuideLine);
        HideResizeGuideLine(_viewJoinsResizeGuideLine);
    }

    private static void HideResizeGuideLine(Border? line)
    {
        if (line != null)
        {
            line.IsVisible = false;
        }
    }

    private bool TryResolveResizeTarget(Control headerControl, out ResizeTarget target)
    {
        target = ResizeTarget.None;
        var itemsControl = headerControl.FindAncestorOfType<ItemsControl>();
        if (itemsControl == null)
        {
            return false;
        }

        if (ReferenceEquals(itemsControl, _columnsHeaderItems) || ReferenceEquals(itemsControl, _viewColumnsHeaderItems))
        {
            target = ResizeTarget.Columns;
            return true;
        }

        if (ReferenceEquals(itemsControl, _indexesHeaderItems))
        {
            target = ResizeTarget.Indexes;
            return true;
        }

        if (ReferenceEquals(itemsControl, _viewTablesHeaderItems))
        {
            target = ResizeTarget.ViewTables;
            return true;
        }

        if (ReferenceEquals(itemsControl, _viewJoinsHeaderItems))
        {
            target = ResizeTarget.ViewJoins;
            return true;
        }

        return false;
    }

    private static bool IsNearRightEdge(Control headerControl, Point localPoint)
    {
        if (headerControl.Bounds.Width <= 0)
        {
            return false;
        }

        return headerControl.Bounds.Width - localPoint.X <= ResizeGripSize;
    }

    private enum ResizeTarget
    {
        None,
        Columns,
        Indexes,
        ViewTables,
        ViewJoins
    }
}
