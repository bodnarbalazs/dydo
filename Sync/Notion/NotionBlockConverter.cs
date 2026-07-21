namespace DynaDocs.Sync.Notion;

using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Parsers;
using Markdig.Syntax;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// Structure-aware, best-effort markdown ⇄ Notion block conversion (Decision 025 §6, ns-6). Write: parse the
/// Markdig AST and map each block — headings (# / ## / ###), paragraphs, fenced/indented code (with language
/// normalization), and bulleted/numbered lists including nested sub-lists carried as block <c>children</c>.
/// Inline text stays a plain, unannotated run: the raw source span is kept verbatim (no bold/italic/link
/// parsing this sprint), so a marker like <c>**x**</c> survives unchanged. Read: each block renders back to a
/// markdown line, nested children indented under their parent. The structured frontmatter↔property path carries
/// the reliable data; bodies are a 3-way *text* merge over this approximation.
/// </summary>
public static class NotionBlockConverter
{
    public static List<NotionBlock> ToBlocks(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n");
        var document = Markdown.Parse(text, Pipeline);
        return ConvertSiblings([.. document], text, nested: false);
    }

    /// <summary>Convert a run of sibling blocks, passing each block the start of the NEXT sibling as a span clamp.
    /// Markdig can over-extend a paragraph's span across a following block it did not consume (a pipe table flush
    /// against the paragraph, no blank line between), so slicing that span raw would carry the table's text into the
    /// paragraph AND emit the table block — duplicating it and growing the body every normalization. Clamping at the
    /// next sibling's start closes that hole at every level (document and nested), the same guard <see cref="SplitItem"/>
    /// applies to a list item's own paragraph.</summary>
    private static List<NotionBlock> ConvertSiblings(IReadOnlyList<Block> nodes, string src, bool nested)
    {
        var blocks = new List<NotionBlock>();
        for (var i = 0; i < nodes.Count; i++)
            blocks.AddRange(Convert(nodes[i], src, nested, i + 1 < nodes.Count ? nodes[i + 1].Span.Start : -1));
        return blocks;
    }

    /// <summary>Setext headings are disabled: a paragraph directly above a <c>---</c>/<c>===</c> line must stay a
    /// paragraph, never be promoted to a heading. Body normalization strips blank lines, which brings a
    /// blank-separated thematic-break <c>---</c> adjacent to the paragraph above it; setext promotion there makes
    /// <c>FromBlocks∘ToBlocks</c> non-idempotent and would silently rewrite the canonical doc on the next sync.</summary>
    private static readonly MarkdownPipeline Pipeline = BuildPipeline();

    private static MarkdownPipeline BuildPipeline()
    {
        var builder = new MarkdownPipelineBuilder();
        builder.UsePipeTables();
        builder.BlockParsers.Find<ParagraphBlockParser>()!.ParseSetexHeadings = false;
        return builder.Build();
    }

    public static string FromBlocks(IReadOnlyList<NotionBlock> blocks)
    {
        var sb = new StringBuilder();
        Render(sb, blocks, "");
        return sb.ToString().TrimEnd('\n');
    }

