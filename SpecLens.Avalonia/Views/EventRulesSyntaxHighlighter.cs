using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using SpecLens.Avalonia.Services;

namespace SpecLens.Avalonia.Views;

internal enum EventRulesLinkType
{
    BusinessFunction,
    Table
}

internal sealed record EventRulesLink(
    EventRulesLinkType Type,
    string ObjectName,
    string? MemberName,
    int StartOffset,
    int Length);

internal enum EventRulesOperatorKind
{
    None,
    Input,
    Output,
    Equals
}

internal static class EventRulesTextClassifier
{
    private static readonly string[] TableOperations =
    {
        "FetchSingle",
        "FetchNext",
        "Select",
        "Delete",
        "Update",
        "Insert"
    };

    public static int GetContentStart(string line)
    {
        int index = 0;
        while (index < line.Length)
        {
            char ch = line[index];
            if (ch == ' ' || ch == '\t' || ch == '|')
            {
                index++;
                continue;
            }

            break;
        }

        return index;
    }

    public static string GetContent(string line)
    {
        int start = GetContentStart(line);
        return start >= line.Length ? string.Empty : line.Substring(start);
    }

    public static bool IsCommentLine(string line, out int contentStart)
    {
        contentStart = GetContentStart(line);
        if (contentStart >= line.Length)
        {
            return false;
        }

        return line.Substring(contentStart).StartsWith("//", StringComparison.Ordinal);
    }

