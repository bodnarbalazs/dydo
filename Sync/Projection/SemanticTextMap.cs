namespace DynaDocs.Sync.Projection;

using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

/// <summary>Maps visible Markdown text back to the raw source characters that produced it.</summary>
public sealed class SemanticTextMap
{
    private readonly int[] offsets;
    private readonly int[] ends;

    private SemanticTextMap(string text, int[] offsets, int[] ends)
    {
        Text = text;
        this.offsets = offsets;
        this.ends = ends;
    }

    public string Text { get; }

    public int RawOffset(int characterIndex) => offsets[characterIndex];

    public int RawEnd(int characterIndex) => ends[characterIndex];

    public static SemanticTextMap Create(string markdown, int sourceOffset = 0)
    {
        var text = new StringBuilder();
        var positions = new List<int>();
        var ends = new List<int>();
        var ranges = new List<(int Start, int End, bool DecodeEscapes)>();
        var hidden = new bool[markdown.Length];
        var found = false;
        var document = Markdown.Parse(markdown);
        VisitBlocks(document);
        if (!found)
            Append(markdown, 0, markdown.Length, decodeEscapes: false);
        else
        {
            var cursor = 0;
            foreach (var range in ranges.OrderBy(r => r.Start).ThenBy(r => r.End))
            {
                var start = Math.Clamp(range.Start, cursor, markdown.Length);
                var end = Math.Clamp(range.End, start, markdown.Length);
                if (cursor < start)
                    AppendLiteralGaps(cursor, start);
                if (end > cursor)
                    Append(markdown, start, end, range.DecodeEscapes);
                cursor = Math.Max(cursor, end);
            }
            if (cursor < markdown.Length)
                AppendLiteralGaps(cursor, markdown.Length);
        }

        return new SemanticTextMap(text.ToString(), [.. positions], [.. ends]);

        void Walk(Inline inline)
        {
            for (var current = inline; current is not null; current = current.NextSibling)
                switch (current)
                {
                    case LiteralInline:
                        AddRange(current.Span.Start, current.Span.End + 1, decodeEscapes: true);
                        found = true;
                        break;
                    case CodeInline:
                        var start = current.Span.Start;
                        var end = current.Span.End + 1;
                        while (start < end && markdown[start] == '`')
                        {
                            hidden[start] = true;
                            start++;
                        }
                        while (end > start && markdown[end - 1] == '`')
                        {
                            hidden[end - 1] = true;
                            end--;
                        }
                        AddRange(start, end, decodeEscapes: false);
                        found = true;
                        break;
                    case EmphasisInline emphasis when emphasis.FirstChild is not null:
                        HideDelimiters(emphasis);
                        Walk(emphasis.FirstChild);
                        break;
                    case LinkInline link when link.FirstChild is not null:
                        Hide(link.Span.Start, link.FirstChild.Span.Start - link.Span.Start);
                        Hide(link.LastChild!.Span.End + 1, link.Span.End - link.LastChild.Span.End);
                        Walk(link.FirstChild);
                        break;
                    case ContainerInline container when container.FirstChild is not null:
                        Walk(container.FirstChild);
                        break;
                    case ContainerInline:
                        break;
                    default:
                        AddRange(current.Span.Start, current.Span.End + 1, decodeEscapes: true);
                        found = true;
                        break;
                }
        }

        void VisitBlocks(ContainerBlock container)
        {
            foreach (var block in container)
            {
                if (block is LeafBlock leaf && leaf.Inline is not null)
                {
                    Walk(leaf.Inline);
                }
                if (block is ContainerBlock child)
                    VisitBlocks(child);
            }
        }

        void AddRange(int start, int end, bool decodeEscapes) => ranges.Add((start, end, decodeEscapes));

        void AppendLiteralGaps(int start, int end)
        {
            Append(markdown, start, end, decodeEscapes: false);
        }

        void Hide(int start, int length)
        {
            for (var index = Math.Max(start, 0); index < Math.Min(start + length, hidden.Length); index++)
                hidden[index] = true;
        }

        void HideDelimiters(EmphasisInline emphasis)
        {
            var marker = new string(emphasis.DelimiterChar, emphasis.DelimiterCount);
            var opening = markdown.IndexOf(marker, Math.Max(0, emphasis.Span.Start - emphasis.DelimiterCount), StringComparison.Ordinal);
            if (opening < 0)
                return;
            Hide(opening, marker.Length);
            var closing = markdown.IndexOf(marker, opening + marker.Length, StringComparison.Ordinal);
            if (closing >= 0)
                Hide(closing, marker.Length);
        }

        bool StartsListMarker(int index, int end, out int markerEnd)
        {
            markerEnd = index;
            var lineStart = markdown.LastIndexOf('\n', index == 0 ? 0 : index - 1) + 1;
            if (markdown[lineStart..index].Any(c => c is not ' ' and not '\t'))
                return false;
            var cursor = index;
            while (cursor < end && markdown[cursor] is ' ' or '\t')
                cursor++;
            if (cursor >= end)
                return false;
            if ((markdown[cursor] is '-' or '+' or '*') && cursor + 1 < end && (markdown[cursor + 1] is ' ' or '\t'))
            {
                markerEnd = cursor + 2;
                return true;
            }
            var digits = cursor;
            while (digits < end && char.IsDigit(markdown[digits]))
                digits++;
            if (digits > cursor && digits + 1 < end && (markdown[digits] is '.' or ')') && (markdown[digits + 1] is ' ' or '\t'))
            {
                markerEnd = digits + 2;
                return true;
            }
            return false;
        }

        void Append(string source, int start, int end, bool decodeEscapes)
        {
            start = Math.Clamp(start, 0, source.Length);
            end = Math.Clamp(end, start, source.Length);
            for (var i = start; i < end; i++)
            {
                if (hidden[i])
                    continue;
                if (IsListMarkerSpace(i))
                    continue;
                if (StartsListMarker(i, markdown.Length, out var markerEnd))
                {
                    i = markerEnd - 1;
                    continue;
                }
                if (decodeEscapes && source[i] == '\\' && i + 1 < end && IsEscapable(source[i + 1]))
                {
                    Add(source[++i], i - 1, i + 1);
                    continue;
                }
                Add(source[i], i, i + 1);
            }
        }

        bool IsListMarkerSpace(int index)
        {
            if (markdown[index] is not ' ' and not '\t' || index == 0)
                return false;
            var marker = markdown[index - 1];
            if (marker is not '-' and not '+' and not '*')
                return false;
            var lineStart = markdown.LastIndexOf('\n', index - 1) + 1;
            return markdown[lineStart..(index - 1)].All(c => c is ' ' or '\t');
        }

        void Add(char value, int offset, int end)
        {
            text.Append(value);
            positions.Add(sourceOffset + offset);
            ends.Add(sourceOffset + end);
        }
    }

