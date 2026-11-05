namespace DynaDocs.Services.Survey;

using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

/// <summary>
/// The lane ledger in a Project body, read as markdown: the section under the first <c>##</c> heading
/// containing "ledger" (any case), up to the next <c>##</c> heading. Its rows are the table rows and list
/// items that name at least one issue identifier, bare or inside a Linear link; code holds none. A table
/// row's state is its cell under a <c>State</c> header.
/// </summary>
internal static partial class Ledger
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();

    /// <summary>The ledger's rows, or null when the body has no ledger section.</summary>
    public static List<LedgerRow>? Parse(string content)
    {
        var blocks = Markdown.Parse(content, Pipeline).ToList();
        var heading = blocks.FindIndex(block => block is HeadingBlock { Level: 2 } section
            && Shown([section]).Contains("ledger", StringComparison.OrdinalIgnoreCase));
        if (heading < 0)
            return null;

        return [.. blocks.Skip(heading + 1)
            .TakeWhile(block => block is not HeadingBlock { Level: 2 })
            .SelectMany(Rows)
            .Where(row => row.Keys.Count > 0)];
    }

    private static IEnumerable<LedgerRow> Rows(Block block) => block switch
    {
        Table table => TableRows(table),
        ListItemBlock item => [Row(Prose(item), null), .. Nested(item)],
        ContainerBlock container => container.SelectMany(Rows),
        _ => [],
    };

    private static IEnumerable<LedgerRow> TableRows(Table table)
    {
        var rows = table.Cast<TableRow>().ToList();
        var header = rows.FirstOrDefault(row => row.IsHeader)?.Cast<TableCell>().ToList() ?? [];
        var state = header.FindIndex(cell => Shown(cell.OfType<LeafBlock>()).Equals("State", StringComparison.OrdinalIgnoreCase));

        return rows.Select(row => row.Cast<TableCell>().ToList()).Select(cells => Row(
            cells.SelectMany(cell => cell.OfType<LeafBlock>()),
            state >= 0 && state < cells.Count ? Shown(cells[state].OfType<LeafBlock>()) : null));
    }

    // A list item's text is all the prose under it, quotes included; its nested lists and tables are rows of their own.
    private static IEnumerable<LeafBlock> Prose(ContainerBlock container) => container.SelectMany(block => block switch
    {
        ListBlock or Table or CodeBlock => [],
        ContainerBlock inner => Prose(inner),
        _ => [(LeafBlock)block],
    });

    private static IEnumerable<LedgerRow> Nested(ContainerBlock container) => container.SelectMany(block => block switch
    {
        ListBlock or Table => Rows(block),
        ContainerBlock inner => Nested(inner),
        _ => [],
    });

    private static LedgerRow Row(IEnumerable<LeafBlock> leaves, string? state)
    {
        var keyed = new StringBuilder();
        foreach (var leaf in leaves)
        {
            Collect(leaf.Inline, new StringBuilder(), keyed);
            keyed.Append(' ');
        }

        return new LedgerRow([.. Key().Matches(keyed.ToString()).Select(key => key.Value).Distinct()], state);
    }

    private static string Shown(IEnumerable<LeafBlock> leaves)
    {
        var text = new StringBuilder();
        foreach (var leaf in leaves)
        {
            Collect(leaf.Inline, text, new StringBuilder());
            text.Append(' ');
        }

        return text.ToString().Trim();
    }

    // `shown` gets what the text shows; `keyed` also gets link targets, where identifiers count too.
    private static void Collect(ContainerInline? inlines, StringBuilder shown, StringBuilder keyed)
    {
        for (var inline = inlines?.FirstChild; inline != null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LinkInline link:
                    keyed.Append(' ').Append(link.Url).Append(' ');
                    Collect(link, shown, keyed);
                    break;
                case AutolinkInline autolink:
                    keyed.Append(' ').Append(autolink.Url).Append(' ');
                    break;
                case ContainerInline emphasis:
                    Collect(emphasis, shown, keyed);
                    break;
                default:
                    var text = Literal(inline);
                    shown.Append(text);
                    keyed.Append(text);
                    break;
            }
        }
    }

    private static string Literal(Inline inline) => inline switch
    {
        LiteralInline literal => literal.Content.ToString(),
        CodeInline code => code.Content,
        HtmlEntityInline entity => entity.Transcoded.ToString(),
        LineBreakInline => " ",
        _ => "",
    };

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]*-[0-9]+\b")]
    private static partial Regex Key();
}
