namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

public class DocsTreeSyncTests : IDisposable
{
    private readonly string _root;
    private readonly string _dydoRoot;

    public DocsTreeSyncTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dydo-docstree-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoRoot = Path.Combine(_root, "dydo");

        // A docs tree spanning browsable areas, a folder index body, a nested folder, the spine dir that must be
        // excluded (project/campaigns), and a framework dir that must be excluded (_system via the model write,
        // plus agents).
        Seed("understand/_index.md", "---\ntitle: Understand\n---\n\nThe understand area.");
        Seed("understand/architecture.md", "---\ntitle: Architecture\narea: understand\n---\n\n# Arch\n\nbody one.");
        Seed("understand/guard/guard-system.md", "---\ntitle: Guard System\n---\n\nHow the guard works.");
        Seed("guides/coding.md", "---\ntitle: Coding\n---\n\nStandards.");
        Seed("project/decisions/033-mirror.md", "---\ntitle: DR 033\n---\n\nThe mirror decision.");
        Seed("project/campaigns/dydo-2-0.md", "---\ntitle: dydo 2.0\nstatus: active\n---\n\nSpine row, not a doc.");
        Seed("agents/Charlie/brief.md", "---\ntitle: Brief\n---\n\nAgent workspace, excluded.");

        // Pin a model whose Campaign dir is project/campaigns, so the mirror derives that exclusion from the model.
        WriteModel("""
            {
              "objects": [
                { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
                  "properties": { "title": { "type": "title" }, "status": { "type": "select", "options": ["active"] } } }
              ]
            }
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private void Seed(string relPath, string content)
    {
        var full = Path.Combine(_dydoRoot, relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private void WriteModel(string json)
    {
        var path = Path.Combine(_dydoRoot, "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private static string Root(FakeNotionClient client) =>
        client.GetChildPages("workspace").Single(p => p.Title == "Docs").Id;

    private static string Child(FakeNotionClient client, string parentId, string title) =>
        client.GetChildPages(parentId).Single(p => p.Title == title).Id;

    private string DocPath(string rel) => Path.Combine(_dydoRoot, rel.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Run_ProvisionsExpectedHierarchy_ExcludingSpineAndFrameworkDirs()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var root = Root(client);
        var topTitles = client.GetChildPages(root).Select(p => p.Title).OrderBy(x => x, StringComparer.Ordinal).ToList();
        // understand/guides/project mirrored; campaigns (spine), _system + agents (framework) excluded.
        Assert.Equal(["Understand", "guides", "project"], topTitles);

        // Doc pages nest under their folder page.
        var understand = Child(client, root, "Understand");
        Assert.Contains(client.GetChildPages(understand), p => p.Title == "Architecture");
        // A nested subfolder resolves its parent chain.
        var guard = Child(client, understand, "guard");
        Assert.Contains(client.GetChildPages(guard), p => p.Title == "Guard System");

        // The excluded spine dir minted no page anywhere under project.
        var project = Child(client, root, "project");
        Assert.DoesNotContain(client.GetChildPages(project), p => p.Title == "Campaigns" || p.Title == "campaigns");
        Assert.Contains(client.GetChildPages(project), p => p.Title == "decisions");
    }

    [Fact]
    public void Run_IndexFile_BecomesFolderPageBody()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var understand = Child(client, Root(client), "Understand");
        Assert.Equal("The understand area.", NotionBlockConverter.FromBlocks(client.GetBlockChildren(understand)));
    }

    [Fact]
    public void Run_DocPageBody_MirrorsMarkdown()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var architecture = Child(client, Child(client, Root(client), "Understand"), "Architecture");
        // Blank lines collapse through the lossy converter; the structural content round-trips.
        Assert.Equal("# Arch\nbody one.", NotionBlockConverter.FromBlocks(client.GetBlockChildren(architecture)));
    }

    [Fact]
    public void Run_RemovedDoc_ArchivesItsPage()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var understand = Child(client, Root(client), "Understand");
        Assert.Contains(client.GetChildPages(understand), p => p.Title == "Architecture");

        // The doc is removed from the repo; the next tick archives its page.
        File.Delete(DocPath("understand/architecture.md"));
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        Assert.DoesNotContain(client.GetChildPages(understand), p => p.Title == "Architecture");
    }

    [Fact]
    public void Run_ReRun_IsIdempotent_NoDuplicatePages()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var root = Root(client);
        var understand = Child(client, root, "Understand");
        var topCountBefore = client.GetChildPages(root).Count;
        var understandCountBefore = client.GetChildPages(understand).Count;

        // A second identical tick creates nothing new and appends no body.
        client.AppendedTo.Clear();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        Assert.Single(client.GetChildPages("workspace")); // still exactly one "Docs" root
        Assert.Equal(topCountBefore, client.GetChildPages(root).Count);
        Assert.Equal(understandCountBefore, client.GetChildPages(understand).Count);
        Assert.Empty(client.AppendedTo); // no body re-pushed
    }

    [Fact]
    public void Run_NotionSideBodyEdit_OnDocPage_MergesBackToRepoFile()
    {
        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var architecture = Child(client, Child(client, Root(client), "Understand"), "Architecture");
        client.SetBlockChildren(architecture, NotionBlockConverter.ToBlocks("# Arch\n\nedited in notion."));

        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var file = File.ReadAllText(DocPath("understand/architecture.md"));
        Assert.Contains("edited in notion.", file);
        Assert.Contains("title: Architecture", file); // frontmatter preserved
    }

    [Fact]
    public void Run_DryRun_CreatesNoPages()
    {
        var client = new FakeNotionClient();
        var output = new StringWriter();

        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: true, output);

        Assert.Contains("--dry-run", output.ToString());
        Assert.Empty(client.GetChildPages("workspace")); // no "Docs" root minted
        Assert.False(File.Exists(BaseSnapshotStore.PathFor(_dydoRoot, DocsTreeSync.AdapterName)));
    }

    [Fact]
    public void Run_EmptyScaffoldingFolder_MintsNoPage()
    {
        // A folder with no docs and no index is pruned — it never mints a barren Notion page.
        Directory.CreateDirectory(Path.Combine(_dydoRoot, "empty-dir"));

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        Assert.DoesNotContain(client.GetChildPages(Root(client)), p => p.Title == "empty-dir");
    }

    [Fact]
    public void Run_NavOrderFrontmatter_OrdersFolderSiblingsAheadOfAlphabetical()
    {
        // 'zzz' sorts last alphabetically but a nav-order override lifts it ahead of 'guides'.
        Seed("zzz/_index.md", "---\ntitle: Zzz\nnav-order: 1\n---\n\nFirst by nav-order.");

        var client = new FakeNotionClient();
        DocsTreeSync.Run(client, _dydoRoot, "workspace", dryRun: false, new StringWriter());

        var topTitles = client.GetChildPages(Root(client)).Select(p => p.Title).ToList();
        Assert.True(topTitles.IndexOf("Zzz") < topTitles.IndexOf("guides"),
            "nav-order:1 folder must be created before the alphabetically-earlier 'guides'");
    }
}