    /// <param name="nested">True when this block is a child inside a list item, so its rendered lines will carry an
    /// indent prefix. A nested multi-line paragraph must have its continuation-line indentation stripped or the
    /// prefix would double it every normalization (runaway indent); a top-level paragraph keeps its lines verbatim,
    /// matching the old converter and staying idempotent (an indented line there is stable prose, not re-indented).</param>
    private static List<NotionBlock> Convert(Block node, string src, bool nested, int clampEnd)
    {
        switch (node)
        {
            // H1–H3 map to the matching Notion heading; H4–H6 clamp to heading_3 (ns-7 item 5) — see ConvertHeading.
            case HeadingBlock heading when heading.Level is >= 1 and <= 6:
                return [ConvertHeading(heading, src)];
            // A blockquote becomes a Notion quote block: its first paragraph fills the quote's own rich_text
            // (else Notion renders "Empty quote"), any remaining blocks become children. Re-parsing the quote's
            // inner content keeps inline markers verbatim like every other block (ns-7 item 4).
            case QuoteBlock quote:
                return [QuoteBlock(quote, src)];
            case Table table:
                return [TableBlock(table, src)];
            // Only a FENCED code block becomes a Notion code block. An INDENTED code block (a plain CodeBlock —
            // FencedCodeBlock, its subtype, is matched here first) has no Notion equivalent and the old line
            // converter kept those lines verbatim, so it falls through to a verbatim paragraph — which also keeps
            // normalization idempotent, since a rendered indented block re-parses as a paragraph continuation.
            case FencedCodeBlock fenced:
                return [CodeBlock(TrimTrailingNewline(fenced.Lines.ToString()), fenced.Info ?? "")];
            // An indented code block keeps its leading whitespace verbatim (it IS the content), so it is emitted as
            // a raw-span paragraph without continuation-line cleaning — a rendered indented block re-parses to the
            // same paragraph.
            case CodeBlock code:
                return [new NotionBlock { Type = "paragraph", Paragraph = Body(Slice(code, src).TrimEnd('\n')) }];
            case ListBlock list:
                return ConvertList(list, src);
            default:
                var text = ClampedSlice(node, clampEnd, src);
                return ParagraphBlocks(nested ? CleanText(text) : text);
        }
    }

    private static NotionBlock ConvertHeading(HeadingBlock heading, string src)
    {
        var body = Body(StripHeadingMarker(Slice(heading, src)));
        return heading.Level switch
        {
            1 => new NotionBlock { Type = "heading_1", Heading1 = body },
            2 => new NotionBlock { Type = "heading_2", Heading2 = body },
            _ => new NotionBlock { Type = "heading_3", Heading3 = body },
        };
    }

    /// <summary>Convert a list. An ordered list whose item numbers are not exactly 1..n (a run starting ≠1, or with
    /// gaps) cannot round-trip as numbered_list_item — Notion stores no ordinal, so FromBlocks re-sequences from 1
    /// and would rewrite the original numbers. Such a run stays verbatim paragraph lines, matching the old
    /// converter's echo; only a clean 1..n run becomes real numbered items.</summary>
    private static List<NotionBlock> ConvertList(ListBlock list, string src)
    {
        if (list.IsOrdered && !IsSequentialFromOne(list))
            return VerbatimOrderedItems(list, src);
        var items = new List<NotionBlock>();
        foreach (var child in list)
            if (child is ListItemBlock item)
                items.Add(ConvertListItem(item, list.IsOrdered, src));
        return items;
    }

    /// <summary>Notion rejects a block whose rich_text array exceeds ~100 runs with a 400. A paragraph long
    /// enough to split into more than that (200KB+ of unbroken text) is emitted across sibling paragraph blocks
    /// of ≤100 runs each — the ecosystem's overflow rule (survey: "overflowing into sibling paragraphs, not
    /// truncation").
    /// <para>KNOWN INSTABILITY: for a single logical line this long, the sibling split inserts a join newline that
    /// the next parse absorbs into the paragraph and re-splits at a shifted boundary — so such a body does NOT
    /// converge to a fixed point (it oscillates), unlike everything else the converter emits. No synced record comes
    /// within orders of magnitude of ~100 runs, so this is unreachable in practice and the fixed-point sweep never
    /// exercises it; documented and pinned by a converter test, tracked as issue 0298.</para></summary>
    private const int MaxRichTextPerBlock = 100;

    private static List<NotionBlock> ParagraphBlocks(string text)
    {
        var runs = NotionRichText.Of(text);
        if (runs.Count <= MaxRichTextPerBlock)
            return [new NotionBlock { Type = "paragraph", Paragraph = new NotionBlockBody { RichText = runs } }];

        var blocks = new List<NotionBlock>();
        for (var i = 0; i < runs.Count; i += MaxRichTextPerBlock)
            blocks.Add(new NotionBlock
            {
                Type = "paragraph",
                Paragraph = new NotionBlockBody { RichText = runs.GetRange(i, Math.Min(MaxRichTextPerBlock, runs.Count - i)) },
            });
        return blocks;
    }

