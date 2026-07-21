namespace DynaDocs.Tests.Sync.Notion.Live;

using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// LIVE (ns-9, ns-7): a body carrying a table, a quote, and a block the converter does not support round-trips
/// through the real read path — the table and quote render back as themselves, and the unsupported block reads
/// back as a visible <c>[!missing]</c> marker rather than a silent drop. The page is created via Notion's native
/// markdown import (which produces real table/quote/divider blocks), then read back through the spine converter
/// (<c>GetBlockChildren</c> → <c>FromBlocks</c>) — the exact combination the fake cannot reproduce.
/// </summary>
[Trait("Category", "notion-live")]
public sealed class NotionLiveBodyRoundTripTests : NotionLiveTestBase
{
    [NotionLiveFact]
    public void TableQuoteAndUnsupportedBlock_RoundTripThroughConverter()
    {
        // A thematic break imports as a Notion `divider` — a block type the converter renders as [!missing].
        const string markdown = "| Name | Role |\n| --- | --- |\n| ann | lead |\n\n> a quoted line\n\n---\n";
        var page = Client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(ChildPageId),
            Properties = new() { ["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of(ScratchName()) } },
            Markdown = markdown,
        });

        var readBack = ReadBodyMarkdown(page.Id);
        Assert.Contains("| Name | Role |", readBack);   // table survives
        Assert.Contains("> a quoted line", readBack);   // quote survives
        Assert.Contains("[!missing]", readBack);         // the divider surfaces as a visible marker, not a drop
    }
}
