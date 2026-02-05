using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia.Media;
using ReactiveUI;
using SpecLens.Avalonia.Models;
using SpecLens.Avalonia.Services;
using SpecLens.Avalonia.ViewModels;

namespace SpecLens.Avalonia.Settings;

public sealed class SettingsViewModel : ViewModelBase, IActivatableViewModel
{
    private readonly IAppSettingsService _settingsService;
    private ColumnHeaderDisplayMode _columnHeaderDisplayMode;
    private bool _showTablePrefixInHeader;
    private double _queryColumnWidth;
    private AppThemeMode _themeMode;
    private bool _isLoggingEnabled;
    private string _loggingPath = string.Empty;
    private bool _isClientLoggingEnabled;
    private string _clientLoggingPath = string.Empty;
    private Color _eventRulesCommentColor;
    private Color _eventRulesLinkColor;
    private Color _eventRulesPipeColor;
    private Color _eventRulesInputColor;
    private Color _eventRulesStringColor;
    private Color _eventRulesOutputColor;
    private Color _eventRulesEqualsColor;
    private Color _eventRulesDefaultTextColor;
    private Color _eventRulesEditorBackgroundColor;

    public SettingsViewModel(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
        ThemeMode = settingsService.Current.ThemeMode;
        IsLoggingEnabled = settingsService.Current.IsLoggingEnabled;
        LoggingPath = settingsService.Current.LoggingPath;
        IsClientLoggingEnabled = settingsService.Current.IsClientLoggingEnabled;
        ClientLoggingPath = settingsService.Current.ClientLoggingPath;
        ColumnHeaderDisplayMode = settingsService.Current.ColumnHeaderDisplayMode;
        ShowTablePrefixInHeader = settingsService.Current.ShowTablePrefixInHeader;
        QueryColumnWidth = settingsService.Current.QueryColumnWidth;
        EventRulesCommentColor = EventRulesSyntaxTheme.ParseColor(
            settingsService.Current.EventRulesCommentColor,
            EventRulesSyntaxTheme.DefaultCommentColor);
        EventRulesLinkColor = EventRulesSyntaxTheme.ParseColor(
            settingsService.Current.EventRulesLinkColor,
            EventRulesSyntaxTheme.DefaultLinkColor);
        EventRulesPipeColor = EventRulesSyntaxTheme.ParseColor(
            settingsService.Current.EventRulesPipeColor,
            EventRulesSyntaxTheme.DefaultPipeColor);
        EventRulesInputColor = EventRulesSyntaxTheme.ParseColor(
            settingsService.Current.EventRulesInputColor,
            EventRulesSyntaxTheme.DefaultInputColor);
        EventRulesStringColor = EventRulesSyntaxTheme.ParseColor(
            settingsService.Current.EventRulesStringColor,
            EventRulesSyntaxTheme.DefaultStringColor);
        EventRulesOutputColor = EventRulesSyntaxTheme.ParseColor(
            settingsService.Current.EventRulesOutputColor,
            EventRulesSyntaxTheme.DefaultOutputColor);
        EventRulesEqualsColor = EventRulesSyntaxTheme.ParseColor(
            settingsService.Current.EventRulesEqualsColor,
            EventRulesSyntaxTheme.DefaultEqualsColor);
        EventRulesDefaultTextColor = EventRulesSyntaxTheme.ParseColor(
            settingsService.Current.EventRulesDefaultTextColor,
            EventRulesSyntaxTheme.DefaultTextColor);
        EventRulesEditorBackgroundColor = EventRulesSyntaxTheme.ParseColor(
            settingsService.Current.EventRulesEditorBackgroundColor,
            EventRulesSyntaxTheme.DefaultEditorBackgroundColor);

        SetHeaderDisplayModeCommand = ReactiveCommand.Create<ColumnHeaderDisplayMode>(SetHeaderDisplayMode);
        SaveCommand = ReactiveCommand.Create(SaveSettings);

        this.WhenActivated(disposables =>
        {
            Observable.FromEventPattern<EventHandler, EventArgs>(
                    h => _settingsService.SettingsChanged += h,
                    h => _settingsService.SettingsChanged -= h)
                .Subscribe(_ => OnSettingsChanged())
                .DisposeWith(disposables);
        });
    }

