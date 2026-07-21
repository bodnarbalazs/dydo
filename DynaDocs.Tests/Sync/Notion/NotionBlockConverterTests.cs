namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

public class NotionBlockConverterTests
{
    [Fact]
    public void Paragraphs_RoundTrip()
    {
        const string md = "first line\nsecond line";
        var back = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(md));
        Assert.Equal(md, back);
    }

    [Fact]
    public void HeadingsAndBullets_RoundTrip()
    {
        const string md = "# Title\n## Sub\n### Small\n- one\n- two\nbody";
        var back = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(md));
        Assert.Equal(md, back);
    }

    [Fact]
    public void CodeFence_RoundTrips_WithLanguage()
    {
        const string md = "```python\nprint(1)\n```";
        var back = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(md));
        Assert.Equal(md, back);
    }

    [Fact]
    public void CodeFence_NoLanguage_RoundTripsAsBareFence()
    {
        const string md = "```\nraw\n```";
        var back = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(md));
        Assert.Equal(md, back);
    }

    [Fact]
    public void BlankLines_AreDropped_NotPreservedAsBlocks()
    {
        var blocks = NotionBlockConverter.ToBlocks("a\n\n\nb");
        Assert.Equal(2, blocks.Count);
        Assert.Equal("a\nb", NotionBlockConverter.FromBlocks(blocks));
    }

    [Fact]
    public void ToBlocks_AsteriskBullet_BecomesBulletedListItem()
    {
        var blocks = NotionBlockConverter.ToBlocks("* item");
        Assert.Equal("bulleted_list_item", blocks.Single().Type);
    }

    [Fact]
    public void ToBlocks_TitleLine_ProducesHeading1Type()
    {
        var blocks = NotionBlockConverter.ToBlocks("# Heading");
        var block = blocks.Single();
        Assert.Equal("heading_1", block.Type);
        Assert.Equal("Heading", NotionRichText.Flatten(block.Heading1!.RichText));
    }

    [Fact]
    public void UnterminatedFence_StillEmitsCodeBlock()
    {
        var blocks = NotionBlockConverter.ToBlocks("```\nunclosed");
        Assert.Equal("code", blocks.Single().Type);
    }

    [Fact]
    public void NestedBulletList_ConvertsToChildrenHierarchy_AndRoundTrips()
    {
        const string md = "- a\n- b\n  - b1\n  - b2\n    - b1x";
        var blocks = NotionBlockConverter.ToBlocks(md);

        // Two top-level items; the nested sub-lists land as block children, not flattened siblings.
        Assert.Equal(["a", "b"], blocks.Select(b => NotionRichText.Flatten(b.BulletedListItem!.RichText)));
        var b = blocks[1];
        Assert.Equal(["b1", "b2"], b.Children!.Select(c => NotionRichText.Flatten(c.BulletedListItem!.RichText)));
        Assert.Equal("b1x", NotionRichText.Flatten(b.Children![1].Children!.Single().BulletedListItem!.RichText));

        Assert.Equal(md, NotionBlockConverter.FromBlocks(blocks));
    }

    [Fact]
    public void TwoDeepList_NestsInOneChildrenLevel()
    {
        var blocks = NotionBlockConverter.ToBlocks("- a\n  - b");
        var top = Assert.Single(blocks);
        var child = Assert.Single(top.Children!);
        Assert.Equal("bulleted_list_item", child.Type);
        Assert.Equal("b", NotionRichText.Flatten(child.BulletedListItem!.RichText));
        Assert.Null(child.Children); // b is a leaf — the nesting is exactly one level
    }

    [Fact]
    public void NumberedList_ConvertsToNumberedItems_AndRoundTrips()
    {
        const string md = "1. one\n2. two\n3. three";
        var blocks = NotionBlockConverter.ToBlocks(md);
        Assert.All(blocks, b => Assert.Equal("numbered_list_item", b.Type));
        Assert.Equal(md, NotionBlockConverter.FromBlocks(blocks));
    }

    [Fact]
    public void NestedNumberedUnderBullet_RoundTrips()
    {
        const string md = "- a\n  1. one\n  2. two";
        Assert.Equal(md, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(md)));
    }

    [Fact]
    public void NestedBulletUnderNumbered_RoundTrips_WithMarkerWidthIndent()
    {
        // A child under a numbered item indents to the marker width ("1. " = 3), the CommonMark minimum, so it
        // re-parses as a child rather than a sibling.
        const string md = "1. one\n   - sub";
        Assert.Equal(md, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(md)));
    }

    [Fact]
    public void InlineMarkers_KeptVerbatim_AsPlainRuns()
    {
        // Structure-only sprint: inline markup is not parsed into annotations, it survives as literal text.
        const string md = "para **bold** and `code` and [x](y)";
        var blocks = NotionBlockConverter.ToBlocks(md);
        Assert.Equal(md, NotionRichText.Flatten(Assert.Single(blocks).Paragraph!.RichText));
    }

    [Theory]
    [InlineData("csharp", "c#")]   // the alias that wedged a live reconcile — Notion spells it "c#"
    [InlineData("cs", "c#")]
    [InlineData("pwsh", "powershell")]
    [InlineData("text", "plain text")]
    [InlineData("CSharp", "c#")]   // case-insensitive
    [InlineData("node", "javascript")] // ns-7: the one genuinely missing alias from the survey list
    [InlineData("bash", "bash")]   // already valid — passes through unchanged
    [InlineData("c#", "c#")]
    [InlineData("cobol", "plain text")] // unknown → fallback, so Notion never gets an invalid language again
    public void CodeFence_Language_IsMappedToNotionVocabulary(string fence, string expected)
    {
        var blocks = NotionBlockConverter.ToBlocks($"```{fence}\ncode\n```");
        var code = Assert.Single(blocks);
        Assert.Equal("code", code.Type);
        Assert.Equal(expected, code.Code!.Language);
    }

    // ── ns-7 item 1: per-block rich_text cap, overflow to sibling paragraphs ──────────────────────

    [Fact]
    public void LongParagraph_OverflowsIntoSiblingParagraphs_NeverTruncates()
    {
        // 101 runs of 2000 chars (the per-run cap) — one past the ≤100-items-per-block limit — must split
        // across sibling paragraph blocks rather than land as a single over-cap block Notion would 400.
        var text = new string('a', 2000 * 101);
        var blocks = NotionBlockConverter.ToBlocks(text);

        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.Equal("paragraph", b.Type));
        Assert.Equal(100, blocks[0].Paragraph!.RichText.Count);
        Assert.Single(blocks[1].Paragraph!.RichText);
        // No character is dropped: the sibling blocks concatenate back to the whole original text.
        var rebuilt = string.Concat(blocks.Select(b => NotionRichText.Flatten(b.Paragraph!.RichText)));
        Assert.Equal(text, rebuilt);
    }

    [Fact]
    public void NormalParagraph_StaysOneBlock_WithinCap()
    {
        var blocks = NotionBlockConverter.ToBlocks("just a normal line");
        Assert.Single(Assert.Single(blocks).Paragraph!.RichText);
    }

    [Fact]
    public void GiantSingleLineParagraph_DoesNotConverge_DocumentedInstability()
    {
        // DOCUMENTATION, not an endorsement: a single unbroken line past ~100 runs overflows to sibling paragraphs,
        // and the join newline re-parses into the paragraph at a shifted boundary — so norm¹..⁴ stay pairwise
        // distinct. Unreachable in practice (no synced record nears 200KB in one line); tracked as issue 0298.
        var giant = new string('a', 2000 * 101);
        var n1 = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(giant));
        var n2 = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(n1));
        var n3 = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(n2));
        Assert.NotEqual(giant, n1);
        Assert.NotEqual(n1, n2);
        Assert.NotEqual(n2, n3);
    }

    // ── ns-7 item 3: tables ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Table_ConvertsToTableBlock_WithWidthHeaderAndCells()
    {
        var blocks = NotionBlockConverter.ToBlocks("| a | b |\n|---|---|\n| 1 | 2 |");
        var table = Assert.Single(blocks);
        Assert.Equal("table", table.Type);
        Assert.Equal(2, table.Table!.TableWidth);
        Assert.True(table.Table.HasColumnHeader);
        Assert.Equal(2, table.Table.Children!.Count); // header row + one data row
        Assert.Equal(["a", "b"], table.Table.Children[0].TableRow!.Cells.Select(c => NotionRichText.Flatten(c)));
        Assert.Equal(["1", "2"], table.Table.Children[1].TableRow!.Cells.Select(c => NotionRichText.Flatten(c)));
    }

    [Fact]
    public void Table_RoundTripsToCanonicalForm_AndIsIdempotent()
    {
        const string canonical = "| a | b |\n| --- | --- |\n| 1 | 2 |";
        var once = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks("| a | b |\n|---|---|\n| 1 | 2 |"));
        Assert.Equal(canonical, once);
        Assert.Equal(once, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(once)));
    }

    [Fact]
    public void RaggedTable_PadsShortRowsToWidestRow()
    {
        var blocks = NotionBlockConverter.ToBlocks("| a | b | c |\n|---|---|---|\n| 1 | 2 |");
        var table = Assert.Single(blocks);
        Assert.Equal(3, table.Table!.TableWidth); // widest row
        Assert.Equal(3, table.Table.Children![1].TableRow!.Cells.Count); // short data row padded to width
        // The padded empty cell renders as an empty column and re-parses unchanged.
        var rendered = NotionBlockConverter.FromBlocks(blocks);
        Assert.Equal("| a | b | c |\n| --- | --- | --- |\n| 1 | 2 |  |", rendered);
        Assert.Equal(rendered, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(rendered)));
    }

    [Fact]
    public void TableCell_KeepsInlineMarkersVerbatim()
    {
        var blocks = NotionBlockConverter.ToBlocks("| **bold** | `code` |\n|---|---|\n| x | y |");
        var header = Assert.Single(blocks).Table!.Children![0];
        Assert.Equal(["**bold**", "`code`"], header.TableRow!.Cells.Select(c => NotionRichText.Flatten(c)));
    }

    // ── ns-7 item 4: blockquotes ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Blockquote_FirstParagraphFillsQuoteRichText_NotEmpty()
    {
        var blocks = NotionBlockConverter.ToBlocks("> quoted text");
        var quote = Assert.Single(blocks);
        Assert.Equal("quote", quote.Type);
        Assert.Equal("quoted text", NotionRichText.Flatten(quote.Quote!.RichText));
        Assert.Null(quote.Children);
        Assert.Equal("> quoted text", NotionBlockConverter.FromBlocks(blocks));
    }

    [Fact]
    public void MultiParagraphBlockquote_FirstInRichText_RestAsChildren_RoundTrips()
    {
        const string md = "> para one\n>\n> para two";
        var blocks = NotionBlockConverter.ToBlocks(md);
        var quote = Assert.Single(blocks);
        Assert.Equal("para one", NotionRichText.Flatten(quote.Quote!.RichText));
        var child = Assert.Single(quote.Children!);
        Assert.Equal("para two", NotionRichText.Flatten(child.Paragraph!.RichText));
        Assert.Equal(md, NotionBlockConverter.FromBlocks(blocks));
    }

    [Fact]
    public void Blockquote_FollowedByParagraph_KeepsBlankSeparator_RoundTrips()
    {
        // Regression: a paragraph rendered flush against a quote is swallowed as a lazy continuation on re-parse.
        const string md = "> a note\n\nplain paragraph after";
        var back = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(md));
        Assert.Equal(md, back);
    }

    // ── ns-7 item 5: heading clamp ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("#### four")]
    [InlineData("##### five")]
    [InlineData("###### six")]
    public void HeadingsFourToSix_ClampToHeading3(string md)
    {
        var block = Assert.Single(NotionBlockConverter.ToBlocks(md));
        Assert.Equal("heading_3", block.Type);
    }

    [Fact]
    public void ClampedHeading_RendersAsHeading3_AndIsThenAFixedPoint()
    {
        var once = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks("##### deep"));
        Assert.Equal("### deep", once);
        Assert.Equal(once, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(once)));
    }

    // ── ns-7 item 6: read-side [!missing] markers ─────────────────────────────────────────────────

    [Fact]
    public void UnsupportedBlockType_RendersVisibleMissingMarker_NotSilentDrop()
    {
        var rendered = NotionBlockConverter.FromBlocks([new NotionBlock { Type = "image" }]);
        Assert.Equal("> [!missing] image", rendered);
    }

    [Fact]
    public void MissingMarker_RoundTripsDeterministically()
    {
        // The marker re-parses as a quote and renders identically, so a dropped block hashes stably (ns-8).
        const string marker = "> [!missing] divider";
        Assert.Equal(marker, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(marker)));
    }

    private static readonly NotionBlock Image = new() { Type = "image" };
    private static readonly NotionBlock Divider = new() { Type = "divider" };
    private static NotionBlock Para(string text) =>
        new() { Type = "paragraph", Paragraph = new NotionBlockBody { RichText = NotionRichText.Of(text) } };

    [Fact]
    public void MissingMarker_FollowedByParagraph_IsBlankFenced_StrictFixedPoint()
    {
        // Regression (major 2): a marker rendered flush against the next paragraph swallows it as a lazy quote
        // continuation on re-parse. Blank-fencing the marker makes the render a strict fixed point.
        var rendered = NotionBlockConverter.FromBlocks([Image, Para("plain paragraph")]);
        Assert.Equal("> [!missing] image\n\nplain paragraph", rendered);
        Assert.Equal(rendered, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(rendered)));
    }

    [Fact]
    public void Paragraph_FollowedByMissingMarker_IsBlankFenced_StrictFixedPoint()
    {
        var rendered = NotionBlockConverter.FromBlocks([Para("plain paragraph"), Image]);
        Assert.Equal("plain paragraph\n\n> [!missing] image", rendered);
        Assert.Equal(rendered, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(rendered)));
    }

    [Fact]
    public void TwoMissingMarkers_ThenParagraph_AreBlankFenced_StrictFixedPoint()
    {
        var rendered = NotionBlockConverter.FromBlocks([Image, Divider, Para("plain paragraph")]);
        Assert.Equal("> [!missing] image\n\n> [!missing] divider\n\nplain paragraph", rendered);
        Assert.Equal(rendered, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(rendered)));
    }

    [Fact]
    public void TableNestedInListItemWithoutBlankLine_IsStrictFixedPoint_NoRunawayGrowth()
    {
        // Regression (major 3): the item paragraph's span over-covered the indented table lines, so the table text
        // landed in the item's rich_text AND as a table child — each normalization appended another copy. The item
        // keeps only "item"; the table is its single child, and the shape is a strict fixed point (no growth).
        const string md = "- item\n  | a | b |\n  |---|---|\n  | 1 | 2 |";
        var item = Assert.Single(NotionBlockConverter.ToBlocks(md));
        Assert.Equal("item", NotionRichText.Flatten(item.BulletedListItem!.RichText));
        Assert.Equal("table", Assert.Single(item.Children!).Type);

        var once = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(md));
        Assert.Equal(once, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(once)));
        Assert.DoesNotContain("| a | b |\n  | --- | --- |\n  | 1 | 2 |\n  | a | b |", once); // no duplicated table
    }

    [Fact]
    public void TopLevelParagraphFlushAgainstTable_IsStrictFixedPoint_NoRunawayGrowth()
    {
        // Regression (re-review major): the same span over-cover survived at the document walk — a paragraph flush
        // against a table (an ordinary human edit, no blank line) duplicated the table every pass, unbounded growth.
        // The paragraph keeps only its own text; the table is a separate block; the shape is a strict fixed point.
        const string md = "intro para\n| a | b |\n|---|---|\n| 1 | 2 |";
        var blocks = NotionBlockConverter.ToBlocks(md);
        Assert.Equal(["paragraph", "table"], blocks.Select(b => b.Type));
        Assert.Equal("intro para", NotionRichText.Flatten(blocks[0].Paragraph!.RichText));

        var once = NotionBlockConverter.FromBlocks(blocks);
        Assert.Equal(once, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(once)));
        var third = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(once));
        Assert.Equal(once.Length, third.Length); // no growth across passes
    }

    [Fact]
    public void TopLevelParagraphFlushAgainstBlockquote_IsStrictFixedPoint()
    {
        // A blockquote interrupts the paragraph cleanly (no over-cover), but pin it so the walk stays honest.
        const string md = "intro para\n> quoted line";
        var once = NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(md));
        Assert.Equal(once, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(once)));
        Assert.Equal(["paragraph", "quote"], NotionBlockConverter.ToBlocks(md).Select(b => b.Type));
    }

    // ── ns-7 read-side shapes (blocks as Notion returns them, never produced by ToBlocks) ─────────

    [Fact]
    public void Table_ReadFromNotion_RendersRowsFromBlockChildren()
    {
        // On read Notion returns the rows via GetBlockChildren, so they sit on the block's generic Children while
        // table_width/has_column_header sit on the table payload — the renderer must read from there.
        var table = new NotionBlock
        {
            Type = "table",
            Table = new NotionTable { TableWidth = 2, HasColumnHeader = true },
            Children =
            [
                Row("a", "b"),
                Row("1", "2"),
            ],
        };
        Assert.Equal("| a | b |\n| --- | --- |\n| 1 | 2 |", NotionBlockConverter.FromBlocks([table]));
    }

    [Fact]
    public void Table_WithoutTablePayload_DerivesWidthFromRows()
    {
        var table = new NotionBlock { Type = "table", Children = [Row("a", "b"), Row("1", "2")] };
        Assert.Equal("| a | b |\n| --- | --- |\n| 1 | 2 |", NotionBlockConverter.FromBlocks([table]));
    }

    private static NotionBlock Row(params string[] cells) => new()
    {
        Type = "table_row",
        TableRow = new NotionTableRow { Cells = cells.Select(NotionRichText.Of).ToList() },
    };

    [Fact]
    public void Blockquote_StartingWithList_HasEmptyRichText_AllContentAsChildren()
    {
        const string md = "> - item";
        var quote = Assert.Single(NotionBlockConverter.ToBlocks(md));
        Assert.Equal("quote", quote.Type);
        Assert.Equal("", NotionRichText.Flatten(quote.Quote!.RichText));
        Assert.Equal("bulleted_list_item", Assert.Single(quote.Children!).Type);
        Assert.Equal(md, NotionBlockConverter.FromBlocks([quote]));
    }

    [Fact]
    public void EmptyQuoteBlock_RendersBareMarker()
    {
        var quote = new NotionBlock { Type = "quote", Quote = new NotionBlockBody() };
        Assert.Equal(">", NotionBlockConverter.FromBlocks([quote]));
    }

    [Fact]
    public void Table_WithEmptyLeadingCell_RoundTrips()
    {
        const string md = "|  | b |\n|---|---|\n| x | y |";
        var table = Assert.Single(NotionBlockConverter.ToBlocks(md));
        Assert.Equal(["", "b"], table.Table!.Children![0].TableRow!.Cells.Select(c => NotionRichText.Flatten(c)));
        var rendered = NotionBlockConverter.FromBlocks([table]);
        Assert.Equal(rendered, NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(rendered)));
    }
}
