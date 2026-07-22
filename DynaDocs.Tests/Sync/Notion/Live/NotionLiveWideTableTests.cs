namespace DynaDocs.Tests.Sync.Notion.Live;

using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// LIVE (ns-9; issue 0299 F19): a table with more rows than Notion's 100-per-children-array cap ROW-BATCHES — the
/// create/first append carries the first 100 rows inline, and the remainder append to the returned table block id
/// via PATCH /blocks/{table_id}/children in ≤100 chunks (live-confirmed 2026-07-22 that appending table_row
/// children to an existing table works). This retargets the old <c>GuardTableWidth</c> loud-abort pin: batching is
/// now implemented, so the assertion is that all rows LAND, not that the guard throws.
/// </summary>
[Trait("Category", "notion-live")]
public sealed class NotionLiveWideTableTests : NotionLiveTestBase
{
    [NotionLiveFact]
    public void TableOver100Rows_RowBatches_AllRowsLand()
    {
        // Header + 149 data rows = 150 table_row children, past the 100-per-array cap — must land via batching.
        var lines = new List<string> { "| a | b |", "| --- | --- |" };
        for (var i = 0; i < 149; i++)
            lines.Add($"| {i} | x |");
        var blocks = NotionBlockConverter.ToBlocks(string.Join("\n", lines));

        var page = Client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(ChildPageId),
            Properties = new() { ["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of(ScratchName()) } },
        });

        NotionBlockAppender.AppendForest(Client, page.Id, blocks);

        var table = Assert.Single(Client.GetBlockChildren(page.Id));
        Assert.Equal(150, Client.GetBlockChildren(table.Id!).Count); // header + 149 rows all landed via row-batching
    }
}
