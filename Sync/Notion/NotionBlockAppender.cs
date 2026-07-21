namespace DynaDocs.Sync.Notion;

using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// The one place that reconciles a nested block tree with Notion's write limits (ns-6, algorithm per
/// notion-oss-survey.md API-limits). Notion accepts at most two levels of children per request and 100 children
/// per array, and its append response returns only the ids of the blocks appended at the top level — never the
/// grandchildren's. So the tree is cut so every block that still owns deferred descendants is emitted at the top
/// level of some request (its id therefore comes back), its descendants deferred and appended against that
/// returned id, BFS by depth. Any block writer (create-overflow, body replace) routes deep structure through
/// here, so the depth/chunk policy lives in exactly one spot.
/// </summary>
public static class NotionBlockAppender
{
    public const int MaxChildrenPerRequest = 100;

    /// <summary>Notion caps a single request's payload at 1000 total block elements (top-level plus every nested
    /// child), independent of the 100-per-array cap. A depth-2 chunk of 100 blocks each carrying inlined children
    /// can breach it, so chunking counts total elements, not just top-level blocks.</summary>
    public const int MaxElementsPerRequest = 1000;

    /// <summary>Split a level of blocks into request payloads that each hold at most 100 top-level blocks AND at
    /// most 1000 total elements (counting inlined children). A single block never exceeds ~101 elements — the cut
    /// only inlines a leaf-child array of ≤100 — so every block fits some chunk. Order is preserved.</summary>
    public static IEnumerable<List<NotionBlock>> Chunk(IReadOnlyList<NotionBlock> blocks)
    {
        var chunk = new List<NotionBlock>();
        var elements = 0;
        foreach (var block in blocks)
        {
            var size = TotalElements(block);
            if (chunk.Count > 0 && (chunk.Count >= MaxChildrenPerRequest || elements + size > MaxElementsPerRequest))
            {
                yield return chunk;
                chunk = [];
                elements = 0;
            }
            chunk.Add(block);
            elements += size;
        }
        if (chunk.Count > 0)
            yield return chunk;
    }

    /// <summary>A block's total element count — itself plus every descendant. A table carries its rows in the
    /// <c>table</c> payload's own children (Notion's nesting), not the generic block children, so those count too;
    /// otherwise a 150-row table reads as one element and a payload silently breaches the 1000-element cap.</summary>
    public static int TotalElements(NotionBlock block) =>
        1 + (block.Children?.Sum(TotalElements) ?? 0) + (block.Table?.Children?.Sum(TotalElements) ?? 0);

    /// <summary>Append a whole nested forest as children of <paramref name="parentId"/>, resolving each deeper
    /// level against the ids the previous append returned. Order is preserved: a block's deferred children are
    /// appended as one ordered batch after it, and the client chunks each level at 100 in order.</summary>
    public static void AppendForest(INotionClient client, string parentId, IReadOnlyList<NotionBlock> forest)
    {
        if (forest.Count == 0)
            return;

        var queue = new Queue<(string ParentId, IReadOnlyList<NotionBlock> Blocks)>();
        queue.Enqueue((parentId, forest));
        while (queue.Count > 0)
        {
            var (pid, blocks) = queue.Dequeue();
            var (payload, deferrals) = Cut(blocks);
            var ids = client.AppendBlockChildren(pid, new NotionAppendChildrenRequest { Children = payload });
            foreach (var (index, children) in deferrals)
                queue.Enqueue((ids[index], children));
        }
    }

    /// <summary>Cut a level of blocks to a payload nested at most two deep, alongside the deferred continuations
    /// (a payload index → its children) to append once the level's ids are known. A block inlines its children
    /// only when they are all leaves and fit one array; otherwise the whole children list defers — deferring the
    /// list whole (never a partial prefix) keeps sibling order intact, and emitting the parent childless keeps it
    /// at the top level so the append returns its id. The payload holds shallow clones so callers never mutate the
    /// source tree.</summary>
    public static (List<NotionBlock> Payload, List<(int Index, IReadOnlyList<NotionBlock> Children)> Deferrals) Cut(
        IReadOnlyList<NotionBlock> blocks)
    {
        var payload = new List<NotionBlock>(blocks.Count);
        var deferrals = new List<(int, IReadOnlyList<NotionBlock>)>();
        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            GuardTableWidth(block);
            if (IsShallow(block))
            {
                // A table travels WHOLE: its rows must ride inline in the table payload (Notion has no way to append
                // rows to a not-yet-created table), so they are never deferred — WithChildren keeps Table.Children.
                payload.Add(WithChildren(block, block.Children));
            }
            else
            {
                payload.Add(WithChildren(block, null));
                deferrals.Add((i, block.Children!));
            }
        }
        return (payload, deferrals);
    }

    /// <summary>A table's rows ride in one <c>table.children</c> array, which Notion caps at 100 like any children
    /// array, and there is no way to append rows to an already-created table — so a table wider than the cap cannot
    /// be written this sprint. Fail loudly rather than shipping a payload Notion 400s (ns-10: confirm live whether
    /// row-batching an existing table is possible, then lift this).</summary>
    private static void GuardTableWidth(NotionBlock block)
    {
        if (block.Type == "table" && block.Table?.Children is { Count: > MaxChildrenPerRequest } rows)
            throw new NotSupportedException(
                $"table has {rows.Count} rows but Notion caps a table_row children array at {MaxChildrenPerRequest} "
                + "per request and dydo cannot yet row-batch a table across appends (ns-10 live check) — split the table.");
    }

    /// <summary>Whether a block's whole subtree fits in one depth-≤2, ≤100-child payload: no children, or children
    /// that are all leaves and fit a single array. A row-carrying TABLE child counts as non-leaf: its rows sit one
    /// level below it, so inlining it under a parent would push them to depth 3 (Notion's 2-level cap) — the parent
    /// therefore defers, landing the table at a request's top level where its rows are a legal depth 2.</summary>
    public static bool IsShallow(NotionBlock block) =>
        block.Children is not { Count: > 0 } children
        || (children.Count <= MaxChildrenPerRequest && children.All(IsLeaf));

    private static bool IsLeaf(NotionBlock block) =>
        block.Children is null or { Count: 0 } && block.Table?.Children is null or { Count: 0 };

    // A shallow clone carrying a REPLACED children list. Every payload-bearing property must be copied or the
    // clone silently drops it (the ns-7 table/quote regression) — a reflection test pins that the only properties
    // NOT propagated here are the read-only Id/HasChildren and the deliberately-replaced Children.
    private static NotionBlock WithChildren(NotionBlock block, List<NotionBlock>? children) => new()
    {
        Object = block.Object,
        Type = block.Type,
        Paragraph = block.Paragraph,
        Heading1 = block.Heading1,
        Heading2 = block.Heading2,
        Heading3 = block.Heading3,
        BulletedListItem = block.BulletedListItem,
        NumberedListItem = block.NumberedListItem,
        Code = block.Code,
        Quote = block.Quote,
        Table = block.Table,
        TableRow = block.TableRow,
        ChildPage = block.ChildPage,
        Children = children,
    };
}