    /// <summary>Convert a blockquote to a Notion quote block. The quote's inner content is re-parsed (its <c>&gt;</c>
    /// markers stripped) so nested structure and inline markers convert exactly as top-level content does; the first
    /// paragraph becomes the quote's own rich_text and the rest become children.</summary>
    private static NotionBlock QuoteBlock(QuoteBlock quote, string src)
    {
        var inner = ToBlocks(StripQuoteMarkers(Slice(quote, src)));
        NotionBlockBody body;
        List<NotionBlock> children;
        if (inner.Count > 0 && inner[0].Type == "paragraph")
        {
            body = inner[0].Paragraph!;
            children = inner.Skip(1).ToList();
        }
        else
        {
            body = Body("");
            children = inner;
        }
        return new NotionBlock { Type = "quote", Quote = body, Children = children.Count > 0 ? children : null };
    }

    /// <summary>Convert a markdown pipe table to a Notion table block: <c>table_width</c> is the widest row, short
    /// rows pad with empty cells, and every recognised markdown table carries a header row (GFM requires the
    /// separator), so <c>has_column_header</c> is set. Cell text is sliced raw so inline markers stay verbatim.
    /// The rows land in the table payload's own children (Notion's nesting for tables).</summary>
    private static NotionBlock TableBlock(Table table, string src)
    {
        var rows = new List<List<string>>();
        foreach (var child in table)
            if (child is TableRow row)
                rows.Add(row.Select(cell => CellText((TableCell)cell, src)).ToList());

        var width = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        var rowBlocks = rows.Select(cells => new NotionBlock
        {
            Type = "table_row",
            TableRow = new NotionTableRow
            {
                Cells = Enumerable.Range(0, width)
                    .Select(i => NotionRichText.Of(i < cells.Count ? cells[i] : ""))
                    .ToList(),
            },
        }).ToList();

        return new NotionBlock
        {
            Type = "table",
            Table = new NotionTable
            {
                TableWidth = width,
                HasColumnHeader = true,
                HasRowHeader = false,
                Children = rowBlocks,
            },
        };
    }

    /// <summary>A table cell's verbatim text — sliced from its paragraph's raw source (keeping inline markers) with
    /// surrounding cell padding trimmed and any soft-wrap collapsed to a space.</summary>
    private static string CellText(TableCell cell, string src)
    {
        var paragraph = cell.Descendants<ParagraphBlock>().FirstOrDefault();
        var raw = paragraph != null ? Slice(paragraph, src) : Slice(cell, src);
        return raw.Replace('\n', ' ').Trim();
    }

