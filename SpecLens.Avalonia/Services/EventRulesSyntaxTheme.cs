using System;
using Avalonia.Media;
using SpecLens.Avalonia.Models;

namespace SpecLens.Avalonia.Services;

public static class EventRulesSyntaxTheme
{
    public const string DefaultCommentColor = "#2E7D32";
    public const string DefaultLinkColor = "#1E88E5";
    public const string DefaultPipeColor = "#7A7A7A";
    public const string DefaultInputColor = "#C62828";
    public const string DefaultOutputColor = "#1565C0";
    public const string DefaultEqualsColor = "#EF6C00";
    public const string DefaultTextColor = "#1F1F1F";
    public const string DefaultEditorBackgroundColor = "#FFFFFF";

    private static readonly SolidColorBrush CommentBrushInternal = new(Color.Parse(DefaultCommentColor));
    private static readonly SolidColorBrush LinkBrushInternal = new(Color.Parse(DefaultLinkColor));
    private static readonly SolidColorBrush PipeBrushInternal = new(Color.Parse(DefaultPipeColor));
    private static readonly SolidColorBrush InputBrushInternal = new(Color.Parse(DefaultInputColor));
    private static readonly SolidColorBrush OutputBrushInternal = new(Color.Parse(DefaultOutputColor));
    private static readonly SolidColorBrush EqualsBrushInternal = new(Color.Parse(DefaultEqualsColor));
    private static readonly SolidColorBrush DefaultTextBrushInternal = new(Color.Parse(DefaultTextColor));
    private static readonly SolidColorBrush EditorBackgroundBrushInternal = new(Color.Parse(DefaultEditorBackgroundColor));

    public static event EventHandler? ThemeChanged;

    public static IBrush CommentBrush => CommentBrushInternal;
    public static IBrush LinkBrush => LinkBrushInternal;
    public static IBrush PipeBrush => PipeBrushInternal;
    public static IBrush InputBrush => InputBrushInternal;
    public static IBrush OutputBrush => OutputBrushInternal;
    public static IBrush EqualsBrush => EqualsBrushInternal;
    public static IBrush DefaultTextBrush => DefaultTextBrushInternal;
    public static IBrush EditorBackgroundBrush => EditorBackgroundBrushInternal;

    public static void Apply(AppSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        UpdateBrush(CommentBrushInternal, settings.EventRulesCommentColor, DefaultCommentColor);
        UpdateBrush(LinkBrushInternal, settings.EventRulesLinkColor, DefaultLinkColor);
        UpdateBrush(PipeBrushInternal, settings.EventRulesPipeColor, DefaultPipeColor);
        UpdateBrush(InputBrushInternal, settings.EventRulesInputColor, DefaultInputColor);
        UpdateBrush(OutputBrushInternal, settings.EventRulesOutputColor, DefaultOutputColor);
        UpdateBrush(EqualsBrushInternal, settings.EventRulesEqualsColor, DefaultEqualsColor);
        UpdateBrush(DefaultTextBrushInternal, settings.EventRulesDefaultTextColor, DefaultTextColor);
        UpdateBrush(EditorBackgroundBrushInternal, settings.EventRulesEditorBackgroundColor, DefaultEditorBackgroundColor);

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static EventRulesSyntaxDefaults GetDefaults(AppThemeMode mode)
    {
        if (mode == AppThemeMode.Dark)
        {
        return new EventRulesSyntaxDefaults(
            Comment: Color.Parse("#7BC77B"),
            Link: Color.Parse("#7AB9F5"),
            Pipe: Color.Parse("#9AA0A6"),
            Input: Color.Parse("#F28B82"),
            Output: Color.Parse("#8AB4F8"),
            EqualsColor: Color.Parse("#F6A04D"),
            DefaultText: Color.Parse("#E6E6E6"),
            Background: Color.Parse("#1B1F24"));
        }

        return new EventRulesSyntaxDefaults(
            Comment: Color.Parse(DefaultCommentColor),
            Link: Color.Parse(DefaultLinkColor),
            Pipe: Color.Parse(DefaultPipeColor),
            Input: Color.Parse(DefaultInputColor),
            Output: Color.Parse(DefaultOutputColor),
            EqualsColor: Color.Parse(DefaultEqualsColor),
            DefaultText: Color.Parse(DefaultTextColor),
            Background: Color.Parse(DefaultEditorBackgroundColor));
    }

    public static Color ParseColor(string? value, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(value) && Color.TryParse(value, out var color))
        {
            return color;
        }

        if (!string.IsNullOrWhiteSpace(fallback) && Color.TryParse(fallback, out var fallbackColor))
        {
            return fallbackColor;
        }

        return Colors.Black;
    }

    public static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static void UpdateBrush(SolidColorBrush brush, string? value, string fallback)
    {
        brush.Color = ParseColor(value, fallback);
    }
}

public readonly record struct EventRulesSyntaxDefaults(
    Color Comment,
    Color Link,
    Color Pipe,
    Color Input,
    Color Output,
    Color EqualsColor,
    Color DefaultText,
    Color Background);
