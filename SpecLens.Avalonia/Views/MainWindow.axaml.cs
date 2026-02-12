using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using ReactiveUI;
using ReactiveUI.Avalonia;
using RoutedEventArgs = global::Avalonia.Interactivity.RoutedEventArgs;
using SpecLens.Avalonia.Settings;
using SpecLens.Avalonia.ViewModels;

namespace SpecLens.Avalonia.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    private const int MaximizedBottomInsetPixels = 1;
    private SettingsWindow? _settingsWindow;

    public MainWindow()
    {
        InitializeComponent();
        this.WhenActivated(_ => { });
        ConfigureResizeCursors();
        PropertyChanged += OnMainWindowPropertyChanged;
        UpdateResizeOverlayState();
        Loaded += OnMainWindowLoaded;
    }

    private void OnMainWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WindowStateProperty)
        {
            OnWindowStateChanged();
        }
    }

    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (((ICommand)viewModel.SearchCommand).CanExecute(null))
        {
            ((ICommand)viewModel.SearchCommand).Execute(null);
            e.Handled = true;
        }
    }

    private void OnResultsGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsRightButtonPressed)
        {
            return;
        }

        if (e.Source is Control source && source.FindAncestorOfType<DataGridRow>() is DataGridRow row)
        {
            row.IsSelected = true;
        }
    }

    private void OnResultsGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var selected = viewModel.SelectedObject;
        if (((ICommand)viewModel.DefaultOpenCommand).CanExecute(selected))
        {
            ((ICommand)viewModel.DefaultOpenCommand).Execute(selected);
        }
    }

    private void OnOpenSpecsMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (TryGetSelectedObject(out var selected) && ((ICommand)viewModel.OpenSpecsForObjectCommand).CanExecute(selected))
        {
            ((ICommand)viewModel.OpenSpecsForObjectCommand).Execute(selected);
        }
    }

    private void OnOpenQueryMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (TryGetSelectedObject(out var selected) && ((ICommand)viewModel.OpenQueryForObjectCommand).CanExecute(selected))
        {
            ((ICommand)viewModel.OpenQueryForObjectCommand).Execute(selected);
        }
    }

    private void OnOpenEventRulesMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (TryGetSelectedObject(out var selected) && ((ICommand)viewModel.OpenEventRulesForObjectCommand).CanExecute(selected))
        {
            ((ICommand)viewModel.OpenEventRulesForObjectCommand).Execute(selected);
        }
    }

    private void OnCloseTabMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (sender is MenuItem menuItem && menuItem.DataContext is WorkspaceTabViewModel tab)
        {
            if (((ICommand)viewModel.CloseTabCommand).CanExecute(tab))
            {
                ((ICommand)viewModel.CloseTabCommand).Execute(tab);
            }
        }
    }

    private void OnCloseAllTabsMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (((ICommand)viewModel.CloseAllTabsCommand).CanExecute(null))
        {
            ((ICommand)viewModel.CloseAllTabsCommand).Execute(null);
        }
    }

    private bool TryGetSelectedObject(out JdeClient.Core.Models.JdeObjectInfo? selected)
    {
        selected = null;
        if (this.FindControl<DataGrid>("ResultsGrid") is DataGrid grid)
        {
            selected = grid.SelectedItem as JdeClient.Core.Models.JdeObjectInfo;
            return selected != null;
        }

        return false;
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }

        var settingsService = ViewModel?.SettingsService;
        if (settingsService == null)
        {
            return;
        }

        _settingsWindow = new SettingsWindow(settingsService);
        _settingsWindow.Closed += (_, __) => _settingsWindow = null;
        _settingsWindow.Show(this);
    }

    private void OnSearchSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var settingsService = ViewModel?.SettingsService;
        if (settingsService == null)
        {
            return;
        }

        if (this.FindControl<Grid>("MainLayoutGrid") is Grid grid && grid.ColumnDefinitions.Count > 0)
        {
            settingsService.Current.SearchPaneWidth = grid.ColumnDefinitions[0].ActualWidth;
            settingsService.Save();
        }
    }

    private void OnMainWindowLoaded(object? sender, RoutedEventArgs e)
    {
        var settingsService = ViewModel?.SettingsService;
        if (settingsService == null)
        {
            return;
        }

        if (settingsService.Current.SearchPaneWidth > 0 &&
            this.FindControl<Grid>("MainLayoutGrid") is Grid grid &&
            grid.ColumnDefinitions.Count > 0)
        {
            grid.ColumnDefinitions[0].Width = new GridLength(settingsService.Current.SearchPaneWidth);
        }
    }

    private void ConfigureResizeCursors()
    {
        SetResizeCursor("ResizeNorthWest", StandardCursorType.TopLeftCorner);
        SetResizeCursor("ResizeNorth", StandardCursorType.TopSide);
        SetResizeCursor("ResizeNorthEast", StandardCursorType.TopRightCorner);
        SetResizeCursor("ResizeWest", StandardCursorType.LeftSide);
        SetResizeCursor("ResizeEast", StandardCursorType.RightSide);
        SetResizeCursor("ResizeSouthWest", StandardCursorType.BottomLeftCorner);
        SetResizeCursor("ResizeSouth", StandardCursorType.BottomSide);
        SetResizeCursor("ResizeSouthEast", StandardCursorType.BottomRightCorner);
    }

    private void SetResizeCursor(string name, StandardCursorType cursorType)
    {
        if (this.FindControl<Control>(name) is { } control)
        {
            control.Cursor = new Cursor(cursorType);
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount > 1)
            {
                return;
            }

            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        ToggleMaximize();
    }

    private void OnResizePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (sender is Control control &&
            control.Tag is string tag &&
            Enum.TryParse(tag, true, out WindowEdge edge))
        {
            BeginResizeDrag(edge, e);
        }
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void OnCloseWindowClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateResizeOverlayState()
    {
        if (this.FindControl<Grid>("ResizeOverlay") is not { } overlay)
        {
            return;
        }

        bool enableOverlay = WindowState != WindowState.Maximized;
        overlay.IsVisible = enableOverlay;
        overlay.IsHitTestVisible = enableOverlay;
    }

    private void OnWindowStateChanged()
    {
        UpdateResizeOverlayState();
        UpdateMaximizedBoundsConstraint();
    }

    private void ToggleMaximize()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            return;
        }

        ApplyCurrentScreenMaximizeConstraint();
        WindowState = WindowState.Maximized;
    }

    private void UpdateMaximizedBoundsConstraint()
    {
        if (WindowState == WindowState.Maximized)
        {
            ApplyCurrentScreenMaximizeConstraint();
            return;
        }

        MaxWidth = double.PositiveInfinity;
        MaxHeight = double.PositiveInfinity;
    }

    private void ApplyCurrentScreenMaximizeConstraint()
    {
        var screen = Screens?.ScreenFromWindow(this);
        if (screen == null || screen.Scaling <= 0)
        {
            return;
        }

        double maxWidthDip = screen.WorkingArea.Width / screen.Scaling;
        double maxHeightDip = Math.Max(
            1,
            (screen.WorkingArea.Height - MaximizedBottomInsetPixels) / screen.Scaling);

        MaxWidth = maxWidthDip;
        MaxHeight = maxHeightDip;
    }
}
