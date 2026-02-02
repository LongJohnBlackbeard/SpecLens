using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using SpecLens.Avalonia.Services;

namespace SpecLens.Avalonia.Views;

internal sealed class EventRulesLineNumberMargin : LineNumberMargin
{
    private readonly EventRulesLineNumberMap _map = new();

    public void RebuildLineNumberMap()
    {
        if (Document == null)
        {
            return;
        }

        _map.Rebuild(Document);
        MinWidthInDigits = Math.Max(MinWidthInDigits, _map.MaxDigits);
        InvalidateMeasure();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Document == null || TextView == null)
        {
            return;
        }

        if (!TextView.VisualLinesValid)
        {
            TextView.EnsureVisualLines();
        }

        foreach (var visualLine in TextView.VisualLines)
        {
            var docLine = visualLine.FirstDocumentLine;
            if (!_map.TryGetDisplayNumber(docLine.LineNumber, out int displayNumber))
            {
                continue;
            }

            if (!_map.ShouldRenderNumber(docLine.LineNumber))
            {
                continue;
            }

            string numberText = displayNumber.ToString(CultureInfo.InvariantCulture);
            var formatted = new FormattedText(
                numberText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                Typeface,
                EmSize,
                EventRulesSyntaxTheme.DefaultTextBrush)
            {
                TextAlignment = TextAlignment.Right
            };

            double x = Math.Max(0, Bounds.Width - formatted.Width - 4);
            double y = visualLine.VisualTop - TextView.VerticalOffset;
            double baseline = visualLine.TextLines.Count > 0 ? visualLine.TextLines[0].Baseline : formatted.Baseline;
            var point = new Point(x, y + baseline - formatted.Baseline);
            context.DrawText(formatted, point);
        }
    }
}

internal sealed class EventRulesLineNumberMap
{
    private readonly Dictionary<int, int> _lineToNumber = new();
    private readonly HashSet<int> _suppressedLines = new();

    public int MaxDigits { get; private set; }

    public void Rebuild(TextDocument document)
    {
        _lineToNumber.Clear();
        _suppressedLines.Clear();
        MaxDigits = 1;

        int logicalLine = 0;
        var lines = document.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            string text = document.GetText(line);
            string content = EventRulesTextClassifier.GetContent(text).TrimEnd();

            if (EventRulesTextClassifier.IsConditionContinuation(content))
            {
                if (logicalLine == 0)
                {
                    logicalLine = 1;
                }

                _lineToNumber[line.LineNumber] = logicalLine;
                _suppressedLines.Add(line.LineNumber);
                continue;
            }

            if (IsGroupedHeader(content))
            {
                logicalLine++;
                _lineToNumber[line.LineNumber] = logicalLine;
                int lastParamIndex = FindLastParameterLine(document, lines, i + 1);
                for (int j = i + 1; j <= lastParamIndex; j++)
                {
                    _lineToNumber[lines[j].LineNumber] = logicalLine;
                    _suppressedLines.Add(lines[j].LineNumber);
                }

                i = Math.Max(i, lastParamIndex);
                continue;
            }

            logicalLine++;
            _lineToNumber[line.LineNumber] = logicalLine;
        }

        MaxDigits = Math.Max(1, logicalLine.ToString(CultureInfo.InvariantCulture).Length);
    }

    public bool TryGetDisplayNumber(int documentLineNumber, out int displayNumber)
    {
        return _lineToNumber.TryGetValue(documentLineNumber, out displayNumber);
    }

    public bool ShouldRenderNumber(int documentLineNumber)
    {
        return !_suppressedLines.Contains(documentLineNumber);
    }

    private static bool IsGroupedHeader(string content)
    {
        return EventRulesTextClassifier.IsTableOrViewHeader(content, out _)
               || EventRulesTextClassifier.IsBusinessFunctionHeader(content);
    }

    private static int FindLastParameterLine(TextDocument document, IList<DocumentLine> lines, int startIndex)
    {
        int last = startIndex - 1;
        for (int i = startIndex; i < lines.Count; i++)
        {
            string text = document.GetText(lines[i]);
            if (!EventRulesTextClassifier.IsParameterLine(text))
            {
                break;
            }

            last = i;
        }

        return last;
    }
}
