using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ReactiveUI;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;
using JdeClient.Core.Models;

namespace SpecLens.Avalonia.Services;

public interface IAppLoggingService
{
    bool IsEnabled { get; set; }
    LogEventLevel MinimumLevel { get; }
    Action<string> ClientLogSink { get; }
}

public sealed class AppLoggingService : ReactiveObject, IAppLoggingService, IDisposable
{
    internal static LoggingLevelSwitch LevelSwitch { get; } = new(LogEventLevel.Fatal);

    private readonly IAppSettingsService settingsService;
    private readonly object _sync = new();
    private bool _isEnabled;
    private string _logPath;
    private bool _isClientEnabled;
    private string _clientLogPath;
    private Logger _clientLogger;

    private static readonly Regex SingleQuotedPattern = new("'[^']*'", RegexOptions.Compiled);
    private static readonly Regex BufferHexPattern = new("(Buffer hex:\\s*)(.*)$", RegexOptions.Compiled);
    private static readonly Regex ValuePattern = new("(value\\s*[:=]\\s*)([^,]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public AppLoggingService(IAppSettingsService settingsService)
    {
        this.settingsService = settingsService;
        _logPath = ResolveLogPath(settingsService.Current.LoggingPath, AppSettingsService.DefaultLoggingPath);
        _clientLogPath = ResolveLogPath(settingsService.Current.ClientLoggingPath, AppSettingsService.DefaultClientLoggingPath);
        ConfigureLogger(_logPath);
        _clientLogger = CreateClientLogger(_clientLogPath);
        IsEnabled = settingsService.Current.IsLoggingEnabled;
        _isClientEnabled = settingsService.Current.IsClientLoggingEnabled;
        settingsService.SettingsChanged += OnSettingsChanged;
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isEnabled, value))
            {
                LevelSwitch.MinimumLevel = value ? LogEventLevel.Information : LogEventLevel.Fatal;
            }
        }
    }

    public LogEventLevel MinimumLevel => LevelSwitch.MinimumLevel;
    public Action<string> ClientLogSink => LogClientMessage;

    private void OnSettingsChanged(object? sender, System.EventArgs e)
    {
        var settings = settingsService.Current;
        string newPath = ResolveLogPath(settings.LoggingPath, AppSettingsService.DefaultLoggingPath);
        if (!string.Equals(_logPath, newPath, StringComparison.OrdinalIgnoreCase))
        {
            _logPath = newPath;
            ReconfigureLogger(newPath);
        }

        string newClientPath = ResolveLogPath(settings.ClientLoggingPath, AppSettingsService.DefaultClientLoggingPath);
        if (!string.Equals(_clientLogPath, newClientPath, StringComparison.OrdinalIgnoreCase))
        {
            _clientLogPath = newClientPath;
            ReconfigureClientLogger(newClientPath);
        }

        bool enabled = settings.IsLoggingEnabled;
        if (IsEnabled != enabled)
        {
            IsEnabled = enabled;
        }

        _isClientEnabled = settings.IsClientLoggingEnabled;
    }

    private static string ResolveLogPath(string? candidate, string defaultPath)
    {
        string path = string.IsNullOrWhiteSpace(candidate)
            ? defaultPath
            : candidate;

        return Environment.ExpandEnvironmentVariables(path.Trim());
    }

    private void ReconfigureLogger(string path)
    {
        lock (_sync)
        {
            Log.CloseAndFlush();
            ConfigureLogger(path);
        }
    }

    private void ReconfigureClientLogger(string path)
    {
        lock (_sync)
        {
            _clientLogger.Dispose();
            _clientLogger = CreateClientLogger(path);
        }
    }

    private static void ConfigureLogger(string path)
    {
        EnsureLogDirectory(path);

        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LevelSwitch)
                .Enrich.WithProperty("App", "SpecLens")
                .Enrich.WithProperty("Version", version)
                .Enrich.WithProperty("ProcessId", Environment.ProcessId)
                .Destructure.ByTransforming<JdeFilter>(filter => new
                {
                    filter.ColumnName,
                    filter.Operator,
                    Value = "<redacted>"
                })
                .Destructure.ByTransforming<Dictionary<string, object?>>(row => new
                {
                    ColumnCount = row.Count,
                    Data = "<redacted>"
                })
                .WriteTo.File(new CompactJsonFormatter(), path, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true)
                .CreateLogger();
        }
        catch
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(LevelSwitch)
                .CreateLogger();
        }
    }

    private static Logger CreateClientLogger(string path)
    {
        EnsureLogDirectory(path);

        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

        try
        {
            return new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithProperty("App", "SpecLens")
                .Enrich.WithProperty("Channel", "JdeClient")
                .Enrich.WithProperty("Version", version)
                .Enrich.WithProperty("ProcessId", Environment.ProcessId)
                .WriteTo.File(new CompactJsonFormatter(), path, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true)
                .CreateLogger();
        }
        catch
        {
            return new LoggerConfiguration()
                .MinimumLevel.Fatal()
                .CreateLogger();
        }
    }

    private void LogClientMessage(string message)
    {
        if (!_isClientEnabled || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string sanitized = SanitizeClientMessage(message);
        try
        {
            _clientLogger.Information("{Message}", sanitized);
        }
        catch
        {
            // Swallow to avoid logging failures from impacting runtime behavior.
        }
    }

    private static string SanitizeClientMessage(string message)
    {
        try
        {
            string sanitized = SingleQuotedPattern.Replace(message, "'<redacted>'");
            sanitized = BufferHexPattern.Replace(sanitized, "$1<redacted>");
            sanitized = ValuePattern.Replace(sanitized, "$1<redacted>");
            return sanitized;
        }
        catch
        {
            return "<redacted>";
        }
    }

    private static void EnsureLogDirectory(string path)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch
        {
            // Swallow to avoid failing app startup on logging setup.
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _clientLogger.Dispose();
            Log.CloseAndFlush();
        }
    }
}
