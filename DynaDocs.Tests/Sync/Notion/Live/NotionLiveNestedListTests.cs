namespace DynaDocs.Tests.Sync.Notion.Live;

using DynaDocs.Sync.Notion;

/// <summary>
/// LIVE (ns-9, ns-6): a three-deep nested list lands as a real hierarchy against Notion's two-levels-per-request
/// cap — the depth-limited append driver (<see cref="NotionBlockAppender.AppendForest"/>) deferring the third
/// level against the ids the second-level append returned. The fake accepts arbitrarily deep children in one
/// call, so only a live write exercises the depth cut. Round-tripped through the real reader, the body must come
/// back with the third-level item at its two-marker indent.
/// </summary>
[Trait("Category", "notion-live")]
public sealed class NotionLiveNestedListTests : NotionLiveTestBase
{
    [NotionLiveFact]
    public void ThreeDeepNestedList_LandsAndRoundTrips()
    {
        const string markdown = "- alpha\n  - beta\n    - gamma";
        var blocks = NotionBlockConverter.ToBlocks(markdown);

        NotionBlockAppender.AppendForest(Client, ChildPageId, blocks);

        var readBack = ReadBodyMarkdown(ChildPageId);
        Assert.Contains("- alpha", readBack);
        Assert.Contains("  - beta", readBack);
        // The third-level item present at depth 3 (two 2-wide markers = 4 spaces) proves the deferred append landed.
        Assert.Contains("    - gamma", readBack);
    }
}
