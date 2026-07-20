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
}
