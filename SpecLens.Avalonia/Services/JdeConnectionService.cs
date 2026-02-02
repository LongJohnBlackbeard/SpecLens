using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using CoreClient = JdeClient.Core.JdeClient;
using JdeClient.Core;
using Serilog;

namespace SpecLens.Avalonia.Services;

public interface IJdeConnectionService : INotifyPropertyChanged, IDisposable
{
    bool IsConnected { get; }
    bool IsConnecting { get; }
    string StatusMessage { get; }
    CoreClient Client { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task MarkDisconnectedAsync(string reason);
    Task<T> RunExclusiveAsync<T>(Func<CoreClient, Task<T>> action, CancellationToken cancellationToken = default);
    Task RunExclusiveAsync(Func<CoreClient, Task> action, CancellationToken cancellationToken = default);
}

public sealed partial class JdeConnectionService : ReactiveObject, IJdeConnectionService, IDisposable
{
    private readonly IAppSettingsService _settingsService;
    private readonly IAppLoggingService _loggingService;
    private readonly CoreClient _client;
    private readonly JdeClientOptions _clientOptions;
    private readonly SemaphoreSlim _nativeGate = new(1, 1);

    private bool _isConnected;

    private bool _isConnecting;

    private string _statusMessage = "Not connected";

    public bool IsConnected
    {
        get => _isConnected;
        private set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    public bool IsConnecting
    {
        get => _isConnecting;
        private set => this.RaiseAndSetIfChanged(ref _isConnecting, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public CoreClient Client => _client;

    public JdeConnectionService(IAppSettingsService settingsService, IAppLoggingService loggingService)
    {
        _settingsService = settingsService;
        _loggingService = loggingService;
        _clientOptions = new JdeClientOptions();
        ApplyClientLoggingSettings(settingsService.Current);
        _client = new CoreClient(_clientOptions);
        settingsService.SettingsChanged += (_, _) => ApplyClientLoggingSettings(settingsService.Current);
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private void SetIsConnected(bool value)
    {
        RunOnUiThread(() => IsConnected = value);
    }

    private void SetIsConnecting(bool value)
    {
        RunOnUiThread(() => IsConnecting = value);
    }

    private void SetStatus(string message)
    {
        RunOnUiThread(() => StatusMessage = message);
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected || IsConnecting)
        {
            return;
        }

        SetIsConnecting(true);
        SetStatus("Starting JDE runtime...");

        try
        {
            await EnsureJdeRuntimeAsync(cancellationToken);
            SetStatus("Connecting to JDE...");

            await _client.ConnectAsync(cancellationToken: cancellationToken);
            SetIsConnected(true);
            SetStatus("Connected");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            Log.Error(ex, "Failed to connect to JDE");
            throw;
        }
        finally
        {
            SetIsConnecting(false);
        }
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected)
        {
            return;
        }

        await _client.DisconnectAsync();
        SetIsConnected(false);
        SetStatus("Disconnected");
    }

    public async Task MarkDisconnectedAsync(string reason)
    {
        SetStatus(reason);
        SetIsConnected(false);

        try
        {
            await _client.DisconnectAsync();
        }
        catch
        {
        }
    }

    public async Task<T> RunExclusiveAsync<T>(Func<CoreClient, Task<T>> action, CancellationToken cancellationToken = default)
    {
        await _nativeGate.WaitAsync(cancellationToken);
        try
        {
            return await action(_client);
        }
        finally
        {
            _nativeGate.Release();
        }
    }

    public async Task RunExclusiveAsync(Func<CoreClient, Task> action, CancellationToken cancellationToken = default)
    {
        await _nativeGate.WaitAsync(cancellationToken);
        try
        {
            await action(_client);
        }
        finally
        {
            _nativeGate.Release();
        }
    }

    private static bool IsActivConsoleRunning()
    {
        return Process.GetProcessesByName("activConsole").Length > 0;
    }

    private void ApplyClientLoggingSettings(AppSettings settings)
    {
        bool enableClientLogs = settings.IsClientLoggingEnabled;
        _clientOptions.EnableDebug = enableClientLogs;
        _clientOptions.EnableSpecDebug = enableClientLogs;
        _clientOptions.EnableQueryDebug = enableClientLogs;
        _clientOptions.LogSink = _loggingService.ClientLogSink;
    }

    private static string? TryGetRunningActivConsolePath()
    {
        try
        {
            return Process.GetProcessesByName("activConsole")
                .FirstOrDefault()
                ?.MainModule
                ?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindActivConsoleInPath()
    {
        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var entry in path.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(entry.Trim(), "activConsole.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindActivConsoleFallback()
    {
        string[] candidates =
        {
            @"C:\E920_1\system\bin64\activConsole.exe",
            @"C:\E920_1\system\bin32\activConsole.exe"
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void PreferJdeRuntime(string? activConsolePath)
    {
        if (string.IsNullOrWhiteSpace(activConsolePath))
        {
            return;
        }

        string? activConsoleDir = Path.GetDirectoryName(activConsolePath);
        if (string.IsNullOrWhiteSpace(activConsoleDir))
        {
            return;
        }

        SetDllDirectoryW(activConsoleDir);

        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (!path.Contains(activConsoleDir, StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable("PATH", $"{activConsoleDir};{path}");
        }
    }

    private async Task EnsureJdeRuntimeAsync(CancellationToken cancellationToken)
    {
        string? activConsolePath = TryGetRunningActivConsolePath();
        if (string.IsNullOrWhiteSpace(activConsolePath))
        {
            activConsolePath = FindActivConsoleInPath() ?? FindActivConsoleFallback();
        }

        if (!IsActivConsoleRunning())
        {
            if (string.IsNullOrWhiteSpace(activConsolePath))
            {
                throw new InvalidOperationException("activConsole.exe was not found. Ensure the JDE runtime is installed and on PATH.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = activConsolePath,
                WorkingDirectory = Path.GetDirectoryName(activConsolePath) ?? string.Empty,
                UseShellExecute = false
            };

            Log.Information("Launching activConsole.exe from {Path}", activConsolePath);
            Process.Start(startInfo);
            await WaitForActivConsoleAsync(cancellationToken);
        }

        PreferJdeRuntime(activConsolePath ?? TryGetRunningActivConsolePath());
    }

    private static async Task WaitForActivConsoleAsync(CancellationToken cancellationToken)
    {
        const int timeoutMs = 30000;
        const int delayMs = 500;
        int elapsed = 0;

        while (elapsed < timeoutMs && !IsActivConsoleRunning())
        {
            await Task.Delay(delayMs, cancellationToken);
            elapsed += delayMs;
        }

        await Task.Delay(1500, cancellationToken);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectoryW(string lpPathName);
}


