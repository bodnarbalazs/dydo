namespace DynaDocs.Sync.Projection;

using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Parsers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

/// <summary>Builds source-spanned syntax nodes from Markdig's parsed block and inline tree.</summary>
internal static class MarkdownSyntaxParser
{
    public static bool TryParse(string source, string? localPageTitle, out IReadOnlyList<MarkdownSyntaxNode> nodes, out string? reason)
    {
        if (source.Length > 2 * 1024 * 1024)
        {
            nodes = [];
            reason = "body exceeds the 2 MiB projection limit";
            return false;
        }
        var document = Markdown.Parse(source, Pipeline);
        var count = 0;
        nodes = BuildBlocks([.. document], source, 0, ref count, out reason);
        if (reason is not null)
        {
            nodes = [];
            return false;
        }
        if (!string.IsNullOrEmpty(localPageTitle) && nodes.Count > 0 && nodes[0].Kind == nameof(HeadingBlock)
            && string.Equals(nodes[0].Literal, localPageTitle, StringComparison.Ordinal))
            nodes = nodes.Skip(1).ToArray();
        return true;
    }

    private static readonly MarkdownPipeline Pipeline = BuildPipeline();

    private static MarkdownPipeline BuildPipeline()
    {
        var builder = new MarkdownPipelineBuilder().UsePipeTables().UseGridTables();
        builder.BlockParsers.Find<ParagraphBlockParser>()!.ParseSetexHeadings = false;
        return builder.Build();
    }

    private static IReadOnlyList<MarkdownSyntaxNode> BuildBlocks(IReadOnlyList<Block> blocks, string source, int depth,
        ref int count, out string? reason)
    {
        if (depth > 256)
        {
            reason = "Markdown nesting exceeds the 256-level projection limit";
            return [];
        }
        var result = new List<MarkdownSyntaxNode>();
        reason = null;
        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];
            if (!IsSupported(block))
            {
                reason = "unsupported Markdig node: " + block.GetType().Name;
                return [];
            }
            var start = Clamp(block.Span.Start, source.Length);
            var end = ClampEnd(block.Span.End + 1, start, source.Length, index + 1 < blocks.Count ? blocks[index + 1].Span.Start : -1);
            if (block is TableCell)
                (start, end) = TableCellSpan(source, start, end);
            var children = block is ContainerBlock container
                ? BuildBlocks([.. container], source, depth + 1, ref count, out reason)
                : block is LeafBlock leaf && leaf.Inline is not null
                    ? BuildInlines(leaf.Inline, source, depth + 1, ref count, out reason)
                    : [];
            if (reason is not null)
                return [];
            result.Add(new MarkdownSyntaxNode(block.GetType().Name, BlockSyntax(block), BlockLiteral(block, source, start, end), children,
                start, end, result.Count == 0 ? start : result[^1].End, start));
            if (++count > 20_000)
            {
                reason = "body exceeds the 20,000 syntax-node projection limit";
                return [];
            }
        }
        return result;
    }

    private static IReadOnlyList<MarkdownSyntaxNode> BuildInlines(ContainerInline first, string source, int depth, ref int count,
        out string? reason)
    {
        if (depth > 256)
        {
            reason = "Markdown nesting exceeds the 256-level projection limit";
            return [];
        }
        var result = new List<MarkdownSyntaxNode>();
        reason = null;
        for (Inline? inline = first.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            if (!IsSupported(inline))
            {
                reason = "unsupported Markdig node: " + inline.GetType().Name;
                return [];
            }
            var start = Clamp(inline.Span.Start, source.Length);
            var end = Math.Clamp(inline.Span.End + 1, start, source.Length);
            var children = inline is ContainerInline container
                ? BuildInlines(container, source, depth + 1, ref count, out reason)
                : [];
            if (reason is not null)
                return [];
            result.Add(new MarkdownSyntaxNode(inline.GetType().Name, InlineSyntax(inline), InlineLiteral(inline), children, start, end,
                result.Count == 0 ? start : result[^1].End, start));
            if (++count > 20_000)
            {
                reason = "body exceeds the 20,000 syntax-node projection limit";
                return [];
            }
        }
        return result;
    }

    private static bool IsSupported(Block block) => block is HeadingBlock or ParagraphBlock or QuoteBlock or FencedCodeBlock
        or CodeBlock or ListBlock or ListItemBlock or Table or TableRow or TableCell or ThematicBreakBlock;

    private static bool IsSupported(Inline inline) => inline is LiteralInline or CodeInline or EmphasisInline or LinkInline
        or LineBreakInline or HtmlInline or AutolinkInline;

    private static string BlockSyntax(Block block) => block switch
    {
        HeadingBlock heading => nameof(HeadingBlock) + ":" + heading.Level,
        FencedCodeBlock fenced => nameof(FencedCodeBlock) + ":" + (fenced.Info ?? ""),
        ListBlock list => nameof(ListBlock) + ":" + list.IsOrdered,
        _ => block.GetType().Name
    };

    private static string BlockLiteral(Block block, string source, int start, int end) => block switch
    {
        HeadingBlock heading => HeadingLiteral(heading, source, start, end),
        FencedCodeBlock fenced => fenced.Lines.ToString(),
        CodeBlock code => code.Lines.ToString(),
        _ => ""
    };

    private static string InlineSyntax(Inline inline) => inline switch
    {
        EmphasisInline emphasis => nameof(EmphasisInline) + ":" + emphasis.DelimiterCount,
        LinkInline link => link.GetType().Name + ":" + (link.Url ?? "") + ":" + (link.Title ?? ""),
        _ => inline.GetType().Name
    };

    private static string InlineLiteral(Inline inline) => inline switch
    {
        LiteralInline literal => literal.Content.ToString(),
        CodeInline code => code.Content,
        _ => ""
    };

    private static int Clamp(int value, int length) => Math.Clamp(value, 0, length);

    private static (int Start, int End) TableCellSpan(string source, int start, int end)
    {
        while (start < end && source[start] is '|' or ' ' or '\t')
            start++;
        while (end > start && source[end - 1] is '|' or ' ' or '\t')
            end--;
        return (start, end);
    }

    private static string HeadingLiteral(HeadingBlock heading, string source, int start, int end)
    {
        var cursor = start;
        while (cursor < end && source[cursor] == '#')
            cursor++;
        while (cursor < end && source[cursor] is ' ' or '\t')
            cursor++;
        return SemanticTextMap.Create(source[cursor..end], cursor).Text;
    }

    private static int ClampEnd(int proposed, int start, int length, int nextStart = -1)
    {
        var end = Math.Clamp(proposed, start, length);
        if (nextStart >= 0 && nextStart < end)
            end = Math.Clamp(nextStart, start, length);
        return end;
    }
}
