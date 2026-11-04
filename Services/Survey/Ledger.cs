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
/// items that name at least one issue identifier, bare or inside a Linear link; code holds none.
/// </summary>
internal static partial class Ledger
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();

    /// <summary>The ledger's rows, or null when the body has no ledger section.</summary>
    public static List<LedgerRow>? Parse(string content)
    {
        var blocks = Markdown.Parse(content, Pipeline).ToList();
        var heading = blocks.FindIndex(block => block is HeadingBlock { Level: 2 } section
            && Visible(section).Contains("ledger", StringComparison.OrdinalIgnoreCase));
        if (heading < 0)
            return null;

        return [.. blocks.Skip(heading + 1)
            .TakeWhile(block => block is not HeadingBlock { Level: 2 })
            .SelectMany(Rows)
            .Where(row => row.Keys.Count > 0)];
    }

    // A list item's row is its own text, continuation lines included; its nested items are rows of their own.
    private static IEnumerable<LedgerRow> Rows(Block block) => block switch
    {
        Table table => table.Cast<TableRow>().Select(row => Row(row.Cast<TableCell>().Select(cell => cell.OfType<LeafBlock>()))),
        ListItemBlock item => [Row([item.OfType<LeafBlock>()]), .. item.OfType<ContainerBlock>().SelectMany(Rows)],
        ContainerBlock container => container.SelectMany(Rows),
        _ => [],
    };

    private static LedgerRow Row(IEnumerable<IEnumerable<LeafBlock>> cells)
    {
        var keyed = new StringBuilder();
        var visible = cells.Select(cell =>
        {
            var text = new StringBuilder();
            foreach (var leaf in cell)
            {
                Collect(leaf.Inline, text, keyed);
                text.Append(' ');
                keyed.Append(' ');
            }

            return Clean(text);
        }).ToList();

        return new LedgerRow(string.Join(" | ", visible), [.. Key().Matches(keyed.ToString()).Select(key => key.Value).Distinct()]);
    }

    private static string Visible(LeafBlock leaf)
    {
        var text = new StringBuilder();
        Collect(leaf.Inline, text, new StringBuilder());
        return Clean(text);
    }

    // `visible` gets what the row shows; `keyed` also gets link targets, where identifiers count too.
    private static void Collect(ContainerInline? inlines, StringBuilder visible, StringBuilder keyed)
    {
        for (var inline = inlines?.FirstChild; inline != null; inline = inline.NextSibling)
        {
            switch (inline)
            {
                case LinkInline link:
                    keyed.Append(' ').Append(link.Url).Append(' ');
                    Collect(link, visible, keyed);
                    break;
                case AutolinkInline autolink:
                    keyed.Append(' ').Append(autolink.Url).Append(' ');
                    break;
                case ContainerInline emphasis:
                    Collect(emphasis, visible, keyed);
                    break;
                default:
                    var text = Literal(inline);
                    visible.Append(text);
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

    // A bare URL shows as text, but it is a target, not something the row says.
    private static string Clean(StringBuilder text) =>
        Whitespace().Replace(BareUrl().Replace(text.ToString(), " "), " ").Trim();

    [GeneratedRegex(@"https?://\S+")]
    private static partial Regex BareUrl();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]*-[0-9]+\b")]
    private static partial Regex Key();
}
