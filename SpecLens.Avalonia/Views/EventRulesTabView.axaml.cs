using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using ReactiveUI.Avalonia;
using SpecLens.Avalonia.Services;
using SpecLens.Avalonia.ViewModels;
using TextMateSharp.Grammars;

namespace SpecLens.Avalonia.Views;

public partial class EventRulesTabView : ReactiveUserControl<EventRulesTabViewModel>
{
    private const double MinEditorFontSize = 9;
    private const double MaxEditorFontSize = 36;
    private const double EditorFontStep = 1;
    private EventRulesTabViewModel? _viewModel;
    private readonly IAppSettingsService? _settingsService = App.GetService<IAppSettingsService>();
    private readonly EventRulesLineNumberMargin _lineNumberMargin = new();
    private readonly EventRulesFoldingStrategy _foldingStrategy = new();
    private readonly RegistryOptions _cTextMateRegistry = new(ThemeName.DarkPlus);
    private TextMate.Installation? _headerCTextMateInstallation;
    private TextMate.Installation? _sourceCTextMateInstallation;
    private string? _cSourceScopeName;
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
        EventRulesEditor.TextArea.TextView.PointerWheelChanged += OnEditorPointerWheelChanged;
        HeaderCodeEditor.TextArea.TextView.PointerWheelChanged += OnEditorPointerWheelChanged;
        SourceCodeEditor.TextArea.TextView.PointerWheelChanged += OnEditorPointerWheelChanged;
        EventRulesEditor.ShowLineNumbers = false;
        _foldingManager = FoldingManager.Install(EventRulesEditor.TextArea);
        InsertLineNumberMargins();
        UpdateCodeEditorLayoutHeights();
        ApplySyntaxTheme();
        EventRulesSyntaxTheme.ThemeChanged += OnSyntaxThemeChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        if (_settingsService != null)
        {
            _settingsService.SettingsChanged += OnSettingsChanged;
            ApplyCCodeHighlightingSetting(_settingsService.Current.EnableCCodeSyntaxHighlighting);
        }

        this.WhenActivated(disposables =>
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                UpdateEditorText(string.Empty);
                UpdateHeaderEditorText(string.Empty);
                UpdateSourceEditorText(string.Empty);
                UpdateTreeItemsSource(null);
                return;
            }

            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            Disposable.Create(() => _viewModel.PropertyChanged -= OnViewModelPropertyChanged)
                .DisposeWith(disposables);

            UpdateEditorText(_viewModel.DocumentText);
            UpdateHeaderEditorText(_viewModel.HeaderCodeText);
            UpdateSourceEditorText(_viewModel.SourceCodeText);
            ScrollSourceToFunction(_viewModel.SourceScrollTarget);
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
        else if (e.PropertyName == nameof(EventRulesTabViewModel.HeaderCodeText))
        {
            UpdateHeaderEditorText(_viewModel.HeaderCodeText);
        }
        else if (e.PropertyName == nameof(EventRulesTabViewModel.SourceCodeText))
        {
            UpdateSourceEditorText(_viewModel.SourceCodeText);
        }
        else if (e.PropertyName == nameof(EventRulesTabViewModel.SourceScrollTarget))
        {
            ScrollSourceToFunction(_viewModel.SourceScrollTarget);
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

    private void UpdateHeaderEditorText(string? text)
    {
        string value = text ?? string.Empty;
        if (Dispatcher.UIThread.CheckAccess())
        {
            HeaderCodeEditor.Text = value;
        }
        else
        {
            Dispatcher.UIThread.Post(() => HeaderCodeEditor.Text = value);
        }
    }

    private void UpdateSourceEditorText(string? text)
    {
        string value = text ?? string.Empty;
        if (Dispatcher.UIThread.CheckAccess())
        {
            SourceCodeEditor.Text = value;
        }
        else
        {
            Dispatcher.UIThread.Post(() => SourceCodeEditor.Text = value);
        }
    }

    private void ScrollSourceToFunction(string? functionName)
    {
        if (string.IsNullOrWhiteSpace(functionName))
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ScrollSourceToFunctionCore(functionName);
            return;
        }

        Dispatcher.UIThread.Post(() => ScrollSourceToFunctionCore(functionName));
    }

