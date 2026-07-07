namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Services;
using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Utils;

[Collection("ConsoleOutput")]
public class NotionSyncServiceTests : IDisposable
{
    private readonly string _dir;

    public NotionSyncServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dydo-notion-svc-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Execute_NoToken_ReportsNotConfigured_ExitsSuccess()
    {
        var err = new StringWriter();
        var code = NotionSyncService.Execute(
            token: null, new ConfigService(), _ => new FakeNotionClient(), dryRun: false, new StringWriter(), err);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("not configured", err.ToString());
    }

    [Fact]
    public void Execute_NoProject_ReportsMissingConfig_ExitsSuccess()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var bare = Path.Combine(_dir, "bare");
        Directory.CreateDirectory(bare);
        var err = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(bare);
            var code = NotionSyncService.Execute(
                "tok", new ConfigService(), _ => new FakeNotionClient(), dryRun: false, new StringWriter(), err);

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("no dydo.json", err.ToString());
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void Execute_NoParentPage_ReportsCleanly_ExitsSuccess()
    {
        // No parentPageId in config; on a clean machine no DYDO_NOTION_PARENT_PAGE either.
        if (NotionParentResolver.Resolve(null) != null)
            return; // machine has an ambient parent page set; the gate cannot be exercised here.

        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out _, parentPageId: null);
        var err = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(project);
            var code = NotionSyncService.Execute(
                "tok", new ConfigService(), _ => new FakeNotionClient(), dryRun: false, new StringWriter(), err);

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("no parent page", err.ToString());
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void Execute_InProject_DryRun_PrintsPlan_WithoutWriting()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, parentPageId: "page-root");
        var output = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(project);
            var code = NotionSyncService.Execute(
                "tok", new ConfigService(), _ => client, dryRun: true, output, new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            var text = output.ToString();
            Assert.Contains("--dry-run", text);
            Assert.Contains("Campaign", text);
            // Dry-run creates no databases.
            Assert.Empty(client.CreatedDatabases);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void Execute_Default_RunsSpineOnly_NoDocsMirror()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, parentPageId: "page-root");
        try
        {
            Directory.SetCurrentDirectory(project);
            var code = NotionSyncService.Execute(
                "tok", new ConfigService(), _ => client, dryRun: false, new StringWriter(), new StringWriter());

            Assert.Equal(ExitCodes.Success, code);
            // The docs mirror is OPT-IN: a plain sync provisions the spine but mints NO "Docs" root page.
            Assert.NotEmpty(client.CreatedDatabases);
            Assert.DoesNotContain(client.GetChildPages("page-root"), p => p.Title == "Docs");
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void Execute_Docs_RunsSpinePlusDocsMirror()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, parentPageId: "page-root");
        try
        {
            Directory.SetCurrentDirectory(project);
            var code = NotionSyncService.Execute(
                "tok", new ConfigService(), _ => client, dryRun: false, new StringWriter(), new StringWriter(),
                prune: false, parentPageOverride: null, docs: true);

            Assert.Equal(ExitCodes.Success, code);
            // --docs runs both surfaces: the spine is provisioned AND a "Docs" root page exists under the parent.
            Assert.NotEmpty(client.CreatedDatabases);
            Assert.Contains(client.GetChildPages("page-root"), p => p.Title == "Docs");
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void Execute_DocsOnlyAndSpineOnly_IsRejected()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, parentPageId: "page-root");
        var err = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(project);
            var code = NotionSyncService.Execute(
                "tok", new ConfigService(), _ => client, dryRun: false, new StringWriter(), err,
                prune: false, parentPageOverride: null, docs: false, docsOnly: true, spineOnly: true);

            Assert.Equal(ExitCodes.ValidationErrors, code);
            Assert.Contains("mutually exclusive", err.ToString());
            // Neither surface ran: no database provisioned, no "Docs" page minted.
            Assert.Empty(client.CreatedDatabases);
            Assert.Empty(client.GetChildPages("page-root"));
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void Execute_DocsAndSpineOnly_IsRejected()
    {
        // Issue 0221 finding 3: --docs (add the mirror) and --spine-only (skip it) are contradictory. Reject
        // rather than silently letting --spine-only win and dropping --docs with no word.
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, parentPageId: "page-root");
        var err = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(project);
            var code = NotionSyncService.Execute(
                "tok", new ConfigService(), _ => client, dryRun: false, new StringWriter(), err,
                prune: false, parentPageOverride: null, docs: true, docsOnly: false, spineOnly: true);

            Assert.Equal(ExitCodes.ValidationErrors, code);
            Assert.Contains("mutually exclusive", err.ToString());
            // Neither surface ran: no database provisioned, no "Docs" page minted.
            Assert.Empty(client.CreatedDatabases);
            Assert.Empty(client.GetChildPages("page-root"));
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void Execute_ParentPageOverride_TakesPrecedenceOverResolver()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, parentPageId: "page-root");
        try
        {
            Directory.SetCurrentDirectory(project);
            var code = NotionSyncService.Execute(
                "tok", new ConfigService(), _ => client, dryRun: false, new StringWriter(), new StringWriter(),
                prune: false, parentPageOverride: "scratch-page", docs: true);

            Assert.Equal(ExitCodes.Success, code);
            // An explicit --parent-page wins over the configured notion.parentPageId: the "Docs" root nests under the
            // scratch page, never the configured page — so a smoke never touches the real workspace.
            Assert.Contains(client.GetChildPages("scratch-page"), p => p.Title == "Docs");
            Assert.DoesNotContain(client.GetChildPages("page-root"), p => p.Title == "Docs");
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void Execute_DifferentParentPages_UseDifferentSnapshotFiles()
    {
        // The notion-docs snapshot is scoped by parent page (finding 4): a scratch smoke and the real workspace
        // must never share one snapshot, or the scratch run's external ids leak into the real run as stale state.
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, parentPageId: "page-root");
        try
        {
            Directory.SetCurrentDirectory(project);
            NotionSyncService.Execute("tok", new ConfigService(), _ => client, dryRun: false,
                new StringWriter(), new StringWriter(), prune: false, parentPageOverride: "scratch-a", docsOnly: true);
            NotionSyncService.Execute("tok", new ConfigService(), _ => client, dryRun: false,
                new StringWriter(), new StringWriter(), prune: false, parentPageOverride: "scratch-b", docsOnly: true);

            var dydoRoot = Path.Combine(project, "dydo");
            var pathA = BaseSnapshotStore.PathFor(dydoRoot, DocsTreeSync.SnapshotAdapterName("scratch-a"));
            var pathB = BaseSnapshotStore.PathFor(dydoRoot, DocsTreeSync.SnapshotAdapterName("scratch-b"));
            Assert.NotEqual(pathA, pathB);
            Assert.True(File.Exists(pathA));
            Assert.True(File.Exists(pathB));
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void Execute_DocsOnly_SkipsSpine_CreatesDocsPage()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, parentPageId: "page-root");
        var output = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(project);
            var code = NotionSyncService.Execute(
                "tok", new ConfigService(), _ => client, dryRun: false, output, new StringWriter(),
                prune: false, parentPageOverride: null, docsOnly: true);

            Assert.Equal(ExitCodes.Success, code);
            // The docs mirror ran: a "Docs" root page exists under the parent.
            Assert.Contains(client.GetChildPages("page-root"), p => p.Title == "Docs");
            // The spine was skipped: no databases provisioned, no spine reconcile output — a smoke never touches the board.
            Assert.Empty(client.CreatedDatabases);
            Assert.DoesNotContain("provisioning", output.ToString());
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void Execute_SpineOnly_SkipsDocsMirror()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out var client, parentPageId: "page-root");
        try
        {
            Directory.SetCurrentDirectory(project);
            var code = NotionSyncService.Execute(
                "tok", new ConfigService(), _ => client, dryRun: false, new StringWriter(), new StringWriter(),
                prune: false, parentPageOverride: null, docsOnly: false, spineOnly: true);

            Assert.Equal(ExitCodes.Success, code);
            // The spine ran: at least one database was provisioned.
            Assert.NotEmpty(client.CreatedDatabases);
            // The docs mirror was skipped: no "Docs" root page was minted.
            Assert.DoesNotContain(client.GetChildPages("page-root"), p => p.Title == "Docs");
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void Execute_NotionApiError_ReportsAndExitsToolError()
    {
        var savedCwd = Directory.GetCurrentDirectory();
        var project = SetUpProject(out _, parentPageId: "page-root");
        var throwing = new ThrowingNotionClient();
        var err = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(project);
            var code = NotionSyncService.Execute(
                "tok", new ConfigService(), _ => throwing, dryRun: false, new StringWriter(), err);

            Assert.Equal(ExitCodes.ToolError, code);
            Assert.Contains("Notion API error", err.ToString());
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    /// <summary>Build a minimal dydo project (dydo.json + a seed campaign) and a fake client. Returns the root.</summary>
    private string SetUpProject(out FakeNotionClient client, string? parentPageId)
    {
        var project = Path.Combine(_dir, "proj-" + Guid.NewGuid().ToString("N")[..6]);
        var campaigns = Path.Combine(project, "dydo", "project", "campaigns");
        Directory.CreateDirectory(campaigns);
        var notion = parentPageId == null ? "" : $",\"notion\":{{\"parentPageId\":\"{parentPageId}\"}}";
        File.WriteAllText(Path.Combine(project, "dydo.json"), $"{{\"version\":1{notion}}}");
        File.WriteAllText(Path.Combine(campaigns, "c.md"), "---\ntitle: A Campaign\nstatus: active\n---\n\nbody");
        client = new FakeNotionClient();
        return project;
    }

    /// <summary>An <see cref="INotionClient"/> whose first provisioning call throws a Notion API error.</summary>
    private sealed class ThrowingNotionClient : INotionClient
    {
        public NotionDatabase RetrieveDatabase(string databaseId) => throw new NotionApiException(500, "boom");
        public NotionDataSource RetrieveDataSource(string dataSourceId) => throw new NotionApiException(500, "boom");
        public NotionDatabase CreateDatabase(NotionDatabaseCreateRequest request) => throw new NotionApiException(429, "rate limited");
        public void UpdateDataSource(string dataSourceId, NotionDataSourceUpdateRequest request) { }
        public void CreateView(NotionViewCreateRequest request) { }
        public IReadOnlyList<string> ListViewIds(string databaseId) => [];
        public void DeleteView(string viewId) { }
        public IReadOnlyList<NotionPage> QueryDataSource(string dataSourceId) => [];
        public NotionPage CreatePage(NotionPageCreateRequest request) => new();
        public NotionPage UpdatePage(string pageId, NotionPageUpdateRequest request) => new();
        public IReadOnlyList<NotionBlock> GetBlockChildren(string blockId) => [];
        public IReadOnlyList<NotionChildPage> GetChildPages(string parentPageId) => [];
        public void AppendBlockChildren(string blockId, NotionAppendChildrenRequest request) { }
        public void DeleteBlock(string blockId) { }
        public IReadOnlyList<string> SearchDataSources() => [];
    }
}
