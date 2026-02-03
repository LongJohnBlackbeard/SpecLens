using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using ReactiveUI.Avalonia;

namespace SpecLens.Avalonia;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !IsRunningAsAdministrator())
        {
            TryRestartAsAdministrator(args);
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static void TryRestartAsAdministrator(string[] args)
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return;
        }

        string arguments = BuildArgumentsForElevation(processPath, args);
        if (string.IsNullOrWhiteSpace(arguments) && IsDotNetHost(processPath))
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Environment.CurrentDirectory
            };
            Process.Start(startInfo);
        }
        catch
        {
            // User likely canceled UAC prompt; just exit.
        }
    }

    private static string BuildArgumentsForElevation(string processPath, string[] args)
    {
        if (IsDotNetHost(processPath))
        {
            string? entryAssembly = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(entryAssembly))
            {
                return string.Empty;
            }

            return $"{Quote(entryAssembly)} {JoinArguments(args)}".Trim();
        }

        return JoinArguments(args);
    }

    private static bool IsDotNetHost(string processPath)
    {
        string fileName = Path.GetFileName(processPath);
        return fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static string JoinArguments(string[] args)
    {
        if (args.Length == 0)
        {
            return string.Empty;
        }

        var parts = new string[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            parts[i] = Quote(args[i]);
        }

        return string.Join(" ", parts);
    }

    private static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\\\"")}\"";
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI()
            .WithInterFont()
            .LogToTrace();
}

