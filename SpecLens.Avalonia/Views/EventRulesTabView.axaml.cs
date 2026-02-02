using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using Avalonia.ReactiveUI;
using ReactiveUI;
using SpecLens.Avalonia.Services;
using SpecLens.Avalonia.ViewModels;

namespace SpecLens.Avalonia.Views;

public partial class EventRulesTabView : ReactiveUserControl<EventRulesTabViewModel>
{
    private EventRulesTabViewModel? _viewModel;
    private readonly EventRulesLineNumberMargin _lineNumberMargin = new();
    private readonly EventRulesFoldingStrategy _foldingStrategy = new();
    private FoldingManager? _foldingManager;
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);
    private static readonly Cursor TextCursor = new(StandardCursorType.Ibeam);

    public EventRulesTabView()
    {
        InitializeComponent();
        EventRulesEditor.TextArea.TextView.LineTransformers.Add(new EventRulesSyntaxHighlighter());
        EventRulesEditor.TextArea.TextView.PointerPressed += OnEditorPointerPressed;
        EventRulesEditor.TextArea.TextView.PointerMoved += OnEditorPointerMoved;
        EventRulesEditor.TextArea.TextView.PointerExited += OnEditorPointerExited;
        EventRulesEditor.ShowLineNumbers = false;
        _foldingManager = FoldingManager.Install(EventRulesEditor.TextArea);
        InsertLineNumberMargins();
        ApplySyntaxTheme();
        EventRulesSyntaxTheme.ThemeChanged += OnSyntaxThemeChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        this.WhenActivated(disposables =>
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                UpdateEditorText(string.Empty);
                UpdateTreeItemsSource(null);
                return;
            }

            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Disposable.Create(() => _viewModel.PropertyChanged -= OnViewModelPropertyChanged)
                .DisposeWith(disposables);

            UpdateEditorText(_viewModel.DocumentText);
            UpdateTreeItemsSource(_viewModel.Nodes);

            Disposable.Create(() =>
            {
                _viewModel = null;
            }).DisposeWith(disposables);
        });
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (e.PropertyName == nameof(EventRulesTabViewModel.DocumentText))
        {
            UpdateEditorText(_viewModel.DocumentText);
        }
        else if (e.PropertyName == nameof(EventRulesTabViewModel.Nodes))
        {
            UpdateTreeItemsSource(_viewModel.Nodes);
        }
    }

    private void UpdateEditorText(string? text)
    {
        string value = text ?? string.Empty;
        if (Dispatcher.UIThread.CheckAccess())
        {
            EventRulesEditor.Text = value;
            RefreshEditorOverlays();
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                EventRulesEditor.Text = value;
                RefreshEditorOverlays();
            });
        }
    }

    private void UpdateTreeItemsSource(IEnumerable? items)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            EventRulesTree.ItemsSource = items;
            return;
        }

        Dispatcher.UIThread.Post(() => EventRulesTree.ItemsSource = items);
    }

    private void RefreshEditorOverlays()
    {
        _lineNumberMargin.RebuildLineNumberMap();
        _foldingStrategy.UpdateFoldings(_foldingManager, EventRulesEditor.Document);
    }

    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (EventRulesEditor.Document == null)
        {
            return;
        }

        var textView = EventRulesEditor.TextArea.TextView;
        if (!e.GetCurrentPoint(textView).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var link = GetLinkAtPointer(textView, e);
        if (link == null)
        {
            return;
        }

        var mainViewModel = FindMainWindowViewModel();
        if (mainViewModel == null)
        {
            return;
        }

        if (link.Type == EventRulesLinkType.Table)
        {
            _ = mainViewModel.OpenSpecsForObjectNameAsync(link.ObjectName);

            return;
        }

        if (link.Type == EventRulesLinkType.BusinessFunction &&
            link.ObjectName.StartsWith("N", StringComparison.OrdinalIgnoreCase))
        {
            _ = mainViewModel.OpenEventRulesForFunctionAsync(link.ObjectName, link.MemberName ?? string.Empty);
        }
    }

    private void OnEditorPointerMoved(object? sender, PointerEventArgs e)
    {
        if (EventRulesEditor.Document == null)
        {
            return;
        }

        var textView = EventRulesEditor.TextArea.TextView;
        var link = GetLinkAtPointer(textView, e);
        textView.Cursor = link == null ? TextCursor : HandCursor;
    }

    private void OnEditorPointerExited(object? sender, PointerEventArgs e)
    {
        EventRulesEditor.TextArea.TextView.Cursor = TextCursor;
    }

    private void OnSyntaxThemeChanged(object? sender, EventArgs e)
    {
        ApplySyntaxTheme();
        ForceRecolorize(EventRulesEditor.TextArea.TextView);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        EventRulesSyntaxTheme.ThemeChanged -= OnSyntaxThemeChanged;
        DetachedFromVisualTree -= OnDetachedFromVisualTree;
    }

    private void ApplySyntaxTheme()
    {
        EventRulesEditor.Background = EventRulesSyntaxTheme.EditorBackgroundBrush;
        EventRulesEditor.Foreground = EventRulesSyntaxTheme.DefaultTextBrush;
    }

    private void ForceRecolorize(TextView textView)
    {
        if (TryInvokeTextViewMethod(textView, "Redraw"))
        {
            return;
        }

        if (TryInvokeTextViewMethod(textView, "InvalidateVisualLines"))
        {
            return;
        }

        textView.InvalidateMeasure();
        textView.InvalidateVisual();
    }

    private static bool TryInvokeTextViewMethod(TextView textView, string methodName)
    {
        var methods = textView.GetType()
            .GetMethods(System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .Where(method => method.GetParameters().Length == 0)
            .ToArray();
        if (methods.Length == 0)
        {
            return false;
        }

        var method = methods.FirstOrDefault(candidate => candidate.ReturnType == typeof(void))
                     ?? methods.First();
        if (method == null)
        {
            return false;
        }

        method.Invoke(textView, Array.Empty<object?>());
        return true;
    }

    private void InsertLineNumberMargins()
    {
        var margins = EventRulesEditor.TextArea.LeftMargins;
        for (int i = margins.Count - 1; i >= 0; i--)
        {
            if (margins[i] is LineNumberMargin)
            {
                margins.RemoveAt(i);
            }
        }

        margins.Insert(0, _lineNumberMargin);
    }

    private MainWindowViewModel? FindMainWindowViewModel()
    {
        var root = this.FindAncestorOfType<Window>();
        return root?.DataContext as MainWindowViewModel;
    }

    private EventRulesLink? GetLinkAtPointer(TextView textView, PointerEventArgs e)
    {
        if (EventRulesEditor.Document == null)
        {
            return null;
        }

        var viewPoint = e.GetPosition(textView);
        var docPoint = new Point(
            viewPoint.X + textView.ScrollOffset.X,
            viewPoint.Y + textView.ScrollOffset.Y);
        if (!TryGetDocumentOffset(textView, docPoint, out int offset))
        {
            return null;
        }

        var docLine = EventRulesEditor.Document.GetLineByOffset(offset);
        string text = EventRulesEditor.Document.GetText(docLine);
        return EventRulesTextClassifier.FindLinkAtOffset(text, docLine.Offset, offset);
    }

    private bool TryGetDocumentOffset(TextView textView, Point point, out int offset)
    {
        offset = -1;
        if (EventRulesEditor.Document == null)
        {
            return false;
        }

        textView.EnsureVisualLines();

        object? position = TryGetPositionFromPoint(textView, point);
        if (position == null)
        {
            return false;
        }

        if (!TryGetLineColumn(position, out int line, out int column))
        {
            return false;
        }

        if (line <= 0 || column <= 0)
        {
            return false;
        }

        offset = EventRulesEditor.Document.GetOffset(line, column);
        return true;
    }

    private static object? TryGetPositionFromPoint(TextView textView, Point point)
    {
        var methods = textView.GetType().GetMethods()
            .Where(method => string.Equals(method.Name, "GetPositionFromPoint", StringComparison.Ordinal));
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Point))
            {
                return method.Invoke(textView, new object?[] { point });
            }

            if (parameters.Length == 2 && parameters[0].ParameterType == typeof(Point))
            {
                return method.Invoke(textView, new object?[] { point, true });
            }
        }

        methods = textView.GetType().GetMethods()
            .Where(method => string.Equals(method.Name, "GetPosition", StringComparison.Ordinal));
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Point))
            {
                return method.Invoke(textView, new object?[] { point });
            }
        }

        return null;
    }

    private static bool TryGetLineColumn(object position, out int line, out int column)
    {
        line = 0;
        column = 0;

        var type = position.GetType();
        var hasValue = type.GetProperty("HasValue");
        if (hasValue != null)
        {
            bool isSet = (bool)(hasValue.GetValue(position) ?? false);
            if (!isSet)
            {
                return false;
            }

            var value = type.GetProperty("Value")?.GetValue(position);
            if (value == null)
            {
                return false;
            }

            position = value;
            type = position.GetType();
        }

        var lineProp = type.GetProperty("Line");
        var columnProp = type.GetProperty("Column");
        if (lineProp == null || columnProp == null)
        {
            return false;
        }

        line = (int)(lineProp.GetValue(position) ?? 0);
        column = (int)(columnProp.GetValue(position) ?? 0);
        return true;
    }
}
