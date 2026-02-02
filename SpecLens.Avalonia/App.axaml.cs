using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using SpecLens.Avalonia.Services;
using SpecLens.Avalonia.ViewModels;
using SpecLens.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace SpecLens.Avalonia;

public partial class App : Application
{
    private IServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Setup DI container
            _services = ConfigureServices();
            _services.GetRequiredService<IAppLoggingService>();

            // Disable Avalonia data annotation validation
            DisableAvaloniaDataAnnotationValidation();

            var settingsService = _services.GetRequiredService<IAppSettingsService>();
            ApplyTheme(settingsService.Current);
            EventRulesSyntaxTheme.Apply(settingsService.Current);
            settingsService.SettingsChanged += (_, _) =>
            {
                ApplyTheme(settingsService.Current);
                EventRulesSyntaxTheme.Apply(settingsService.Current);
            };

            // Create main window with DI-provided ViewModel
            desktop.MainWindow = new MainWindow
            {
                ViewModel = _services.GetRequiredService<MainWindowViewModel>()
            };

            // Cleanup on shutdown
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register services as singletons
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IAppLoggingService, AppLoggingService>();
        services.AddSingleton<IJdeConnectionService, JdeConnectionService>();
        services.AddSingleton<IDataDictionaryInfoService, DataDictionaryInfoService>();

        // Register ViewModels as transient (new instance per request)
        services.AddTransient<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        try
        {
            // Save settings on shutdown
            _services?.GetService<IAppSettingsService>()?.Save();

            // Dispose connection
            _services?.GetService<IJdeConnectionService>()?.Dispose();

            // Close Serilog
            if (_services?.GetService<IAppLoggingService>() is IDisposable logging)
            {
                logging.Dispose();
            }
            else
            {
                Log.CloseAndFlush();
            }
        }
        catch
        {
            // Swallow to avoid crash during shutdown
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    /// <summary>
    /// Get a service from the DI container. For use in Views that can't use constructor injection.
    /// </summary>
    public static T? GetService<T>() where T : class
    {
        return (Current as App)?._services?.GetService<T>();
    }

    private void ApplyTheme(AppSettings settings)
    {
        RequestedThemeVariant = settings.ThemeMode == Models.AppThemeMode.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
        ApplyThemeResources(settings.ThemeMode);
    }

    private static void ApplyThemeResources(Models.AppThemeMode mode)
    {
        if (Current is not Application app)
        {
            return;
        }

        var palette = ThemePalette.Create(mode);
        SetBrush(app, "AppWindowBackgroundBrush", palette.WindowBackground);
        SetBrush(app, "AppPanelBackgroundBrush", palette.PanelBackground);
        SetBrush(app, "AppPanelAltBackgroundBrush", palette.PanelAltBackground);
        SetBrush(app, "AppToolbarBackgroundBrush", palette.ToolbarBackground);
        SetBrush(app, "AppBorderBrush", palette.Border);
        SetBrush(app, "AppBorderStrongBrush", palette.BorderStrong);
        SetBrush(app, "AppSplitterBrush", palette.Splitter);
        SetBrush(app, "AppTextPrimaryBrush", palette.TextPrimary);
        SetBrush(app, "AppTextSecondaryBrush", palette.TextSecondary);
        SetBrush(app, "AppTextMutedBrush", palette.TextMuted);
        SetBrush(app, "AppHeaderBackgroundBrush", palette.HeaderBackground);
        SetBrush(app, "AppHeaderForegroundBrush", palette.HeaderForeground);
        SetBrush(app, "AppHeaderSubtextBrush", palette.HeaderSubtext);
        SetBrush(app, "AppHeaderButtonBorderBrush", palette.HeaderButtonBorder);
        SetBrush(app, "AppTitleBarButtonHoverBrush", palette.TitleBarButtonHover);
        SetBrush(app, "AppAccentBrush", palette.Accent);
        SetBrush(app, "AppAccentBorderBrush", palette.AccentBorder);
        SetBrush(app, "AppDisconnectBrush", palette.Disconnect);
        SetBrush(app, "AppDisconnectBorderBrush", palette.DisconnectBorder);
        SetBrush(app, "AppFrozenColumnBrush", palette.FrozenColumn);
        SetBrush(app, "AppOverlayBrush", palette.Overlay);
        SetBrush(app, "AppOverlayTextBrush", palette.OverlayText);
        SetBrush(app, "AppLoadingOverlayBrush", palette.LoadingOverlay);
        SetBrush(app, "AppShadowBrush", palette.Shadow);
        SetBrush(app, "AppTabAccentBrush", palette.TabAccent);
        SetGradient(app, "AppTabSelectedBackgroundBrush", palette.TabSelectedTop, palette.TabSelectedBottom);
        SetGradient(app, "AppTabUnselectedBackgroundBrush", palette.TabUnselectedTop, palette.TabUnselectedBottom);
        SetBoxShadow(app, "AppTabShadow", palette.Shadow);
    }

    private static void SetBrush(Application app, string key, Color color)
    {
        if (app.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = color;
            return;
        }

        app.Resources[key] = new SolidColorBrush(color);
    }

    private static void SetGradient(Application app, string key, Color top, Color bottom)
    {
        if (app.Resources[key] is not LinearGradientBrush brush)
        {
            brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
            };
            brush.GradientStops.Add(new GradientStop(top, 0));
            brush.GradientStops.Add(new GradientStop(bottom, 1));
            app.Resources[key] = brush;
            return;
        }

        if (brush.GradientStops.Count < 2)
        {
            brush.GradientStops.Clear();
            brush.GradientStops.Add(new GradientStop(top, 0));
            brush.GradientStops.Add(new GradientStop(bottom, 1));
            return;
        }

        brush.GradientStops[0].Color = top;
        brush.GradientStops[1].Color = bottom;
    }

