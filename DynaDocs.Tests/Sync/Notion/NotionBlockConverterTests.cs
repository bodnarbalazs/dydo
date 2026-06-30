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
}