    private void ScrollSourceToFunctionCore(string functionName)
    {
        var document = SourceCodeEditor.Document;
        if (document == null || document.TextLength == 0)
        {
            return;
        }

        int offset = FindFunctionOffset(document.Text, functionName);
        if (offset < 0)
        {
            return;
        }

        var location = document.GetLocation(offset);
        SourceCodeEditor.TextArea.Caret.Line = Math.Max(1, location.Line);
        SourceCodeEditor.TextArea.Caret.Column = Math.Max(1, location.Column);
        TryInvokeCaretMethod(SourceCodeEditor.TextArea.Caret, "BringCaretToView");
        ScrollSourceEditorToLine(location.Line);
    }

    private static int FindFunctionOffset(string sourceCode, string functionName)
    {
        if (string.IsNullOrWhiteSpace(sourceCode) || string.IsNullOrWhiteSpace(functionName))
        {
            return -1;
        }

        string escapedFunction = Regex.Escape(functionName.Trim());
        string[] patterns =
        {
            $@"^\s*[A-Za-z_][A-Za-z0-9_\s\*\(\)]*\b{escapedFunction}\s*\(",
            $@"\b{escapedFunction}\s*\(",
            $@"\b{escapedFunction}\b"
        };

        foreach (string pattern in patterns)
        {
            var match = Regex.Match(
                sourceCode,
                pattern,
                RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                return match.Index;
            }
        }

        return -1;
    }

    private void ScrollSourceEditorToLine(int line)
    {
        if (line <= 0)
        {
            return;
        }

        if (TryInvokeOneArgMethod(SourceCodeEditor, "ScrollToLine", line))
        {
            return;
        }

        if (TryInvokeTwoArgMethod(SourceCodeEditor, "ScrollTo", line, 1))
        {
            return;
        }

        TryInvokeTwoArgMethod(SourceCodeEditor.TextArea.TextView, "ScrollTo", line, 1);
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
            _ = mainViewModel.OpenSpecsForObjectNameAsync(link.ObjectName, ViewModel?.PathCode);

            return;
        }

        if (link.Type == EventRulesLinkType.BusinessFunction &&
            link.ObjectName.StartsWith("N", StringComparison.OrdinalIgnoreCase))
        {
            _ = mainViewModel.OpenEventRulesForFunctionAsync(
                link.ObjectName,
                link.MemberName ?? string.Empty,
                ViewModel?.PathCode);
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

    private void OnEditorPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        var editor = ResolveEditorFromTextView(sender);
        if (editor == null)
        {
            return;
        }

        double delta = e.Delta.Y;
        if (Math.Abs(delta) <= double.Epsilon)
        {
            return;
        }

        double newSize = editor.FontSize + (delta > 0 ? EditorFontStep : -EditorFontStep);
        newSize = Math.Clamp(newSize, MinEditorFontSize, MaxEditorFontSize);
        if (Math.Abs(newSize - editor.FontSize) <= double.Epsilon)
        {
            return;
        }

        editor.FontSize = newSize;
        e.Handled = true;
    }

    private TextEditor? ResolveEditorFromTextView(object? sender)
    {
        if (ReferenceEquals(sender, EventRulesEditor.TextArea.TextView))
        {
            return EventRulesEditor;
        }

        if (ReferenceEquals(sender, HeaderCodeEditor.TextArea.TextView))
        {
            return HeaderCodeEditor;
        }

        if (ReferenceEquals(sender, SourceCodeEditor.TextArea.TextView))
        {
            return SourceCodeEditor;
        }

        return null;
    }

    private void OnCodeExpanderToggled(object? sender, RoutedEventArgs e)
    {
        UpdateCodeEditorLayoutHeights();
    }