    internal static SemanticTextMap CreateRaw(string markdown, int sourceOffset)
    {
        var offsets = Enumerable.Range(0, markdown.Length).Select(index => sourceOffset + index).ToArray();
        var ends = Enumerable.Range(1, markdown.Length).Select(index => sourceOffset + index).ToArray();
        return new(markdown, offsets, ends);
    }

    internal static SemanticTextMap CreateFencedCode(string markdown, int sourceOffset)
    {
        var firstLineEnd = markdown.IndexOf('\n');
        var lastLineStart = markdown.LastIndexOf('\n');
        if (firstLineEnd < 0 || lastLineStart <= firstLineEnd)
            return CreateRaw(markdown, sourceOffset);
        return CreateRaw(markdown[(firstLineEnd + 1)..lastLineStart], sourceOffset + firstLineEnd + 1);
    }

    internal static SemanticTextMap CreateTableCell(string markdown, int sourceOffset)
    {
        var start = 0;
        var end = markdown.Length;
        while (start < end && markdown[start] is '|' or ' ' or '\t')
            start++;
        while (end > start && markdown[end - 1] is '|' or ' ' or '\t')
            end--;
        return CreateRaw(markdown[start..end], sourceOffset + start);
    }

    private static bool IsEscapable(char c) => "\\`*{}_[]<>()#+-.!|".Contains(c);

}
