using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Controls.Documents;

namespace Tshin.Utilities;

public static class BbCodeParser
{
    private static readonly char[] TagStart = ['['];

    public static InlineCollection Parse(string? text)
    {
        var inlines = new InlineCollection();
        if (string.IsNullOrEmpty(text))
            return inlines;

        var segments = ParseToSegments(text);
        foreach (var seg in segments)
        {
            var run = new Run { Text = seg.Text };
            if (seg.Bold) run.FontWeight = FontWeight.Bold;
            if (seg.Italic) run.FontStyle = FontStyle.Italic;
            if (seg.Underline) run.TextDecorations = TextDecorations.Underline;
            if (seg.FontSize.HasValue) run.FontSize = seg.FontSize.Value;
            if (seg.Color.HasValue) run.Foreground = new SolidColorBrush(seg.Color.Value);
            inlines.Add(run);
        }
        return inlines;
    }

    private sealed record FormatState(bool Bold, bool Italic, bool Underline, double? FontSize, Color? Color);

    private sealed record TextSegment(string Text, bool Bold, bool Italic, bool Underline, double? FontSize, Color? Color);

    private static List<TextSegment> ParseToSegments(string text)
    {
        var segments = new List<TextSegment>();
        var stack = new List<FormatState>();
        var pos = 0;
        var sb = new System.Text.StringBuilder();

        while (pos < text.Length)
        {
            if (text[pos] == '[')
            {
                var closeBracket = text.IndexOf(']', pos + 1);
                if (closeBracket == -1)
                {
                    sb.Append(text[pos]);
                    pos++;
                    continue;
                }

                var tag = text[(pos + 1)..closeBracket];
                var isClosing = tag.StartsWith('/');

                if (isClosing)
                {
                    var tagName = tag[1..].ToLowerInvariant();
                    if (tagName is "b" or "i" or "u" or "size" or "color")
                    {
                        FlushBuffer(sb, stack, segments);
                        PopTag(stack, tagName);
                        pos = closeBracket + 1;
                        continue;
                    }
                }
                else
                {
                    var (tagName, value) = ParseOpeningTag(tag);
                    if (tagName is "b" or "i" or "u" or "size" or "color")
                    {
                        FlushBuffer(sb, stack, segments);
                        PushTag(stack, tagName, value);
                        pos = closeBracket + 1;
                        continue;
                    }
                }

                sb.Append(text[pos]);
                pos++;
            }
            else
            {
                sb.Append(text[pos]);
                pos++;
            }
        }

        FlushBuffer(sb, stack, segments);
        return segments;
    }

    private static (string name, string? value) ParseOpeningTag(string tag)
    {
        var eq = tag.IndexOf('=');
        if (eq == -1)
            return (tag.ToLowerInvariant(), null);
        var name = tag[..eq].ToLowerInvariant();
        var value = tag[(eq + 1)..];
        return (name, value);
    }

    private static void PushTag(List<FormatState> stack, string name, string? value)
    {
        var current = stack.Count > 0 ? stack[^1] : new FormatState(false, false, false, null, null);
        var bold = current.Bold || name == "b";
        var italic = current.Italic || name == "i";
        var underline = current.Underline || name == "u";
        double? fontSize = current.FontSize;
        Color? color = current.Color;

        if (name == "size" && value is not null)
        {
            if (double.TryParse(value, out var fs))
                fontSize = fs;
        }

        if (name == "color" && value is not null)
        {
            if (Color.TryParse(value, out var c))
                color = c;
        }

        stack.Add(new FormatState(bold, italic, underline, fontSize, color));
    }

    private static void PopTag(List<FormatState> stack, string name)
    {
        if (stack.Count == 0) return;

        for (var i = stack.Count - 1; i >= 0; i--)
        {
            if (name is "b" or "i" or "u" or "size" or "color")
            {
                stack.RemoveAt(i);
                return;
            }
        }
    }

    private static void FlushBuffer(System.Text.StringBuilder sb, List<FormatState> stack, List<TextSegment> segments)
    {
        if (sb.Length == 0) return;
        var fmt = stack.Count > 0 ? stack[^1] : new FormatState(false, false, false, null, null);
        segments.Add(new TextSegment(sb.ToString(), fmt.Bold, fmt.Italic, fmt.Underline, fmt.FontSize, fmt.Color));
        sb.Clear();
    }
}
