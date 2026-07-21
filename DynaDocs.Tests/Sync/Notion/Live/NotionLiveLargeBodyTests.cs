namespace DynaDocs.Tests.Sync.Notion.Live;

using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// LIVE (ns-9, issue 0291 + constraint 3): a body larger than Notion's 100-children-per-request cap is created
/// with the first 100 blocks then APPENDED the rest, without the 400 the fake can never produce (it holds any
/// list length in memory). One paragraph exceeds Notion's 2000-char per-run cap, so it also exercises
/// <see cref="NotionRichText.Of"/>'s live run-splitting (constraint 3). Proves the create-with-head-then-append-tail
/// chunking (<see cref="NotionSyncAdapter"/> / <see cref="NotionClient.AppendBlockChildren"/>) is legal against
/// real Notion.
/// </summary>
[Trait("Category", "notion-live")]
public sealed class NotionLiveLargeBodyTests : NotionLiveTestBase
{
    [NotionLiveFact]
    public void Body_Over100Blocks_CreatesThenAppends_WithoutError()
    {
        const int total = 150;
        // Paragraph 1 is a single 2500-char line (> Notion's 2000-char run cap) — still ONE block, but its
        // rich_text splits into two runs, so the create body exercises constraint 3 live. It rides in the head.
        var markdown = string.Join("\n\n", Enumerable.Range(1, total).Select(i =>
            i == 1 ? new string('x', 2500) : $"Paragraph {i}."));
        var blocks = NotionBlockConverter.ToBlocks(markdown);
        Assert.Equal(total, blocks.Count);

        // Create the page carrying the first 100 blocks (Notion's create cap), then append the overflow.
        var head = blocks.Take(NotionBlockAppender.MaxChildrenPerRequest).ToList();
        var tail = blocks.Skip(NotionBlockAppender.MaxChildrenPerRequest).ToList();
        var page = Client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(ChildPageId),
            Properties = new() { ["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of(ScratchName()) } },
            Children = head,
        });

        NotionBlockAppender.AppendForest(Client, page.Id, tail);

        Assert.Equal(total, Client.GetBlockChildren(page.Id).Count);
    }
}
