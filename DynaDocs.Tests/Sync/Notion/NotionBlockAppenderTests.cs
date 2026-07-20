namespace DynaDocs.Tests.Sync.Notion;

using System.Text;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// Drives <see cref="NotionBlockAppender"/> against <see cref="FakeNotionClient"/>: the depth cut keeps every
/// emitted payload ≤2 deep, deferred deeper levels append against the ids the previous append returned, order is
/// preserved, and wide levels compose with chunking — the ns-6 append algorithm end to end.
/// </summary>
public class NotionBlockAppenderTests
{
    private static NotionBlock Bullet(string text, params NotionBlock[] children) => new()
    {
        Type = "bulleted_list_item",
        BulletedListItem = new NotionBlockBody { RichText = NotionRichText.Of(text) },
        Children = children.Length > 0 ? children.ToList() : null,
    };

    /// <summary>Re-read a page's whole body tree the way the adapter does — recurse only into flagged blocks.</summary>
    private static string ReadBody(FakeNotionClient client, string blockId)
    {
        var sb = new StringBuilder();
        Render(client, blockId, "", sb);
        return sb.ToString().TrimEnd('\n');
    }

    private static void Render(FakeNotionClient client, string blockId, string prefix, StringBuilder sb)
    {
        foreach (var block in client.GetBlockChildren(blockId))
        {
            sb.Append(prefix).Append("- ").Append(NotionRichText.Flatten(block.BulletedListItem?.RichText)).Append('\n');
            if (block.HasChildren == true)
                Render(client, block.Id!, prefix + "  ", sb);
        }
    }

    [Fact]
    public void FourDeepList_CutsInitialPayloadToDepth2_ThenAppendsAgainstReturnedParentIds_OrderPreserved()
    {
        var client = new FakeNotionClient();
        var tree = NotionBlockConverter.ToBlocks("- A\n  - B\n    - C\n      - D");

        NotionBlockAppender.AppendForest(client, "page", tree);

        // No payload Notion was asked to persist ever exceeded two levels.
        Assert.True(client.MaxPayloadDepth <= 2, $"max payload depth was {client.MaxPayloadDepth}");
        // Each deeper level appended against the id the previous append returned: page, then A's id, then B's id.
        Assert.Equal(["page", "block-1", "block-2"], client.AppendedTo);
        // The full four-deep chain reconstructs in order.
        Assert.Equal("- A\n  - B\n    - C\n      - D", ReadBody(client, "page"));
    }

    [Fact]
    public void TwoDeepList_LandsInASingleNestedPayload()
    {
        var client = new FakeNotionClient();
        var tree = NotionBlockConverter.ToBlocks("- a\n  - b\n- c\n  - d");

        NotionBlockAppender.AppendForest(client, "page", tree);

        // One append carries the whole two-deep forest — no follow-ups needed.
        Assert.Equal(["page"], client.AppendedTo);
        Assert.Equal(2, client.MaxPayloadDepth);
        Assert.Equal("- a\n  - b\n- c\n  - d", ReadBody(client, "page"));
    }

    [Fact]
    public void FlatForest_PassesOneLevelToTheClient_NeverExceedsDepth1()
    {
        var client = new FakeNotionClient();
        var tree = NotionBlockConverter.ToBlocks(
            string.Join("\n", Enumerable.Range(0, 250).Select(i => $"- item {i}")));

        NotionBlockAppender.AppendForest(client, "page", tree);

        // A flat forest is one level: a single logical append (the real client splits it 100/100/50).
        Assert.Equal(["page"], client.AppendedTo);
        Assert.Equal(1, client.MaxPayloadDepth);
        Assert.Equal(250, client.GetBlockChildren("page").Count);
    }

    [Fact]
    public void Chunk_SplitsByBothTopLevelCountAndTotalElements()
    {
        // 100 parents × 15 children = 1600 elements: every chunk must stay within 100 top-level blocks AND 1000
        // total elements, and the split must preserve order and lose nothing.
        var blocks = Enumerable.Range(0, 100).Select(i => Bullet("p" + i, Enumerable.Range(0, 15).Select(j => Bullet("c")).ToArray())).ToList();

        var chunks = NotionBlockAppender.Chunk(blocks).ToList();

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Count <= 100));
        Assert.All(chunks, c => Assert.True(c.Sum(NotionBlockAppender.TotalElements) <= 1000));
        Assert.Equal(100, chunks.Sum(c => c.Count)); // nothing dropped
        Assert.Equal(blocks.Select(b => NotionRichText.Flatten(b.BulletedListItem!.RichText)),
            chunks.SelectMany(c => c).Select(b => NotionRichText.Flatten(b.BulletedListItem!.RichText))); // order preserved
    }

    [Fact]
    public void DeepAndWide_Composes_EveryPayloadStaysWithinDepth2()
    {
        // A root with 150 children (wide — the real client chunks 100/50) where one child itself nests a grandchild
        // (deep). The cut must keep every emitted payload ≤2 while reconstructing the whole tree in order.
        var wide = Enumerable.Range(0, 150)
            .Select(i => i == 0 ? Bullet("c0", Bullet("g0")) : Bullet($"c{i}"))
            .ToArray();
        var root = Bullet("root", wide);
        var client = new FakeNotionClient();

        NotionBlockAppender.AppendForest(client, "page", [root]);

        Assert.True(client.MaxPayloadDepth <= 2, $"max payload depth was {client.MaxPayloadDepth}");
        var rootId = client.GetBlockChildren("page").Single().Id!;
        Assert.Equal(150, client.GetBlockChildren(rootId).Count);
        var c0 = client.GetBlockChildren(rootId).First();
        Assert.True(c0.HasChildren);
        Assert.Equal("g0", NotionRichText.Flatten(client.GetBlockChildren(c0.Id!).Single().BulletedListItem!.RichText));
    }
}