    public static bool IsBlockStart(string content, out EventRulesBlockKind kind)
    {
        if (content.StartsWith("If ", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(content, "If", StringComparison.OrdinalIgnoreCase))
        {
            kind = EventRulesBlockKind.If;
            return true;
        }

        if (content.StartsWith("While ", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(content, "While", StringComparison.OrdinalIgnoreCase))
        {
            kind = EventRulesBlockKind.While;
            return true;
        }

        kind = EventRulesBlockKind.If;
        return false;
    }

    public static bool IsBlockEnd(string content, out EventRulesBlockKind kind)
    {
        if (string.Equals(content, "End If", StringComparison.OrdinalIgnoreCase))
        {
            kind = EventRulesBlockKind.If;
            return true;
        }

        if (string.Equals(content, "End While", StringComparison.OrdinalIgnoreCase))
        {
            kind = EventRulesBlockKind.While;
            return true;
        }

        kind = EventRulesBlockKind.If;
        return false;
    }

    public static bool IsConditionContinuation(string content)
    {
        return content.StartsWith("And ", StringComparison.OrdinalIgnoreCase)
               || content.StartsWith("Or ", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsTableOrViewHeader(string content, out string tableName)
    {
        tableName = string.Empty;
        int dotIndex = content.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex <= 0)
        {
            return false;
        }

        string token = content.Substring(0, dotIndex);
        if (!IsIdentifier(token))
        {
            return false;
        }

        int opEnd = content.IndexOf(' ', dotIndex + 1);
        if (opEnd < 0)
        {
            opEnd = content.Length;
        }

        string operation = content.Substring(dotIndex + 1, opEnd - dotIndex - 1);
        if (TableOperations.Any(op => string.Equals(op, operation, StringComparison.OrdinalIgnoreCase)))
        {
            tableName = token;
            return true;
        }

        return false;
    }

    public static bool IsBusinessFunctionHeader(string content)
    {
        int openIndex = content.IndexOf('(', StringComparison.Ordinal);
        int closeIndex = content.IndexOf(')', StringComparison.Ordinal);
        if (openIndex <= 0 || closeIndex <= openIndex + 1)
        {
            return false;
        }

        string inside = content.Substring(openIndex + 1, closeIndex - openIndex - 1);
        int dotIndex = inside.IndexOf('.');
        return dotIndex > 0 && dotIndex < inside.Length - 1;
    }

    public static bool IsParameterLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (IsCommentLine(line, out _))
        {
            return false;
        }

        return line.Contains("<->", StringComparison.Ordinal)
               || line.Contains("<-", StringComparison.Ordinal)
               || line.Contains("->", StringComparison.Ordinal)
               || line.Contains(" = ", StringComparison.Ordinal);
    }

    public static EventRulesOperatorKind GetOperatorKind(string line)
    {
        if (line.Contains("<->", StringComparison.Ordinal))
        {
            return EventRulesOperatorKind.Output;
        }

        if (line.Contains("<-", StringComparison.Ordinal))
        {
            return EventRulesOperatorKind.Output;
        }

        if (line.Contains("->", StringComparison.Ordinal))
        {
            return EventRulesOperatorKind.Input;
        }

        int equalsIndex = line.IndexOf(" = ", StringComparison.Ordinal);
        if (equalsIndex >= 0)
        {
            string right = line.Substring(equalsIndex + 3).TrimStart();
            if (ShouldColorEquals(right))
            {
                return EventRulesOperatorKind.Equals;
            }
        }

        return EventRulesOperatorKind.None;
    }

    public static IReadOnlyList<EventRulesLink> GetLinks(string line, int lineOffset)
    {
        if (string.IsNullOrEmpty(line))
        {
            return Array.Empty<EventRulesLink>();
        }

        int contentStart = GetContentStart(line);
        if (contentStart >= line.Length)
        {
            return Array.Empty<EventRulesLink>();
        }

        string content = line.Substring(contentStart);
        var links = new List<EventRulesLink>();
        int contentOffset = lineOffset + contentStart;

        var tableLink = TryGetTableLink(content, contentOffset);
        if (tableLink != null)
        {
            links.Add(tableLink);
        }

        var bfLink = TryGetBusinessFunctionLink(content, contentOffset);
        if (bfLink != null)
        {
            links.Add(bfLink);
        }

        return links;
    }

    public static EventRulesLink? FindLinkAtOffset(string line, int lineOffset, int offset)
    {
        foreach (var link in GetLinks(line, lineOffset))
        {
            if (offset >= link.StartOffset && offset < link.StartOffset + link.Length)
            {
                return link;
            }
        }

        return null;
    }

    private static EventRulesLink? TryGetTableLink(string content, int contentOffset)
    {
        int dotIndex = content.IndexOf('.', StringComparison.Ordinal);
        if (dotIndex <= 0)
        {
            return null;
        }

        string token = content.Substring(0, dotIndex);
        if (!IsIdentifier(token))
        {
            return null;
        }

        return new EventRulesLink(
            EventRulesLinkType.Table,
            token,
            MemberName: null,
            StartOffset: contentOffset,
            Length: token.Length);
    }

    private static EventRulesLink? TryGetBusinessFunctionLink(string content, int contentOffset)
    {
        int searchIndex = 0;
        while (searchIndex < content.Length)
        {
            int openIndex = content.IndexOf('(', searchIndex);
            if (openIndex < 0)
            {
                break;
            }

            int closeIndex = content.IndexOf(')', openIndex + 1);
            if (closeIndex < 0)
            {
                break;
            }

            string inside = content.Substring(openIndex + 1, closeIndex - openIndex - 1);
            if (inside.Contains(' ') || !inside.Contains('.'))
            {
                searchIndex = closeIndex + 1;
                continue;
            }

            int dotIndex = inside.IndexOf('.');
            if (dotIndex <= 0 || dotIndex == inside.Length - 1)
            {
                searchIndex = closeIndex + 1;
                continue;
            }

            string objectName = inside.Substring(0, dotIndex);
            string memberName = inside.Substring(dotIndex + 1);

            return new EventRulesLink(
                EventRulesLinkType.BusinessFunction,
                objectName,
                MemberName: memberName,
                StartOffset: contentOffset + openIndex + 1,
                Length: inside.Length);
        }

        return null;
    }

    private static bool IsIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!char.IsLetter(value[0]))
        {
            return false;
        }

        for (int i = 1; i < value.Length; i++)
        {
            char ch = value[i];
            if (!char.IsLetterOrDigit(ch) && ch != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool ShouldColorEquals(string rightSide)
    {
        if (string.IsNullOrWhiteSpace(rightSide))
        {
            return false;
        }

        if (StartsWithQualifier(rightSide))
        {
            return false;
        }

        char first = rightSide[0];
        if (first == '"' || first == '\'' || first == '<' || first == '-' || char.IsDigit(first))
        {
            return false;
        }

        return true;
    }

    private static bool StartsWithQualifier(string text)
    {
        return text.StartsWith("BF ", StringComparison.Ordinal)
               || text.StartsWith("VA ", StringComparison.Ordinal)
               || text.StartsWith("SV ", StringComparison.Ordinal)
               || text.StartsWith("CO ", StringComparison.Ordinal);
    }
}

internal enum EventRulesBlockKind
{
    If,
    While
}

internal sealed class EventRulesSyntaxHighlighter : DocumentColorizingTransformer
{
    private static IBrush CommentBrush => EventRulesSyntaxTheme.CommentBrush;
    private static IBrush LinkBrush => EventRulesSyntaxTheme.LinkBrush;
    private static IBrush PipeBrush => EventRulesSyntaxTheme.PipeBrush;
    private static IBrush InputBrush => EventRulesSyntaxTheme.InputBrush;
    private static IBrush OutputBrush => EventRulesSyntaxTheme.OutputBrush;
    private static IBrush EqualsBrush => EventRulesSyntaxTheme.EqualsBrush;
    private static IBrush StringBrush => EventRulesSyntaxTheme.StringBrush;
    private static IBrush DefaultTextBrush => EventRulesSyntaxTheme.DefaultTextBrush;

    protected override void ColorizeLine(DocumentLine line)
    {
        string text = CurrentContext.Document.GetText(line);
        int lineOffset = line.Offset;

        ApplyForeground(lineOffset, line.EndOffset, DefaultTextBrush);

        if (EventRulesTextClassifier.IsCommentLine(text, out int contentStart))
        {
            ApplyForeground(lineOffset, line.EndOffset, CommentBrush);
            return;
        }

        var opKind = EventRulesTextClassifier.GetOperatorKind(text);
        bool isTableIoEquals = IsTableIoEqualsLine(CurrentContext.Document, line, text);
        if (isTableIoEquals)
        {
            if (opKind == EventRulesOperatorKind.None || opKind == EventRulesOperatorKind.Equals)
            {
                opKind = EventRulesOperatorKind.Equals;
            }
        }
        else if (opKind == EventRulesOperatorKind.Equals)
        {
            opKind = EventRulesOperatorKind.None;
        }
        switch (opKind)
        {
            case EventRulesOperatorKind.Input:
                ApplyForeground(lineOffset, line.EndOffset, InputBrush);
                break;
            case EventRulesOperatorKind.Output:
                ApplyForeground(lineOffset, line.EndOffset, OutputBrush);
                break;
            case EventRulesOperatorKind.Equals:
                ApplyForeground(lineOffset, line.EndOffset, EqualsBrush);
                break;
        }

        for (int i = 0; i < contentStart && i < text.Length; i++)
        {
            if (text[i] == '|')
            {
                ApplyForeground(lineOffset + i, lineOffset + i + 1, PipeBrush);
            }
        }

        ApplyStringForeground(text, lineOffset);

        foreach (var link in EventRulesTextClassifier.GetLinks(text, lineOffset))
        {
            ChangeLinePart(link.StartOffset, link.StartOffset + link.Length, element =>
            {
                element.TextRunProperties.SetForegroundBrush(LinkBrush);
                element.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
            });
        }
    }

    private void ApplyForeground(int startOffset, int endOffset, IBrush brush)
    {
        ChangeLinePart(startOffset, endOffset, element => element.TextRunProperties.SetForegroundBrush(brush));
    }

    private void ApplyStringForeground(string text, int lineOffset)
    {
        foreach (var span in GetStringSpans(text))
        {
            ApplyForeground(lineOffset + span.Start, lineOffset + span.Start + span.Length, StringBrush);
        }
    }

    private static IEnumerable<(int Start, int Length)> GetStringSpans(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        bool inString = false;
        char quoteChar = '\0';
        int start = -1;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (!inString)
            {
                if (ch == '"' || ch == '\'')
                {
                    inString = true;
                    quoteChar = ch;
                    start = i;
                }

                continue;
            }

            if (ch != quoteChar)
            {
                continue;
            }

            if (i + 1 < text.Length && text[i + 1] == quoteChar)
            {
                i++;
                continue;
            }

            if (IsBackslashEscaped(text, i))
            {
                continue;
            }

            int length = i - start + 1;
            if (length > 0)
            {
                yield return (start, length);
            }

            inString = false;
            quoteChar = '\0';
            start = -1;
        }

        if (inString && start >= 0)
        {
            yield return (start, text.Length - start);
        }
    }

    private static bool IsBackslashEscaped(string text, int index)
    {
        int backslashes = 0;
        for (int i = index - 1; i >= 0; i--)
        {
            if (text[i] != '\\')
            {
                break;
            }

            backslashes++;
        }

        return backslashes % 2 == 1;
    }

    private static bool IsTableIoEqualsLine(TextDocument document, DocumentLine line, string text)
    {
        if (!text.Contains(" = ", StringComparison.Ordinal))
        {
            return false;
        }

        if (!EventRulesTextClassifier.IsParameterLine(text))
        {
            return false;
        }

        int currentIndent = GetIndentLevel(text);
        if (currentIndent == 0)
        {
            return false;
        }

        var previousLine = line.PreviousLine;
        while (previousLine != null)
        {
            string previousText = document.GetText(previousLine);
            if (string.IsNullOrWhiteSpace(previousText))
            {
                return false;
            }

            int previousIndent = GetIndentLevel(previousText);
            if (previousIndent >= currentIndent)
            {
                previousLine = previousLine.PreviousLine;
                continue;
            }

            string content = EventRulesTextClassifier.GetContent(previousText).TrimEnd();
            return EventRulesTextClassifier.IsTableOrViewHeader(content, out _);
        }

        return false;
    }

    private static int GetIndentLevel(string line)
    {
        int contentStart = EventRulesTextClassifier.GetContentStart(line);
        int level = 0;
        for (int i = 0; i < contentStart && i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '|' || ch == '\t')
            {
                level++;
            }
        }

        return level;
    }
}
