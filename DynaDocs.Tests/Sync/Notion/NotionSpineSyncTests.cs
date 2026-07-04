namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

public class NotionSpineSyncTests : IDisposable
{
    private readonly string _root;
    private readonly string _dydoRoot;

    public NotionSpineSyncTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dydo-spine-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoRoot = Path.Combine(_root, "dydo");
        Seed("project/campaigns/dydo-2-0", "---\ntitle: dydo 2.0\nstatus: active\npriority: P0\n---\n\nThe pivot.");
        Seed("project/sprints/notion-sync", "---\ntitle: Notion Sync\nseq: 7\nstatus: active\ncampaign: dydo-2-0\n---\n\nSync work.");
        Seed("project/sprint-tasks/spine-task", "---\ntitle: Spine task\nstatus: in-progress\npriority: P0\nsprint: notion-sync\n---\n\nProvision the spine.");

        // Pin the classic Campaign → Sprint → SprintTask spine so these mechanics tests stay independent
        // of the evolving default model (which now also ships Release and Issue types).
        WriteModel("""
            {
              "objects": [
                { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "dydo Campaigns",
                  "properties": { "title": { "type": "title" }, "goal": { "type": "rich_text" },
                    "status": { "type": "select", "options": ["proposed", "active", "done", "abandoned"] },
                    "priority": { "type": "select", "options": ["P0", "P1", "P2", "P3"] } } },
                { "type": "Sprint", "dir": "project/sprints", "notionTitle": "dydo Sprints",
                  "properties": { "title": { "type": "title" }, "seq": { "type": "number" },
                    "status": { "type": "select", "options": ["planned", "active", "in-review", "done", "escalated"] },
                    "campaign": { "type": "relation", "to": "Campaign" } } },
                { "type": "SprintTask", "dir": "project/sprint-tasks", "notionTitle": "dydo Sprint Tasks",
                  "properties": { "title": { "type": "title" },
                    "status": { "type": "select", "options": ["backlog", "ready", "in-progress", "in-review", "blocked", "done"] },
                    "priority": { "type": "select", "options": ["P0", "P1", "P2", "P3"] },
                    "sprint": { "type": "relation", "to": "Sprint" } } }
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
        var full = Path.Combine(_dydoRoot, relPath.Replace('/', Path.DirectorySeparatorChar) + ".md");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    /// <summary>Write a project-specific sync model, overriding the auto-seeded default so a test can
    /// exercise its own object types.</summary>
    private void WriteModel(string json)
    {
        var path = Path.Combine(_dydoRoot, "_system", "sync-model.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    [Fact]
    public void Run_ProvisionsThreeDatabases_InDependencyOrder_WithRelationsInBody()
    {
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        Assert.Equal(
            ["dydo Campaigns", "dydo Sprints", "dydo Sprint Tasks"],
            client.CreatedDatabases.Select(r => NotionRichText.Flatten(r.Title)));

        // Sprint's relation references Campaign's data source (ds-1); Task's references Sprint's (ds-2).
        Assert.Equal("ds-1", client.CreatedDatabases[1].InitialDataSource.Properties["campaign"].Relation!.DataSourceId);
        Assert.Equal("ds-2", client.CreatedDatabases[2].InitialDataSource.Properties["sprint"].Relation!.DataSourceId);
    }

    [Fact]
    public void Run_CreatesRelatedPages_ResolvingLocalIdsToParentPageIds()
    {
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var campaign = Assert.Single(client.QueryDataSource("ds-1"));
        var sprint = Assert.Single(client.QueryDataSource("ds-2"));
        var task = Assert.Single(client.QueryDataSource("ds-3"));

        // Child relation VALUES point at the real parent page ids, not the local-id strings.
        Assert.Equal(campaign.Id, sprint.Properties["campaign"].Relation!.Single().Id);
        Assert.Equal(sprint.Id, task.Properties["sprint"].Relation!.Single().Id);
    }

    [Fact]
    public void Run_SecondPass_IsIdempotent_NoNewDatabasesOrPages()
    {
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        Assert.Equal(3, client.CreatedDatabases.Count);
        Assert.Single(client.QueryDataSource("ds-1"));
        Assert.Single(client.QueryDataSource("ds-2"));
        Assert.Single(client.QueryDataSource("ds-3"));
    }

    [Fact]
    public void Run_ReadDirection_MapsRelationPageIdBackToParentLocalId()
    {
        // First pass creates the spine. A Notion-side edit to the sprint's title is read back on the
        // second pass; the campaign relation must round-trip to the local id, not a raw page id, so the
        // sprint file keeps `campaign: dydo-2-0` rather than gaining a page-id reference.
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var sprintPage = client.QueryDataSource("ds-2").Single();
        sprintPage.Properties["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("Renamed In Notion") };

        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var sprintFile = File.ReadAllText(Path.Combine(_dydoRoot, "project", "sprints", "notion-sync.md"));
        Assert.Contains("campaign: dydo-2-0", sprintFile);
        Assert.Contains("Renamed In Notion", sprintFile);
    }

    [Fact]
    public void Run_UnderscorePrefixedMetaFile_NotSyncedAsRow()
    {
        // A `_`-prefixed file (e.g. _index.md) beside real docs is folder metadata, not a domain object,
        // and must never sync as a Notion row.
        Seed("project/sprint-tasks/_index", "---\ntitle: Sprint Tasks\n---\n\nFolder index, not a task.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var task = Assert.Single(client.QueryDataSource("ds-3")); // only the real spine-task, not _index
        Assert.Equal("Spine task", NotionRichText.Flatten(task.Properties["title"].Title));
    }

    [Fact]
    public void Run_ChildRelatingToTwoParentTypes_ResolvesBothOnWriteAndRead()
    {
        // A model where one type relates to TWO distinct parent types must resolve both relation values to
        // their parent page ids on write, and back to their local ids on read.
        WriteModel("""
            {
              "objects": [
                { "type": "Area",  "dir": "project/areas",  "notionTitle": "Areas",  "properties": { "title": { "type": "title" } } },
                { "type": "Owner", "dir": "project/owners", "notionTitle": "Owners", "properties": { "title": { "type": "title" } } },
                { "type": "Item",  "dir": "project/items",  "notionTitle": "Items",
                  "properties": { "title": { "type": "title" }, "area": { "type": "relation", "to": "Area" }, "owner": { "type": "relation", "to": "Owner" } } }
              ]
            }
            """);
        Seed("project/areas/design", "---\ntitle: Design\n---\n\nArea.");
        Seed("project/owners/alice", "---\ntitle: Alice\n---\n\nOwner.");
        Seed("project/items/item-1", "---\ntitle: Widget\narea: design\nowner: alice\n---\n\nAn item.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var area = Assert.Single(client.QueryDataSource("ds-1"));
        var owner = Assert.Single(client.QueryDataSource("ds-2"));
        var item = Assert.Single(client.QueryDataSource("ds-3"));

        // Write direction: both relations resolve to the real parent page ids.
        Assert.Equal(area.Id, item.Properties["area"].Relation!.Single().Id);
        Assert.Equal(owner.Id, item.Properties["owner"].Relation!.Single().Id);

        // Read direction: a second pass keeps the item file's local-id references, not raw page ids.
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        var itemFile = File.ReadAllText(Path.Combine(_dydoRoot, "project", "items", "item-1.md"));
        Assert.Contains("area: design", itemFile);
        Assert.Contains("owner: alice", itemFile);
    }

    [Fact]
    public void Run_DivergingRepoAndNotionBodyEdits_ReportConflict_RepoFileGetsMarkers()
    {
        // A colleague edits the sprint's body in Notion while the repo edits the same line differently.
        // The overlapping edit must surface as a conflict (reported + visible markers), never a silent clobber.
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var sprintPath = Path.Combine(_dydoRoot, "project", "sprints", "notion-sync.md");
        File.WriteAllText(sprintPath, File.ReadAllText(sprintPath).Replace("Sync work.", "Sync work REPO."));

        var sprintPage = client.QueryDataSource("ds-2").Single();
        client.SetBlockChildren(sprintPage.Id, NotionBlockConverter.ToBlocks("Sync work EXTERNAL."));

        var output = new StringWriter();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, output);

        Assert.Contains("conflict", output.ToString());
        var merged = File.ReadAllText(sprintPath);
        Assert.Contains("<<<<<<< repo", merged);
        Assert.Contains("Sync work REPO.", merged);
        Assert.Contains("Sync work EXTERNAL.", merged);
    }

    [Fact]
    public void LoadDocs_PoolsRecursivelyFromSubfolders_SkippingUnderscoreMeta()
    {
        // Folder placement is derived presentation (slice brief §3): a doc under resolved/ is pooled the
        // same as one at the dir root. Underscore-prefixed metadata — files and whole folders — is skipped.
        Seed("project/issues/open-bug", "---\ntitle: Open bug\nstatus: open\n---\n\nBody.");
        Seed("project/issues/resolved/done-bug", "---\ntitle: Done bug\nstatus: resolved\n---\n\nBody.");
        Seed("project/issues/_index", "---\ntitle: Issues\n---\n\nFolder index.");
        Seed("project/issues/_archive/old", "---\ntitle: Archived\nstatus: resolved\n---\n\nBody.");

        var docs = NotionSpineSync.LoadDocs(Path.Combine(_dydoRoot, "project", "issues"));

        Assert.Equal(["done-bug", "open-bug"], docs.Select(d => d.LocalId).OrderBy(x => x));
    }

    [Fact]
    public void Run_DryRun_PreviewsProvisioningAndPlan_WritesNothing()
    {
        var client = new FakeNotionClient();
        var output = new StringWriter();

        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: true, output);

        var text = output.ToString();
        Assert.Contains("--dry-run", text);
        Assert.Contains("would create database", text);
        Assert.Empty(client.CreatedDatabases);
        Assert.False(File.Exists(Path.Combine(_dydoRoot, "_system", ".local", "notion", "provision.json")));
    }
}
