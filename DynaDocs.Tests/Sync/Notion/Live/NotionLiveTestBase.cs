namespace DynaDocs.Tests.Sync.Notion.Live;

using System.Net.Http;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// Shared setup/teardown for a live test class (ns-9 task 2): resolves the token+parent, builds a REAL
/// <see cref="NotionClient"/>, and provisions a uniquely named child page (<c>smoke-&lt;utcstamp&gt;-&lt;rand4&gt;</c>)
/// under the configured test parent that this class's test writes into. The child page is archived in
/// <see cref="Dispose"/> (best-effort — a leaked page is visible in the scratch parent, which the sprint
/// accepts). xunit constructs one instance per test method, so each live test gets its own disjoint child
/// page and cannot see another's writes.
/// <para>The constructor runs only for a NON-skipped test — a both-unset run is skipped by
/// <see cref="NotionLiveFactAttribute"/> before this is reached. So reaching here means at least one var is
/// set; <see cref="NotionLiveEnv.RequireConfig"/> then fails loudly on a partial pair, and a wrong-but-complete
/// pair fails when the page create below is rejected by Notion — both surface as a test failure, never a
/// silent pass.</para>
/// </summary>
public abstract class NotionLiveTestBase : IDisposable
{
    private readonly HttpClient _http;

    protected NotionClient Client { get; }

    /// <summary>The parent page (from <c>DYDO_NOTION_TEST_PARENT</c>) the child page is created under, and the
    /// scratch parent a spine provision/reset targets.</summary>
    protected string TestParentId { get; }

    /// <summary>This test's own scratch child page — its spine databases and body blocks land under here, and
    /// it is archived in teardown.</summary>
    protected string ChildPageId { get; }

    protected NotionLiveTestBase()
    {
        var (token, parent) = NotionLiveEnv.RequireConfig();
        TestParentId = parent;
        _http = new HttpClient();
        Client = new NotionClient(_http, token);
        ChildPageId = CreateChildPage(TestParentId, ScratchName());
    }

    /// <summary>A collision-resistant scratch name: <c>smoke-&lt;utcstamp&gt;-&lt;rand4&gt;</c> (ns-9 task 2).</summary>
    protected static string ScratchName() =>
        $"smoke-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4]}";

    /// <summary>Create a titled page nested under <paramref name="parentId"/>, returning its id.</summary>
    protected string CreateChildPage(string parentId, string title) =>
        Client.CreatePage(new NotionPageCreateRequest
        {
            Parent = NotionParent.Page(parentId),
            Properties = new() { ["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of(title) } },
        }).Id;

    /// <summary>Read a page's body back through the real spine reader: the nested block tree
    /// (<see cref="INotionClient.GetBlockChildren"/> recursively, as <see cref="NotionSyncAdapter"/> does)
    /// rendered to markdown by <see cref="NotionBlockConverter.FromBlocks"/>. This is the exact read half of
    /// the round-trip the fake cannot exercise.</summary>
    protected string ReadBodyMarkdown(string pageId) =>
        NotionBlockConverter.FromBlocks(ReadBlockTree(pageId));

    private List<NotionBlock> ReadBlockTree(string blockId)
    {
        var blocks = Client.GetBlockChildren(blockId).ToList();
        foreach (var block in blocks)
            if (block.HasChildren == true && block.Id != null && block.Type != "child_page")
                block.Children = ReadBlockTree(block.Id);
        return blocks;
    }

    public void Dispose()
    {
        try
        {
            Client.UpdatePage(ChildPageId, new NotionPageUpdateRequest { Archived = true });
        }
        catch
        {
            // Best-effort teardown (ns-9 task 2): a leaked scratch page is visible under the test parent and
            // acceptable — never fail a passing test over cleanup.
        }
        _http.Dispose();
        GC.SuppressFinalize(this);
    }
}
