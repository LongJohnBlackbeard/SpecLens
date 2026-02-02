using System;
using System.Collections.Generic;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace SpecLens.Avalonia.Views;

internal sealed class EventRulesFoldingStrategy
{
    public void UpdateFoldings(FoldingManager? manager, TextDocument? document)
    {
        if (manager == null || document == null)
        {
            return;
        }

        var foldings = CreateFoldings(document);
        manager.UpdateFoldings(foldings, -1);
    }

    private static IEnumerable<NewFolding> CreateFoldings(TextDocument document)
    {
        var result = new List<NewFolding>();
        var stack = new Stack<BlockStart>();
        var lines = document.Lines;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            string text = document.GetText(line);
            string content = EventRulesTextClassifier.GetContent(text).TrimEnd();

            if (EventRulesTextClassifier.IsTableOrViewHeader(content, out _) ||
                EventRulesTextClassifier.IsBusinessFunctionHeader(content))
            {
                int lastParamIndex = FindLastParameterLine(document, lines, i + 1);
                if (lastParamIndex > i)
                {
                    var fold = CreateFold(line, lines[lastParamIndex], content);
                    if (fold != null)
                    {
                        result.Add(fold);
                    }
                }
            }

            if (EventRulesTextClassifier.IsBlockStart(content, out var startKind))
            {
                stack.Push(new BlockStart(startKind, line, content));
                continue;
            }

            if (EventRulesTextClassifier.IsBlockEnd(content, out var endKind))
            {
                if (TryPopMatching(stack, endKind, out var start))
                {
                    var fold = CreateFold(start.Line, line, start.Label);
                    if (fold != null)
                    {
                        result.Add(fold);
                    }
                }
            }
        }

        result.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return result;
    }

    private static NewFolding? CreateFold(DocumentLine startLine, DocumentLine endLine, string label)
    {
        if (endLine.EndOffset <= startLine.EndOffset)
        {
            return null;
        }

        var folding = new NewFolding(startLine.EndOffset, endLine.EndOffset)
        {
            Name = string.IsNullOrWhiteSpace(label) ? "..." : label.Trim(),
            IsDefinition = true
        };

        return folding;
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

    private static bool TryPopMatching(Stack<BlockStart> stack, EventRulesBlockKind kind, out BlockStart start)
    {
        while (stack.Count > 0)
        {
            start = stack.Pop();
            if (start.Kind == kind)
            {
                return true;
            }
        }

        start = default;
        return false;
    }

    private readonly record struct BlockStart(EventRulesBlockKind Kind, DocumentLine Line, string Label);
}