    private static void SetBoxShadow(Application app, string key, Color color)
    {
        var boxShadow = new BoxShadow
        {
            OffsetX = 0,
            OffsetY = 2,
            Blur = 4,
            Spread = 0,
            Color = color
        };

        if (app.Resources[key] is BoxShadows shadows && shadows.Count > 0)
        {
            app.Resources[key] = new BoxShadows(boxShadow);
            return;
        }

        app.Resources[key] = new BoxShadows(boxShadow);
    }

    private readonly record struct ThemePalette(
        Color WindowBackground,
        Color PanelBackground,
        Color PanelAltBackground,
        Color ToolbarBackground,
        Color Border,
        Color BorderStrong,
        Color Splitter,
        Color TextPrimary,
        Color TextSecondary,
        Color TextMuted,
        Color HeaderBackground,
        Color HeaderForeground,
        Color HeaderSubtext,
        Color HeaderButtonBorder,
        Color TitleBarButtonHover,
        Color Accent,
        Color AccentBorder,
        Color Disconnect,
        Color DisconnectBorder,
        Color Overlay,
        Color OverlayText,
        Color LoadingOverlay,
        Color Shadow,
        Color FrozenColumn,
        Color TabSelectedTop,
        Color TabSelectedBottom,
        Color TabUnselectedTop,
        Color TabUnselectedBottom,
        Color TabAccent)
    {
        public static ThemePalette Create(Models.AppThemeMode mode)
        {
            return mode == Models.AppThemeMode.Dark
                ? new ThemePalette(
                    WindowBackground: Color.Parse("#15181C"),
                    PanelBackground: Color.Parse("#1E2328"),
                    PanelAltBackground: Color.Parse("#20262C"),
                    ToolbarBackground: Color.Parse("#1C2126"),
                    Border: Color.Parse("#2F353C"),
                    BorderStrong: Color.Parse("#3A414A"),
                    Splitter: Color.Parse("#2F353C"),
                    TextPrimary: Color.Parse("#E8E8E8"),
                    TextSecondary: Color.Parse("#A6A6A6"),
                    TextMuted: Color.Parse("#7C7C7C"),
                    HeaderBackground: Color.Parse("#151E20"),
                    HeaderForeground: Color.Parse("#E6F0F0"),
                    HeaderSubtext: Color.Parse("#9FB0B0"),
                    HeaderButtonBorder: Color.Parse("#2B3A3A"),
                    TitleBarButtonHover: Color.Parse("#243234"),
                    Accent: Color.Parse("#3BAF87"),
                    AccentBorder: Color.Parse("#49C095"),
                    Disconnect: Color.Parse("#3E4B55"),
                    DisconnectBorder: Color.Parse("#586A78"),
                    Overlay: Color.Parse("#AA000000"),
                    OverlayText: Color.Parse("#D6D6D6"),
                    LoadingOverlay: Color.Parse("#D01B1F24"),
                    Shadow: Color.Parse("#40000000"),
                    FrozenColumn: Color.Parse("#273240"),
                    TabSelectedTop: Color.Parse("#242A31"),
                    TabSelectedBottom: Color.Parse("#1C2126"),
                    TabUnselectedTop: Color.Parse("#1E242A"),
                    TabUnselectedBottom: Color.Parse("#171C21"),
                    TabAccent: Color.Parse("#3BAF87"))
                : new ThemePalette(
                    WindowBackground: Color.Parse("#FFFFFF"),
                    PanelBackground: Color.Parse("#F5F6F7"),
                    PanelAltBackground: Color.Parse("#FAFAFA"),
                    ToolbarBackground: Color.Parse("#F4F6F6"),
                    Border: Color.Parse("#E0E0E0"),
                    BorderStrong: Color.Parse("#C9CDD3"),
                    Splitter: Color.Parse("#E0E0E0"),
                    TextPrimary: Color.Parse("#1A1A1A"),
                    TextSecondary: Color.Parse("#666666"),
                    TextMuted: Color.Parse("#999999"),
                    HeaderBackground: Color.Parse("#1E2A2A"),
                    HeaderForeground: Color.Parse("#FFFFFF"),
                    HeaderSubtext: Color.Parse("#B7C6C6"),
                    HeaderButtonBorder: Color.Parse("#3A4A4A"),
                    TitleBarButtonHover: Color.Parse("#2A3A3A"),
                    Accent: Color.Parse("#2F6F5A"),
                    AccentBorder: Color.Parse("#3F7F6A"),
                    Disconnect: Color.Parse("#546E7A"),
                    DisconnectBorder: Color.Parse("#78909C"),
                    Overlay: Color.Parse("#AA000000"),
                    OverlayText: Color.Parse("#DDDDDD"),
                    LoadingOverlay: Color.Parse("#D0FFFFFF"),
                    Shadow: Color.Parse("#20000000"),
                    FrozenColumn: Color.Parse("#E8F1FF"),
                    TabSelectedTop: Color.Parse("#FFFFFF"),
                    TabSelectedBottom: Color.Parse("#F4F6F8"),
                    TabUnselectedTop: Color.Parse("#F0F2F5"),
                    TabUnselectedBottom: Color.Parse("#E3E7EC"),
                    TabAccent: Color.Parse("#2F6F5A"));
        }
    }
}
