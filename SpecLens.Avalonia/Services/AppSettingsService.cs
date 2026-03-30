using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Media;
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
    public string ObjectSearchPathCode { get; set; } = string.Empty;
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
    public bool EnableCCodeSyntaxHighlighting { get; set; } = true;
    public string EventRulesFontFamily { get; set; } = AppSettingsService.DefaultEventRulesFontFamily;
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
    private static readonly StringComparer FontFamilyComparer = StringComparer.OrdinalIgnoreCase;
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

    public const string DefaultEventRulesFontFamily = "Consolas";

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

    public static IReadOnlyList<string> GetInstalledFontFamilyNames()
    {
        return FontManager.Current.SystemFonts
            .Select(font => font.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(FontFamilyComparer)
            .OrderBy(name => name, FontFamilyComparer)
            .ToArray();
    }

    public static string NormalizeEventRulesFontFamily(string? fontFamily)
    {
        return NormalizeEventRulesFontFamily(fontFamily, GetInstalledFontFamilyNames());
    }

    public static string NormalizeEventRulesFontFamily(string? fontFamily, IReadOnlyList<string> installedFontFamilies)
    {
        if (!string.IsNullOrWhiteSpace(fontFamily))
        {
            string? matchingFont = installedFontFamilies.FirstOrDefault(name =>
                string.Equals(name, fontFamily, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(matchingFont))
            {
                return matchingFont;
            }
        }

        string? preferredDefault = installedFontFamilies.FirstOrDefault(name =>
            string.Equals(name, DefaultEventRulesFontFamily, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(preferredDefault))
        {
            return preferredDefault;
        }

        return installedFontFamilies.FirstOrDefault()
               ?? FontFamily.Default.Name
               ?? DefaultEventRulesFontFamily;
    }

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
        catch (Exception ex)
        {
            Trace.TraceWarning("Failed to save settings. {0}", ex);
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
        catch (Exception ex)
        {
            Trace.TraceWarning("Failed to load settings. {0}", ex);
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

        settings.EventRulesFontFamily = NormalizeEventRulesFontFamily(settings.EventRulesFontFamily);
    }
}