    public ViewModelActivator Activator { get; } = new();

    public ColumnHeaderDisplayMode ColumnHeaderDisplayMode
    {
        get => _columnHeaderDisplayMode;
        set
        {
            if (EqualityComparer<ColumnHeaderDisplayMode>.Default.Equals(_columnHeaderDisplayMode, value))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _columnHeaderDisplayMode, value);
            this.RaisePropertyChanged(nameof(IsHeaderDisplayModeDatabase));
            this.RaisePropertyChanged(nameof(IsHeaderDisplayModeDictionary));
            this.RaisePropertyChanged(nameof(IsHeaderDisplayModeDatabaseWithDescription));
            this.RaisePropertyChanged(nameof(IsHeaderDisplayModeDataDictionaryWithDescription));
        }
    }

    public bool ShowTablePrefixInHeader
    {
        get => _showTablePrefixInHeader;
        set => this.RaiseAndSetIfChanged(ref _showTablePrefixInHeader, value);
    }

    public double QueryColumnWidth
    {
        get => _queryColumnWidth;
        set
        {
            if (value <= 0)
            {
                value = 160;
            }

            this.RaiseAndSetIfChanged(ref _queryColumnWidth, value);
        }
    }

    public AppThemeMode ThemeMode
    {
        get => _themeMode;
        set => this.RaiseAndSetIfChanged(ref _themeMode, value);
    }

    public bool IsLoggingEnabled
    {
        get => _isLoggingEnabled;
        set => this.RaiseAndSetIfChanged(ref _isLoggingEnabled, value);
    }

    public string LoggingPath
    {
        get => _loggingPath;
        set => this.RaiseAndSetIfChanged(ref _loggingPath, value);
    }

    public bool IsClientLoggingEnabled
    {
        get => _isClientLoggingEnabled;
        set => this.RaiseAndSetIfChanged(ref _isClientLoggingEnabled, value);
    }

    public string ClientLoggingPath
    {
        get => _clientLoggingPath;
        set => this.RaiseAndSetIfChanged(ref _clientLoggingPath, value);
    }

    public Color EventRulesCommentColor
    {
        get => _eventRulesCommentColor;
        set => this.RaiseAndSetIfChanged(ref _eventRulesCommentColor, value);
    }

    public Color EventRulesLinkColor
    {
        get => _eventRulesLinkColor;
        set => this.RaiseAndSetIfChanged(ref _eventRulesLinkColor, value);
    }

    public Color EventRulesPipeColor
    {
        get => _eventRulesPipeColor;
        set => this.RaiseAndSetIfChanged(ref _eventRulesPipeColor, value);
    }

    public Color EventRulesInputColor
    {
        get => _eventRulesInputColor;
        set => this.RaiseAndSetIfChanged(ref _eventRulesInputColor, value);
    }

    public Color EventRulesStringColor
    {
        get => _eventRulesStringColor;
        set => this.RaiseAndSetIfChanged(ref _eventRulesStringColor, value);
    }

    public Color EventRulesOutputColor
    {
        get => _eventRulesOutputColor;
        set => this.RaiseAndSetIfChanged(ref _eventRulesOutputColor, value);
    }

    public Color EventRulesEqualsColor
    {
        get => _eventRulesEqualsColor;
        set => this.RaiseAndSetIfChanged(ref _eventRulesEqualsColor, value);
    }

    public Color EventRulesDefaultTextColor
    {
        get => _eventRulesDefaultTextColor;
        set => this.RaiseAndSetIfChanged(ref _eventRulesDefaultTextColor, value);
    }

    public Color EventRulesEditorBackgroundColor
    {
        get => _eventRulesEditorBackgroundColor;
        set => this.RaiseAndSetIfChanged(ref _eventRulesEditorBackgroundColor, value);
    }

    public ReactiveCommand<ColumnHeaderDisplayMode, Unit> SetHeaderDisplayModeCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public IReadOnlyList<AppThemeMode> ThemeModeOptions { get; } =
        new[] { AppThemeMode.Light, AppThemeMode.Dark };

    public bool IsHeaderDisplayModeDatabase => ColumnHeaderDisplayMode == ColumnHeaderDisplayMode.DatabaseColumnName;
    public bool IsHeaderDisplayModeDictionary => ColumnHeaderDisplayMode == ColumnHeaderDisplayMode.DataDictionary;
    public bool IsHeaderDisplayModeDatabaseWithDescription => ColumnHeaderDisplayMode == ColumnHeaderDisplayMode.DatabaseColumnNameWithDescription;

    public bool IsHeaderDisplayModeDataDictionaryWithDescription =>
        ColumnHeaderDisplayMode == ColumnHeaderDisplayMode.DatabaseDataDictionaryWithDescription;

    private void SetHeaderDisplayMode(ColumnHeaderDisplayMode mode)
    {
        ColumnHeaderDisplayMode = mode;
    }

    private void OnSettingsChanged()
    {
        var mode = _settingsService.Current.ColumnHeaderDisplayMode;
        if (ColumnHeaderDisplayMode != mode)
        {
            ColumnHeaderDisplayMode = mode;
        }

        bool showPrefix = _settingsService.Current.ShowTablePrefixInHeader;
        if (ShowTablePrefixInHeader != showPrefix)
        {
            ShowTablePrefixInHeader = showPrefix;
        }

        double columnWidth = _settingsService.Current.QueryColumnWidth;
        if (Math.Abs(QueryColumnWidth - columnWidth) > 0.1)
        {
            QueryColumnWidth = columnWidth;
        }

        var themeMode = _settingsService.Current.ThemeMode;
        if (ThemeMode != themeMode)
        {
            ThemeMode = themeMode;
        }

        bool loggingEnabled = _settingsService.Current.IsLoggingEnabled;
        if (IsLoggingEnabled != loggingEnabled)
        {
            IsLoggingEnabled = loggingEnabled;
        }

        string loggingPath = _settingsService.Current.LoggingPath;
        if (!string.Equals(LoggingPath, loggingPath, StringComparison.OrdinalIgnoreCase))
        {
            LoggingPath = loggingPath;
        }

        bool clientLoggingEnabled = _settingsService.Current.IsClientLoggingEnabled;
        if (IsClientLoggingEnabled != clientLoggingEnabled)
        {
            IsClientLoggingEnabled = clientLoggingEnabled;
        }

        string clientLoggingPath = _settingsService.Current.ClientLoggingPath;
        if (!string.Equals(ClientLoggingPath, clientLoggingPath, StringComparison.OrdinalIgnoreCase))
        {
            ClientLoggingPath = clientLoggingPath;
        }

        var commentColor = EventRulesSyntaxTheme.ParseColor(
            _settingsService.Current.EventRulesCommentColor,
            EventRulesSyntaxTheme.DefaultCommentColor);
        if (EventRulesCommentColor != commentColor)
        {
            EventRulesCommentColor = commentColor;
        }

        var linkColor = EventRulesSyntaxTheme.ParseColor(
            _settingsService.Current.EventRulesLinkColor,
            EventRulesSyntaxTheme.DefaultLinkColor);
        if (EventRulesLinkColor != linkColor)
        {
            EventRulesLinkColor = linkColor;
        }

        var pipeColor = EventRulesSyntaxTheme.ParseColor(
            _settingsService.Current.EventRulesPipeColor,
            EventRulesSyntaxTheme.DefaultPipeColor);
        if (EventRulesPipeColor != pipeColor)
        {
            EventRulesPipeColor = pipeColor;
        }

        var inputColor = EventRulesSyntaxTheme.ParseColor(
            _settingsService.Current.EventRulesInputColor,
            EventRulesSyntaxTheme.DefaultInputColor);
        if (EventRulesInputColor != inputColor)
        {
            EventRulesInputColor = inputColor;
        }

        var stringColor = EventRulesSyntaxTheme.ParseColor(
            _settingsService.Current.EventRulesStringColor,
            EventRulesSyntaxTheme.DefaultStringColor);
        if (EventRulesStringColor != stringColor)
        {
            EventRulesStringColor = stringColor;
        }

        var outputColor = EventRulesSyntaxTheme.ParseColor(
            _settingsService.Current.EventRulesOutputColor,
            EventRulesSyntaxTheme.DefaultOutputColor);
        if (EventRulesOutputColor != outputColor)
        {
            EventRulesOutputColor = outputColor;
        }

        var equalsColor = EventRulesSyntaxTheme.ParseColor(
            _settingsService.Current.EventRulesEqualsColor,
            EventRulesSyntaxTheme.DefaultEqualsColor);
        if (EventRulesEqualsColor != equalsColor)
        {
            EventRulesEqualsColor = equalsColor;
        }

        var defaultTextColor = EventRulesSyntaxTheme.ParseColor(
            _settingsService.Current.EventRulesDefaultTextColor,
            EventRulesSyntaxTheme.DefaultTextColor);
        if (EventRulesDefaultTextColor != defaultTextColor)
        {
            EventRulesDefaultTextColor = defaultTextColor;
        }

        var editorBackgroundColor = EventRulesSyntaxTheme.ParseColor(
            _settingsService.Current.EventRulesEditorBackgroundColor,
            EventRulesSyntaxTheme.DefaultEditorBackgroundColor);
        if (EventRulesEditorBackgroundColor != editorBackgroundColor)
        {
            EventRulesEditorBackgroundColor = editorBackgroundColor;
        }
    }

    private void SaveSettings()
    {
        bool themeChanged = _settingsService.Current.ThemeMode != ThemeMode;
        if (QueryColumnWidth <= 0)
        {
            QueryColumnWidth = 160;
        }

        if (themeChanged)
        {
            var defaults = EventRulesSyntaxTheme.GetDefaults(ThemeMode);
            EventRulesCommentColor = defaults.Comment;
            EventRulesLinkColor = defaults.Link;
            EventRulesPipeColor = defaults.Pipe;
            EventRulesInputColor = defaults.Input;
            EventRulesStringColor = defaults.String;
            EventRulesOutputColor = defaults.Output;
            EventRulesEqualsColor = defaults.EqualsColor;
            EventRulesDefaultTextColor = defaults.DefaultText;
            EventRulesEditorBackgroundColor = defaults.Background;
        }

        _settingsService.Update(settings =>
        {
            settings.ThemeMode = ThemeMode;
            settings.IsLoggingEnabled = IsLoggingEnabled;
            settings.LoggingPath = string.IsNullOrWhiteSpace(LoggingPath)
                ? AppSettingsService.DefaultLoggingPath
                : LoggingPath;
            settings.IsClientLoggingEnabled = IsClientLoggingEnabled;
            settings.ClientLoggingPath = string.IsNullOrWhiteSpace(ClientLoggingPath)
                ? AppSettingsService.DefaultClientLoggingPath
                : ClientLoggingPath;
            settings.ColumnHeaderDisplayMode = ColumnHeaderDisplayMode;
            settings.ShowTablePrefixInHeader = ShowTablePrefixInHeader;
            settings.QueryColumnWidth = QueryColumnWidth;
            settings.EventRulesCommentColor = EventRulesSyntaxTheme.ToHex(EventRulesCommentColor);
            settings.EventRulesLinkColor = EventRulesSyntaxTheme.ToHex(EventRulesLinkColor);
            settings.EventRulesPipeColor = EventRulesSyntaxTheme.ToHex(EventRulesPipeColor);
            settings.EventRulesInputColor = EventRulesSyntaxTheme.ToHex(EventRulesInputColor);
            settings.EventRulesStringColor = EventRulesSyntaxTheme.ToHex(EventRulesStringColor);
            settings.EventRulesOutputColor = EventRulesSyntaxTheme.ToHex(EventRulesOutputColor);
            settings.EventRulesEqualsColor = EventRulesSyntaxTheme.ToHex(EventRulesEqualsColor);
            settings.EventRulesDefaultTextColor = EventRulesSyntaxTheme.ToHex(EventRulesDefaultTextColor);
            settings.EventRulesEditorBackgroundColor = EventRulesSyntaxTheme.ToHex(EventRulesEditorBackgroundColor);
        });
    }
}