    private void UpdateCodeEditorLayoutHeights()
    {
        if (CodeEditorsGrid.RowDefinitions.Count < 3)
        {
            return;
        }

        bool isHeaderExpanded = HeaderCodeExpander.IsExpanded;
        bool isSourceExpanded = SourceCodeExpander.IsExpanded;
        bool bothExpanded = isHeaderExpanded && isSourceExpanded;
        CodeEditorsSplitter.IsVisible = bothExpanded;
        if (isHeaderExpanded && isSourceExpanded)
        {
            SetCodeEditorsRowHeights(
                new GridLength(1, GridUnitType.Star),
                new GridLength(6, GridUnitType.Pixel),
                new GridLength(1, GridUnitType.Star));
            return;
        }

        if (isHeaderExpanded)
        {
            SetCodeEditorsRowHeights(
                new GridLength(1, GridUnitType.Star),
                new GridLength(0, GridUnitType.Pixel),
                GridLength.Auto);
            return;
        }

        if (isSourceExpanded)
        {
            SetCodeEditorsRowHeights(
                GridLength.Auto,
                new GridLength(0, GridUnitType.Pixel),
                new GridLength(1, GridUnitType.Star));
            return;
        }

        SetCodeEditorsRowHeights(
            GridLength.Auto,
            new GridLength(0, GridUnitType.Pixel),
            GridLength.Auto);
    }

    private void SetCodeEditorsRowHeights(GridLength headerHeight, GridLength splitterHeight, GridLength sourceHeight)
    {
        CodeEditorsGrid.RowDefinitions[0].Height = headerHeight;
        CodeEditorsGrid.RowDefinitions[1].Height = splitterHeight;
        CodeEditorsGrid.RowDefinitions[2].Height = sourceHeight;
    }

    private void OnSyntaxThemeChanged(object? sender, EventArgs e)
    {
        ApplySyntaxTheme();
        UpdateCTextMateTheme();
        ForceRecolorize(EventRulesEditor.TextArea.TextView);
        ForceRecolorize(HeaderCodeEditor.TextArea.TextView);
        ForceRecolorize(SourceCodeEditor.TextArea.TextView);
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_settingsService == null)
        {
            return;
        }

        ApplyCCodeHighlightingSetting(_settingsService.Current.EnableCCodeSyntaxHighlighting);
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        EventRulesSyntaxTheme.ThemeChanged -= OnSyntaxThemeChanged;
        EventRulesEditor.TextArea.TextView.PointerWheelChanged -= OnEditorPointerWheelChanged;
        HeaderCodeEditor.TextArea.TextView.PointerWheelChanged -= OnEditorPointerWheelChanged;
        SourceCodeEditor.TextArea.TextView.PointerWheelChanged -= OnEditorPointerWheelChanged;
        if (_settingsService != null)
        {
            _settingsService.SettingsChanged -= OnSettingsChanged;
        }

