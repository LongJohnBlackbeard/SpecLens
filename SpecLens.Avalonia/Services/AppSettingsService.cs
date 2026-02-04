using System;
using System.IO;
using System.Text.Json;
using Avalonia.Controls;
using JdeClient.Core.Models;
using SpecLens.Avalonia.Models;

namespace SpecLens.Avalonia.Services;

public sealed class AppSettings
{
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 700;
    public double WindowLeft { get; set; } = 100;
    public double WindowTop { get; set; } = 100;
    public WindowState WindowState { get; set; } = WindowState.Normal;
    public double SearchPaneWidth { get; set; } = 360;
    public bool IsLoggingEnabled { get; set; }
    public string LoggingPath { get; set; } = string.Empty;
    public bool IsClientLoggingEnabled { get; set; }
    public string ClientLoggingPath { get; set; } = string.Empty;
    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.Light;
    public string SearchText { get; set; } = string.Empty;
    public string DescriptionSearchText { get; set; } = string.Empty;
    public JdeObjectType ObjectTypeFilter { get; set; } = JdeObjectType.Table;
    public ColumnHeaderDisplayMode ColumnHeaderDisplayMode { get; set; } = ColumnHeaderDisplayMode.DataDictionary;
    public bool ShowTablePrefixInHeader { get; set; } = true;
    public double QueryColumnWidth { get; set; } = 160;
    public string EventRulesCommentColor { get; set; } = EventRulesSyntaxTheme.DefaultCommentColor;
    public string EventRulesLinkColor { get; set; } = EventRulesSyntaxTheme.DefaultLinkColor;
    public string EventRulesPipeColor { get; set; } = EventRulesSyntaxTheme.DefaultPipeColor;
    public string EventRulesInputColor { get; set; } = EventRulesSyntaxTheme.DefaultInputColor;
    public string EventRulesOutputColor { get; set; } = EventRulesSyntaxTheme.DefaultOutputColor;
    public string EventRulesEqualsColor { get; set; } = EventRulesSyntaxTheme.DefaultEqualsColor;
    public string EventRulesDefaultTextColor { get; set; } = EventRulesSyntaxTheme.DefaultTextColor;
    public string EventRulesEditorBackgroundColor { get; set; } = EventRulesSyntaxTheme.DefaultEditorBackgroundColor;
    public string EventRulesStringColor { get; set; } = EventRulesSyntaxTheme.DefaultStringColor;
}

public interface IAppSettingsService
{
    AppSettings Current { get; }
    event EventHandler? SettingsChanged;
    void Update(Action<AppSettings> update);
    void Save();
}

public sealed class AppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string settingsPath;

    public AppSettingsService()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpecLens");
        Directory.CreateDirectory(root);
        settingsPath = Path.Combine(root, "settings.json");
        Current = Load();
        ApplyDefaults(Current);
    }

    public AppSettings Current { get; }
    public event EventHandler? SettingsChanged;

    public static string DefaultLoggingPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpecLens",
            "Logs",
            "App",
            "spec-lens-.log");

    public static string DefaultClientLoggingPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpecLens",
            "Logs",
            "JdeClient",
            "jde-client-.log");

    public void Update(Action<AppSettings> update)
    {
        update(Current);
        Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Save()
    {
        try
        {
            string payload = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(settingsPath, payload);
        }
        catch
        {
            // Swallow to avoid crashing on settings write failures.
        }
    }

    private AppSettings Load()
    {
        if (!File.Exists(settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            string payload = File.ReadAllText(settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(payload, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private static void ApplyDefaults(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.LoggingPath))
        {
            settings.LoggingPath = DefaultLoggingPath;
        }

        if (string.IsNullOrWhiteSpace(settings.ClientLoggingPath))
        {
            settings.ClientLoggingPath = DefaultClientLoggingPath;
        }
    }
}
