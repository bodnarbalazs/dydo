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

        // A flat forest is one level, split by the ≤100-per-request cap: 100/100/50 (issue 0299, F19 — the fake now
        // chunks like the real client).
        Assert.Equal(["page", "page", "page"], client.AppendedTo);
        Assert.Equal([100, 100, 50], client.AppendChildCounts);
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

    // ── ns-7 major 4: the appender must see a table's rows, which live in the table payload, not block children ──

    private static NotionBlock Table(int rows) => new()
    {
        Type = "table",
        Table = new NotionTable
        {
            TableWidth = 2,
            HasColumnHeader = true,
            Children = Enumerable.Range(0, rows).Select(i => new NotionBlock
            {
                Type = "table_row",
                TableRow = new NotionTableRow { Cells = [NotionRichText.Of($"a{i}"), NotionRichText.Of($"b{i}")] },
            }).ToList(),
        },
    };

    [Fact]
    public void TotalElements_CountsTableRows_NotJustTheTableBlock()
    {
        Assert.Equal(4, NotionBlockAppender.TotalElements(Table(3))); // the table + 3 rows
    }

    [Fact]
    public void IsShallow_RowCarryingTableChild_IsNonLeaf_SoParentDefers()
    {
        // A table's rows sit one level below it; inlining the table under a list item would push them to depth 3.
        // The parent must therefore be non-shallow so it defers the table to a request's top level (rows at depth 2).
        var item = Bullet("item");
        item.Children = [Table(2)];
        Assert.False(NotionBlockAppender.IsShallow(item));
    }

    [Fact]
    public void Cut_PreservesTableAndQuotePayloads_ThroughTheShallowClone()
    {
        var quote = new NotionBlock { Type = "quote", Quote = new NotionBlockBody { RichText = NotionRichText.Of("q") } };
        var (payload, _) = NotionBlockAppender.Cut([quote, Table(2)]);
        Assert.Equal("q", NotionRichText.Flatten(payload[0].Quote!.RichText));
        Assert.Equal(2, payload[1].Table!.Children!.Count); // rows survive the clone
    }

    [Fact]
    public void TableNestedInListItem_AppendsWholeTableAtTopLevel_RowsAtLegalDepth2()
    {
        var item = Bullet("item");
        item.Children = [Table(2)];
        var client = new FakeNotionClient();

        NotionBlockAppender.AppendForest(client, "page", [item]);

        Assert.True(client.MaxPayloadDepth <= 2, $"max payload depth was {client.MaxPayloadDepth}");
        var itemId = client.GetBlockChildren("page").Single().Id!;
        var table = Assert.Single(client.GetBlockChildren(itemId));
        Assert.Equal("table", table.Type);
        Assert.Equal(2, client.GetBlockChildren(table.Id!).Count); // the two rows, read back as the table's children
    }

    [Fact]
    public void WideTable_RowBatches_FirstHundredInlineRestDeferred()
    {
        // Issue 0299 (F19; live-confirmed 2026-07-22): a table wider than 100 rows is no longer a hard error — the
        // first 100 rows ride inline in the table payload and the remainder defer to the table's returned id.
        var (payload, deferrals) = NotionBlockAppender.Cut([Table(250)]);

        var table = Assert.Single(payload);
        Assert.Equal("table", table.Type);
        Assert.Equal(100, table.Table!.Children!.Count);       // first 100 rows inline
        var (index, rest) = Assert.Single(deferrals);
        Assert.Equal(0, index);                                 // deferred against the table at payload index 0
        Assert.Equal(150, rest.Count);                          // remaining 150 rows appended to the table id
        Assert.All(rest, r => Assert.Equal("table_row", r.Type));
    }

    [Fact]
    public void WideTable_AppendForest_Lands250Rows_Via100_100_50()
    {
        // End-to-end: a 250-row table appended as a body block lands all rows via the create/first-append carrying
        // 100 and the overflow appended to the table id in 100/50 chunks (F19).
        var client = new FakeNotionClient();

        NotionBlockAppender.AppendForest(client, "page", [Table(250)]);

        var table = Assert.Single(client.GetBlockChildren("page"));
        Assert.Equal(250, client.GetBlockChildren(table.Id!).Count); // every row landed
        // First append carries the table (with 100 inline rows); the deferred 150 append to the table id as 100/50.
        Assert.Equal([100, 50], client.AppendChildCounts.Skip(1).ToList());
    }

    [Fact]
    public void WithChildren_PropagatesEveryPayloadProperty_ExceptTheReadOnlyAndReplacedSet()
    {
        // Structural guard (moderate 5): the clone in Cut must CARRY every payload-bearing property, or a newly
        // added block field is silently dropped on write (the ns-7 table/quote regression). Children now live inside
        // the payload, so WithChildren clones the active body/table rather than sharing it (source-mutation safety,
        // ns-12) — the clone is therefore a distinct instance, so the guard checks each property is PRESENT (carried,
        // not dropped) rather than reference-identical. Pins the exclusion set: Id/HasChildren are read-only (never
        // written), Children is the deliberately-replaced accessor.
        var excluded = new HashSet<string> { nameof(NotionBlock.Id), nameof(NotionBlock.HasChildren), nameof(NotionBlock.Children) };
        var props = typeof(NotionBlock).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var source = new NotionBlock();
        foreach (var p in props)
            if (p.Name != nameof(NotionBlock.Children)) // leave null so the block stays shallow (no deferral)
                p.SetValue(source, SampleValue(p.PropertyType));

        var (payload, _) = NotionBlockAppender.Cut([source]);
        var clone = payload.Single();

        foreach (var p in props)
            if (!excluded.Contains(p.Name))
                Assert.True(p.GetValue(clone) != null,
                    $"WithChildren dropped NotionBlock.{p.Name} — add it to the clone or the exclusion set");
    }

    private static object SampleValue(Type type) =>
        type == typeof(string) ? "x"
        : type == typeof(bool?) ? true
        : Activator.CreateInstance(type)!;

    [Fact]
    public void Table_WrittenThenReadBackThroughFake_RoundTrips()
    {
        // Moderate 6: the fake now stores a table's rows under the table's id (childless table payload + rows via
        // GetBlockChildren), Notion's real read shape — so this exercises RenderTable's block-children fallback and
        // the Store rerouting end to end.
        const string md = "| a | b |\n| --- | --- |\n| 1 | 2 |";
        var client = new FakeNotionClient();
        NotionBlockAppender.AppendForest(client, "page", NotionBlockConverter.ToBlocks(md));

        var table = Assert.Single(client.GetBlockChildren("page"));
        Assert.Equal("table", table.Type);
        Assert.True(table.HasChildren);          // rows are reachable only via the table's id, as on real Notion
        Assert.Null(table.Table!.Children);       // the stored table payload is childless
        Assert.Equal(md, NotionBlockConverter.FromBlocks(ReadTree(client, "page")));
    }

    private static List<NotionBlock> ReadTree(FakeNotionClient client, string blockId)
    {
        var blocks = client.GetBlockChildren(blockId).ToList();
        foreach (var b in blocks)
            if (b.HasChildren == true && b.Id != null && b.Type != "child_page")
                b.Children = ReadTree(client, b.Id);
        return blocks;
    }
}