        DisposeCTextMateInstallations();
        DetachedFromVisualTree -= OnDetachedFromVisualTree;
    }

    private void ApplyCCodeHighlightingSetting(bool isEnabled)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ApplyCCodeHighlightingSetting(isEnabled));
            return;
        }

        if (isEnabled)
        {
            EnsureCTextMateInstallations();
            EnsureCGrammarApplied();
            UpdateCTextMateTheme();
        }
        else
        {
            DisposeCTextMateInstallations();
        }

        ForceRecolorize(HeaderCodeEditor.TextArea.TextView);
        ForceRecolorize(SourceCodeEditor.TextArea.TextView);
    }

    private void EnsureCTextMateInstallations()
    {
        _headerCTextMateInstallation ??= HeaderCodeEditor.InstallTextMate(_cTextMateRegistry);
        _sourceCTextMateInstallation ??= SourceCodeEditor.InstallTextMate(_cTextMateRegistry);
    }

    private void DisposeCTextMateInstallations()
    {
        _headerCTextMateInstallation?.Dispose();
        _sourceCTextMateInstallation?.Dispose();
        _headerCTextMateInstallation = null;
        _sourceCTextMateInstallation = null;
    }

    private void EnsureCGrammarApplied()
    {
        string? cScopeName = GetCSourceScopeName();
        if (string.IsNullOrWhiteSpace(cScopeName))
        {
            return;
        }

        _headerCTextMateInstallation?.SetGrammar(cScopeName);
        _sourceCTextMateInstallation?.SetGrammar(cScopeName);
    }

    private string? GetCSourceScopeName()
    {
        if (!string.IsNullOrWhiteSpace(_cSourceScopeName))
        {
            return _cSourceScopeName;
        }

        var cLanguage = _cTextMateRegistry.GetLanguageByExtension(".c");
        if (cLanguage == null)
        {
            return null;
        }

        _cSourceScopeName = _cTextMateRegistry.GetScopeByLanguageId(cLanguage.Id);
        return _cSourceScopeName;
    }

    private void UpdateCTextMateTheme()
    {
        if (_headerCTextMateInstallation == null && _sourceCTextMateInstallation == null)
        {
            return;
        }

        ThemeName themeName = ResolveTextMateThemeName();
        var theme = _cTextMateRegistry.LoadTheme(themeName);
        _headerCTextMateInstallation?.SetTheme(theme);
        _sourceCTextMateInstallation?.SetTheme(theme);
    }

    private ThemeName ResolveTextMateThemeName()
    {
        if (_settingsService?.Current.ThemeMode == SpecLens.Avalonia.Models.AppThemeMode.Dark)
        {
            return ThemeName.DarkPlus;
        }

        return ThemeName.LightPlus;
    }

    private void ApplySyntaxTheme()
    {
        EventRulesEditor.Background = EventRulesSyntaxTheme.EditorBackgroundBrush;
        EventRulesEditor.Foreground = EventRulesSyntaxTheme.DefaultTextBrush;
        HeaderCodeEditor.Background = EventRulesSyntaxTheme.EditorBackgroundBrush;
        HeaderCodeEditor.Foreground = EventRulesSyntaxTheme.DefaultTextBrush;
        SourceCodeEditor.Background = EventRulesSyntaxTheme.EditorBackgroundBrush;
        SourceCodeEditor.Foreground = EventRulesSyntaxTheme.DefaultTextBrush;
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

    private static bool TryInvokeCaretMethod(object caret, string methodName)
    {
        var method = caret.GetType()
            .GetMethods(System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic)
            .Where(candidate => string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
            .Where(candidate => candidate.GetParameters().Length == 0)
            .FirstOrDefault();

        if (method == null)
        {
            return false;
        }

        method.Invoke(caret, Array.Empty<object?>());
        return true;
    }

    private static bool TryInvokeOneArgMethod(object instance, string methodName, int value)
    {
        var methods = instance.GetType()
            .GetMethods(System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .Where(method => method.GetParameters().Length == 1);

        foreach (var method in methods)
        {
            var parameterType = method.GetParameters()[0].ParameterType;
            if (parameterType == typeof(int))
            {
                method.Invoke(instance, new object?[] { value });
                return true;
            }

            if (parameterType == typeof(double))
            {
                method.Invoke(instance, new object?[] { (double)value });
                return true;
            }

            if (parameterType == typeof(float))
            {
                method.Invoke(instance, new object?[] { (float)value });
                return true;
            }
        }

        return false;
    }

    private static bool TryInvokeTwoArgMethod(object instance, string methodName, int first, int second)
    {
        var methods = instance.GetType()
            .GetMethods(System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .Where(method => method.GetParameters().Length == 2);

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters[0].ParameterType == typeof(int) &&
                parameters[1].ParameterType == typeof(int))
            {
                method.Invoke(instance, new object?[] { first, second });
                return true;
            }

            if (parameters[0].ParameterType == typeof(double) &&
                parameters[1].ParameterType == typeof(double))
            {
                method.Invoke(instance, new object?[] { (double)first, (double)second });
                return true;
            }

            if (parameters[0].ParameterType == typeof(float) &&
                parameters[1].ParameterType == typeof(float))
            {
                method.Invoke(instance, new object?[] { (float)first, (float)second });
                return true;
            }
        }

        return false;
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