    /// <summary>Strip one leading <c>&gt;</c> quote marker (and its optional single space) from every line, leaving
    /// the quote's inner markdown to be re-parsed. A lazy-continuation line without a marker is kept as-is.</summary>
    private static string StripQuoteMarkers(string raw)
    {
        var lines = raw.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var j = 0;
            while (j < line.Length && line[j] == ' ')
                j++;
            if (j < line.Length && line[j] == '>')
            {
                j++;
                if (j < line.Length && line[j] == ' ')
                    j++;
                lines[i] = line[j..];
            }
        }
        return string.Join('\n', lines);
    }

    /// <summary>A list item's own text is its first paragraph; every following child block — chiefly a nested
    /// sub-list — becomes the item's <c>children</c>, so an indented list lands as a real hierarchy.</summary>
    private static NotionBlock ConvertListItem(ListItemBlock item, bool ordered, string src)
    {
        var (text, children) = SplitItem(item, src);
        var body = Body(text);
        return new NotionBlock
        {
            Type = ordered ? "numbered_list_item" : "bulleted_list_item",
            BulletedListItem = ordered ? null : body,
            NumberedListItem = ordered ? body : null,
            Children = children.Count > 0 ? children : null,
        };
    }

    /// <summary>Split a list item into its first-paragraph text and its remaining child blocks. The paragraph text
    /// is sliced from its span CLAMPED to the start of the next sibling block: Markdig's paragraph span can
    /// over-cover a following sibling (an indented pipe table stays inside the paragraph's span while ALSO parsing as
    /// its own Table child), and slicing the raw span would carry that block's text into the item's rich_text AND
    /// emit the block, duplicating it and growing the body every normalization. Continuation-line indentation is
    /// then cleaned as before.</summary>
    private static (string Text, List<NotionBlock> Children) SplitItem(ListItemBlock item, string src)
    {
        var blocks = new List<Block>(item);
        var firstParagraph = blocks.FindIndex(b => b is ParagraphBlock);
        var text = "";
        var children = new List<NotionBlock>();
        for (var i = 0; i < blocks.Count; i++)
        {
            var clampEnd = i + 1 < blocks.Count ? blocks[i + 1].Span.Start : -1;
            if (i == firstParagraph)
                text = CleanText(ClampedSlice(blocks[i], clampEnd, src));
            else
                children.AddRange(Convert(blocks[i], src, nested: true, clampEnd));
        }
        return (text, children);
    }

    /// <summary>Slice a block's source span, but end no later than <paramref name="clampEnd"/> (the next sibling
    /// block's start, or -1 for none). Clamps ONLY when the next sibling begins before this span ends — i.e. Markdig
    /// over-extended the span across a block it did not consume — trimming the trailing whitespace/newlines the clamp
    /// exposes; an un-over-covered block keeps its raw span verbatim (the prior behaviour, so a hard line break's
    /// trailing spaces are untouched).</summary>
    private static string ClampedSlice(Block block, int clampEnd, string src)
    {
        var start = block.Span.Start;
        if (start < 0 || start >= src.Length)
            return "";
        var end = block.Span.End + 1;
        if (clampEnd >= 0 && clampEnd < end)
            return src[start..clampEnd].TrimEnd('\n', ' ', '\t');
        return src[start..Math.Min(end, src.Length)];
    }

    /// <summary>Whether an ordered list's items are exactly 1, 2, 3, …, n — the only shape that round-trips as
    /// numbered items, since render re-sequences from 1.</summary>
    private static bool IsSequentialFromOne(ListBlock list)
    {
        var expected = 1;
        foreach (var child in list)
        {
            if (child is not ListItemBlock item || item.Order != expected)
                return false;
            expected++;
        }
        return true;
    }

    /// <summary>Render a non-1..n ordered list as verbatim paragraph lines (old-converter behavior): each item is a
    /// paragraph carrying its original marker (<c>3. </c>) and text, with any nested blocks kept as children.</summary>
    private static List<NotionBlock> VerbatimOrderedItems(ListBlock list, string src)
    {
        var result = new List<NotionBlock>();
        foreach (var child in list)
        {
            if (child is not ListItemBlock item)
                continue;
            var (text, children) = SplitItem(item, src);
            var marker = item.Order + list.OrderedDelimiter.ToString() + " ";
            result.Add(new NotionBlock
            {
                Type = "paragraph",
                Paragraph = Body(marker + text),
                Children = children.Count > 0 ? children : null,
            });
        }
        return result;
    }

    private static void Render(StringBuilder sb, IReadOnlyList<NotionBlock> blocks, string prefix)
    {
        // Numbered items are re-sequenced from 1 within each contiguous run at this level: Notion stores no
        // ordinal on a numbered_list_item, so on read we can only count, and on write a 1,2,3… list round-trips.
        var number = 0;
        foreach (var block in blocks)
        {
            // A child_page block is a nested sub-page (DR 033), not body content — never rendered into a body.
            if (block.Type == "child_page")
                continue;
            // Quotes and tables own their children (quote continuations, table rows), so they render whole here.
            // Quotes, tables AND the [!missing] marker (itself a quote on re-parse) must be fenced by blank lines:
            // a block rendered flush against a quote is swallowed as a lazy continuation on the next parse, and a
            // pipe table is only recognised when a blank line precedes it — either way the round-trip would drift.
            if (!IsLineRendered(block.Type))
            {
                EnsureBlankSeparator(sb, prefix);
                if (block.Type == "quote")
                    RenderQuote(sb, block, prefix);
                else if (block.Type == "table")
                    RenderTable(sb, block, prefix);
                else
                    sb.Append(prefix).Append(BlockToLine(block, 0)).Append('\n'); // unsupported type → [!missing] marker
                EnsureBlankSeparator(sb, prefix);
                number = 0;
                continue;
            }
            number = block.Type == "numbered_list_item" ? number + 1 : 0;
            // Prefix EVERY physical line, not just the first: a multi-line block (a code fence, a soft-wrapped
            // paragraph) nested under a list item must carry the indent on all its lines, or the un-indented
            // continuation lines break out of the item and the body stops round-tripping.
            foreach (var physical in BlockToLine(block, number).Split('\n'))
                sb.Append(prefix).Append(physical).Append('\n');
            if (block.Children is { Count: > 0 } children)
                Render(sb, children, prefix + new string(' ', MarkerWidth(block, number)));
        }
    }

    /// <summary>Block types that render as one (or a few) plain indented markdown lines via <see cref="BlockToLine"/>,
    /// as opposed to the blank-fenced whole-block path (quote, table) or a swallow-prone unsupported-type marker.
    /// child_page is filtered before this set is consulted.</summary>
    private static readonly HashSet<string> LineRenderedTypes = new(StringComparer.Ordinal)
    {
        "paragraph", "heading_1", "heading_2", "heading_3", "bulleted_list_item", "numbered_list_item", "code",
    };

    private static bool IsLineRendered(string type) => LineRenderedTypes.Contains(type);

    /// <summary>Ensure the buffer ends with a blank line separator (an empty line) so a following block cannot merge
    /// into a quote or table. A no-op at the very start and when a blank line is already present, so it never stacks
    /// blanks — keeping the render a fixed point.</summary>
    private static void EnsureBlankSeparator(StringBuilder sb, string prefix)
    {
        if (sb.Length == 0)
            return;
        if (sb.Length >= 2 && sb[^1] == '\n' && sb[^2] == '\n')
            return;
        sb.Append(prefix).Append('\n');
    }

    /// <summary>Render a quote block back to a markdown blockquote. The first paragraph (the quote's own rich_text)
    /// and each child block are rendered independently and joined with a blank line, then every line is prefixed
    /// with <c>&gt; </c> (a blank line becomes a bare <c>&gt;</c>) — so a multi-paragraph quote keeps its separators
    /// and round-trips exactly.</summary>
    private static void RenderQuote(StringBuilder sb, NotionBlock block, string prefix)
    {
        var inner = new List<NotionBlock>();
        if (block.Quote is { } quote && NotionRichText.Flatten(quote.RichText).Length > 0)
            inner.Add(new NotionBlock { Type = "paragraph", Paragraph = quote });
        if (block.Children is { Count: > 0 } children)
            inner.AddRange(children);

        var parts = inner.Select(b =>
        {
            var buffer = new StringBuilder();
            Render(buffer, [b], "");
            return buffer.ToString().TrimEnd('\n');
        });
        var content = string.Join("\n\n", parts);
        if (content.Length == 0)
        {
            sb.Append(prefix).Append('>').Append('\n');
            return;
        }
        foreach (var line in content.Split('\n'))
            sb.Append(prefix).Append(line.Length == 0 ? ">" : "> " + line).Append('\n');
    }

    /// <summary>Render a table block back to a GFM pipe table: the first row plus a <c>| --- |</c> separator, then
    /// the remaining rows, every row padded to <c>table_width</c>. Rows come from the table payload's children on
    /// write and from the block's generic children on read (Notion returns them via GetBlockChildren).</summary>
    private static void RenderTable(StringBuilder sb, NotionBlock block, string prefix)
    {
        var rows = block.Table?.Children ?? block.Children ?? [];
        var width = block.Table?.TableWidth is { } w and > 0
            ? w
            : rows.Count == 0 ? 0 : rows.Max(r => r.TableRow?.Cells.Count ?? 0);
        if (rows.Count == 0 || width == 0)
            return;

        for (var i = 0; i < rows.Count; i++)
        {
            sb.Append(prefix).Append(RenderRow(rows[i], width)).Append('\n');
            if (i == 0)
                sb.Append(prefix).Append("| ").Append(string.Join(" | ", Enumerable.Repeat("---", width)))
                    .Append(" |").Append('\n');
        }
    }

    private static string RenderRow(NotionBlock row, int width)
    {
        var cells = row.TableRow?.Cells ?? [];
        var texts = Enumerable.Range(0, width).Select(i => i < cells.Count ? NotionRichText.Flatten(cells[i]) : "");
        return "| " + string.Join(" | ", texts) + " |";
    }

    private static string BlockToLine(NotionBlock block, int number) => block.Type switch
    {
        "bulleted_list_item" => "- " + Text(block.BulletedListItem),
        "numbered_list_item" => number + ". " + Text(block.NumberedListItem),
        "code" => Fence(block.Code),
        "paragraph" => Text(block.Paragraph),
        _ => HeadingOrMissingLine(block),
    };

    private static string HeadingOrMissingLine(NotionBlock block) => block.Type switch
    {
        "heading_1" => "# " + Text(block.Heading1),
        "heading_2" => "## " + Text(block.Heading2),
        "heading_3" => "### " + Text(block.Heading3),
        // An unsupported Notion block type (image, divider, callout, …) renders as a visible marker instead of a
        // silent drop, so lost content is deterministic text that hashes stably (ns-7 item 6, ns-8 depends on it).
        _ => "> [!missing] " + block.Type,
    };

    /// <summary>The indentation a child list needs to nest under its parent item — the width of the parent's list
    /// marker (<c>"- "</c> = 2, <c>"1. "</c> = 3), the CommonMark minimum, so the rendered indent re-parses as a
    /// child rather than a sibling.
    /// <para>KNOWN EDGE: a non-1..n ordered list is emitted verbatim as <c>paragraph</c> blocks (see
    /// <see cref="VerbatimOrderedItems"/>), not numbered_list_item, so a child under such an item uses the default
    /// width 2 even though its rendered <c>"N. "</c> marker is 3+ wide. The child is then under-indented and
    /// un-nests one level on re-parse, so a shape like <c>"3. text\n   - sub"</c> is NOT a pass-1 fixed point — it
    /// converges after a second normalization (the sub-item becomes a top-level sibling). No synced record contains
    /// a non-1 ordered run with a nested child, so this never fires in practice; pinned by a converter test.</para></summary>
    private static int MarkerWidth(NotionBlock block, int number) =>
        block.Type == "numbered_list_item" ? number.ToString().Length + 2 : 2;

    /// <summary>The verbatim source text a Markdig block spans — kept unparsed so inline markers survive a
    /// round-trip (structure-only sprint). An empty or unset span yields the empty string.</summary>
    private static string Slice(Block block, string src)
    {
        var span = block.Span;
        if (span.Start < 0 || span.Length <= 0 || span.Start >= src.Length)
            return "";
        return src.Substring(span.Start, Math.Min(span.Length, src.Length - span.Start));
    }

    /// <summary>Strip the leading indentation off a multi-line text block's continuation lines. A paragraph or
    /// list-item text nested in a list carries the source's continuation indentation inside its span; without this
    /// the per-line render prefix would double it, and each normalization would add another level (runaway indent).
    /// The first line already starts at the content column, so only lines after it are trimmed; prose continuation
    /// whitespace is never semantic.</summary>
    private static string CleanText(string raw)
    {
        var newline = raw.IndexOf('\n');
        if (newline < 0)
            return raw;
        var lines = raw.Split('\n');
        for (var i = 1; i < lines.Length; i++)
            lines[i] = lines[i].TrimStart(' ', '\t');
        return string.Join('\n', lines);
    }

    private static string StripHeadingMarker(string raw)
    {
        var i = 0;
        while (i < raw.Length && raw[i] == '#')
            i++;
        while (i < raw.Length && (raw[i] == ' ' || raw[i] == '\t'))
            i++;
        return raw[i..];
    }

    private static string TrimTrailingNewline(string s) => s.EndsWith('\n') ? s[..^1] : s;

    private static NotionBlockBody Body(string text) => new()
    {
        RichText = NotionRichText.Of(text),
    };

    private static NotionBlock CodeBlock(string code, string language) => new()
    {
        Type = "code",
        Code = new NotionBlockBody { RichText = NotionRichText.Of(code), Language = NormalizeLanguage(language) },
    };

    private static string Text(NotionBlockBody? body) => NotionRichText.Flatten(body?.RichText);

    private static string Fence(NotionBlockBody? body)
    {
        var lang = body?.Language is { Length: > 0 } and not "plain text" ? body.Language : "";
        return "```" + lang + "\n" + Text(body) + "\n```";
    }

    /// <summary>Notion's code block accepts only a fixed language vocabulary and rejects anything else with a
    /// 400 that aborts the whole reconcile (e.g. the ubiquitous "csharp" fence — Notion spells it "c#"). Map
    /// the common markdown aliases to Notion's spelling, then fall back to "plain text" for any language Notion
    /// does not accept, so an unrecognised fence degrades to an un-highlighted block instead of wedging the
    /// sync. An empty fence defaults to "plain text". A repo body's original fence tag is not rewritten — the
    /// adapter's NormalizeBody re-runs this mapping so the round-trip comparison matches Notion's echo.</summary>
    private static string NormalizeLanguage(string language)
    {
        var lang = language.Trim().ToLowerInvariant();
        if (lang.Length == 0)
            return "plain text";
        if (LanguageAliases.TryGetValue(lang, out var canonical))
            lang = canonical;
        return NotionLanguages.Contains(lang) ? lang : "plain text";
    }

    /// <summary>Common markdown / highlighter fence tags that differ from Notion's spelling.</summary>
    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.Ordinal)
    {
        ["csharp"] = "c#", ["cs"] = "c#",
        ["cpp"] = "c++",
        ["fsharp"] = "f#",
        ["py"] = "python",
        ["js"] = "javascript", ["node"] = "javascript",
        ["ts"] = "typescript",
        ["yml"] = "yaml",
        ["sh"] = "shell", ["zsh"] = "shell", ["console"] = "shell", ["shell-session"] = "shell",
        ["pwsh"] = "powershell", ["ps1"] = "powershell",
        ["md"] = "markdown",
        ["text"] = "plain text", ["plaintext"] = "plain text", ["plain"] = "plain text", ["txt"] = "plain text",
        ["dockerfile"] = "docker",
        ["objc"] = "objective-c",
        ["vb"] = "visual basic",
        ["asm"] = "assembly",
        ["tex"] = "latex",
        ["golang"] = "go", ["rs"] = "rust", ["kt"] = "kotlin", ["rb"] = "ruby",
    };

    /// <summary>Notion's accepted code-block languages (Notion-Version 2026-03-11). A language outside this set
    /// is rejected with a validation_error, so NormalizeLanguage maps to it or falls back to "plain text".</summary>
    private static readonly HashSet<string> NotionLanguages = new(StringComparer.Ordinal)
    {
        "abap", "abc", "agda", "arduino", "ascii art", "assembly", "bash", "basic", "bnf", "c", "c#", "c++",
        "clojure", "coffeescript", "coq", "css", "dart", "dhall", "diff", "docker", "ebnf", "elixir", "elm",
        "erlang", "f#", "flow", "fortran", "gherkin", "glsl", "go", "graphql", "groovy", "haskell", "hcl",
        "html", "idris", "java", "javascript", "json", "julia", "kotlin", "latex", "less", "lisp", "livescript",
        "llvm ir", "lua", "makefile", "markdown", "markup", "matlab", "mathematica", "mermaid", "nix",
        "notion formula", "objective-c", "ocaml", "pascal", "perl", "php", "plain text", "powershell", "prolog",
        "protobuf", "purescript", "python", "r", "racket", "reason", "ruby", "rust", "sass", "scala", "scheme",
        "scss", "shell", "smalltalk", "solidity", "sql", "swift", "toml", "typescript", "vb.net", "verilog",
        "vhdl", "visual basic", "webassembly", "xml", "yaml", "java/c/c++/c#",
    };
}
