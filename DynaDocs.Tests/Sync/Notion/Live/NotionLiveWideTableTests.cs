namespace DynaDocs.Tests.Sync.Notion.Live;

using DynaDocs.Sync.Notion;

/// <summary>
/// LIVE (ns-9, ns-7 <c>GuardTableWidth</c>): a table with more rows than Notion's 100-per-children-array cap
/// fails loudly through our own guard BEFORE any request reaches Notion, rather than shipping a payload Notion
/// 400s. The guard fires inside <see cref="NotionBlockAppender.Cut"/>, so the append throws
/// <see cref="NotSupportedException"/> without a network round-trip. ns-10 confirms live whether row-batching an
/// existing table is possible; until then this pins that the guard fires first.
/// </summary>
[Trait("Category", "notion-live")]
public sealed class NotionLiveWideTableTests : NotionLiveTestBase
{
    [NotionLiveFact]
    public void TableOver100Rows_FailsLoudly_ThroughGuardBeforeNotion()
    {
        // Header + 101 data rows = 102 table_row children, past the 100-per-array cap.
        var lines = new List<string> { "| a | b |", "| --- | --- |" };
        for (var i = 0; i < 101; i++)
            lines.Add("| 1 | 2 |");
        var blocks = NotionBlockConverter.ToBlocks(string.Join("\n", lines));

        Assert.Throws<NotSupportedException>(() => NotionBlockAppender.AppendForest(Client, ChildPageId, blocks));
    }
}
