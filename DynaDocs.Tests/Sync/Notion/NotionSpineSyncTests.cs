namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Sync.Notion.Provisioning;

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
        Seed("project/slices/spine-task", "---\ntitle: Spine task\nstatus: in-progress\npriority: P0\nsprint: notion-sync\n---\n\nProvision the spine.");

        // Pin the classic Campaign → Sprint → Slice spine so these mechanics tests stay independent
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
                { "type": "Slice", "dir": "project/slices", "notionTitle": "dydo Sprint Tasks",
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
        Seed("project/slices/_index", "---\ntitle: Sprint Tasks\n---\n\nFolder index, not a task.");

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
    public void Run_SelfRelation_BlockedBy_RoundTripsLocalIdsBothDirections()
    {
        // DR 029 §5: blocked-by is a canonical self-relation on Slice, synced two-way. On write a
        // local `blocked-by: task-a` must resolve to task-a's Notion page id (never dropped); on read a
        // blocker linked on the board must render back to the LOCAL id, never a raw Notion page id — else
        // the first human link would inject a UUID into the repo source of truth.
        WriteModel("""
            {
              "objects": [
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": {
                    "title": { "type": "title" },
                    "blocked-by": { "type": "relation", "to": "Slice", "reverse": "blocks" } } }
              ]
            }
            """);
        File.Delete(Path.Combine(_dydoRoot, "project", "slices", "spine-task.md")); // ctor seed, off-model
        Seed("project/slices/task-a", "---\ntitle: Task A\n---\n\nFirst.");
        Seed("project/slices/task-b", "---\ntitle: Task B\nblocked-by: task-a\n---\n\nSecond.");

        var client = new FakeNotionClient();
        // Tick 1 creates both pages; the self-relation can't resolve yet (pages not in the base snapshot).
        // Tick 2 resolves it now that task-a's page id is recorded — proving the base-seeded self map works.
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == t);
        var pageA = PageByTitle("Task A");
        var pageB = PageByTitle("Task B");

        // Write direction: task-b.blocked-by resolved to task-a's real page id, not omitted.
        Assert.Equal(pageA.Id, pageB.Properties["blocked-by"].Relation!.Single().Id);

        // Read direction: a human links task-a as blocked by task-b on the board.
        pageA.Properties["blocked-by"] = new NotionPropertyValue { Type = "relation", Relation = [new() { Id = pageB.Id }] };
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var fileA = File.ReadAllText(Path.Combine(_dydoRoot, "project", "slices", "task-a.md"));
        Assert.Contains("blocked-by: task-b", fileA);   // rendered to the local id
        Assert.DoesNotContain(pageB.Id, fileA);          // never the raw Notion page id
    }

    /// <summary>A Slice self-relation model (blocked-by → Slice) with three bare blockers and one
    /// task that references two of them, used by the multi-target relation change-detection tests below.</summary>
    private void SeedMultiBlockerSpine()
    {
        WriteModel("""
            {
              "objects": [
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": {
                    "title": { "type": "title" },
                    "blocked-by": { "type": "relation", "to": "Slice", "reverse": "blocks" } } }
              ]
            }
            """);
        File.Delete(Path.Combine(_dydoRoot, "project", "slices", "spine-task.md")); // ctor seed, off-model
        Seed("project/slices/task-a", "---\ntitle: A\n---\n\nA.");
        Seed("project/slices/task-b", "---\ntitle: B\n---\n\nB.");
        Seed("project/slices/task-c", "---\ntitle: C\n---\n\nC.");
        Seed("project/slices/task-x", "---\ntitle: X\nblocked-by: task-a, task-b\n---\n\nX.");
    }

    [Fact]
    public void Run_ClearAllBlockers_RepoSide_EmptiesMultiRelationInNotion()
    {
        // Regression (review R2-1): before the per-entry fix, a multi-target relation ("task-a, task-b") was
        // classified adapter-invisible in whole, so clearing every blocker in the repo dropped out of the
        // normalized fields on BOTH sides — base == repo == external — and the emptying never propagated.
        SeedMultiBlockerSpine();

        var client = new FakeNotionClient();
        // Two ticks: tick 1 creates every page, tick 2 resolves the self-relation to both blockers' page ids.
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == t);
        Assert.Equal(2, PageByTitle("X").Properties["blocked-by"].Relation!.Count); // baseline: both blockers linked

        var xPath = Path.Combine(_dydoRoot, "project", "slices", "task-x.md");
        File.WriteAllText(xPath, "---\ntitle: X\nblocked-by:\n---\n\nX.");
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        Assert.Empty(PageByTitle("X").Properties["blocked-by"].Relation!);
    }

    [Fact]
    public void Run_MultiToMultiEdit_RepoSide_PropagatesSwappedTargetToNotion()
    {
        // Regression (review R2-1): a repo edit that swaps one target of a two-target relation
        // ("task-a, task-b" -> "task-a, task-c") must be detected and pushed; the old whole-string
        // ContainsKey check saw neither value as resolvable, so the change was never propagated.
        SeedMultiBlockerSpine();

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == t);
        var pageA = PageByTitle("A");
        var pageC = PageByTitle("C");
        Assert.Equal([pageA.Id, PageByTitle("B").Id], PageByTitle("X").Properties["blocked-by"].Relation!.Select(r => r.Id));

        var xPath = Path.Combine(_dydoRoot, "project", "slices", "task-x.md");
        File.WriteAllText(xPath, "---\ntitle: X\nblocked-by: task-a, task-c\n---\n\nX.");
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        // The swap propagated: task-b's page id is gone, task-c's is in, order preserved.
        Assert.Equal([pageA.Id, pageC.Id], PageByTitle("X").Properties["blocked-by"].Relation!.Select(r => r.Id));
    }

    [Fact]
    public void Run_MultiToMultiEdit_NotionSide_LandsAsLocalIds_NotRevertedByOverlay()
    {
        // Regression (review R2-1): a Notion-side swap of one target of a two-target relation, in the SAME
        // tick another field changed, must land in frontmatter as LOCAL ids and must NOT be reverted by
        // OverlayAdapterInvisibleFields. The old code saw the fully-resolvable multi-relation as invisible,
        // so the overlay restored the stale repo value and advanced the base — a silent permanent divergence.
        SeedMultiBlockerSpine();

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == t);
        var pageX = PageByTitle("X");
        var pageC = PageByTitle("C");

        // A colleague swaps task-b for task-c on the board AND renames the task, in the same tick.
        pageX.Properties["blocked-by"] = new NotionPropertyValue { Type = "relation", Relation = [new() { Id = PageByTitle("A").Id }, new() { Id = pageC.Id }] };
        pageX.Properties["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("X Renamed") };
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var fileX = File.ReadAllText(Path.Combine(_dydoRoot, "project", "slices", "task-x.md"));
        Assert.Contains("blocked-by: task-a, task-c", fileX); // the Notion edit landed, as local ids
        Assert.DoesNotContain(pageC.Id, fileX);               // never a raw Notion page id
        Assert.Contains("X Renamed", fileX);                  // the co-occurring field edit also landed
    }

    [Fact]
    public void Run_ProvisionsDualRelationColorsAndRollupPostPass()
    {
        // A DR 029-shaped model: Campaign rolls up progress over its Sprints via the reverse "sprints"
        // column that Sprint's dual-property relation creates; priority carries palette colors.
        WriteModel("""
            {
              "objects": [
                { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
                  "properties": {
                    "title": { "type": "title" },
                    "priority": { "type": "select", "options": ["Urgent", "Low"], "colors": { "Urgent": "red", "Low": "gray" } },
                    "progress": { "type": "rollup", "rollupRelation": "sprints", "rollupProperty": "done", "rollupFunction": "percent_checked" } } },
                { "type": "Sprint", "dir": "project/sprints", "notionTitle": "Sprints",
                  "properties": {
                    "title": { "type": "title" },
                    "campaign": { "type": "relation", "to": "Campaign", "reverse": "sprints" } } }
              ]
            }
            """);
        Seed("project/campaigns/c1", "---\ntitle: C1\npriority: Urgent\n---\n\nBody.");
        Seed("project/sprints/s1", "---\ntitle: S1\ncampaign: c1\n---\n\nBody.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        // Campaign (ds-1) created first; its priority select carries colors.
        var campaignDb = client.CreatedDatabases[0];
        Assert.Equal("red", campaignDb.InitialDataSource.Properties["priority"].Select!.Options.Single(o => o.Name == "Urgent").Color);
        Assert.False(campaignDb.InitialDataSource.Properties.ContainsKey("progress")); // rollup deferred

        // Sprint (ds-2) relates to Campaign dual-property, naming the "sprints" reverse.
        var sprintRelation = client.CreatedDatabases[1].InitialDataSource.Properties["campaign"].Relation!;
        Assert.Equal("dual_property", sprintRelation.Type);
        Assert.Equal("sprints", sprintRelation.DualProperty!.SyncedPropertyName);

        // Rollup post-pass: after both databases exist, Campaign's data source is PATCHed with progress.
        var rollupPatch = Assert.Single(client.DataSourceUpdates, u => u.Request.Properties.ContainsKey("progress"));
        Assert.Equal("ds-1", rollupPatch.DataSourceId);
        Assert.Equal("sprints", rollupPatch.Request.Properties["progress"].Rollup!.RelationPropertyName);
    }

    [Fact]
    public void Run_Prune_DeletesRogueNotionProperty()
    {
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        // A colleague adds a column in Notion that the model does not define.
        client.DataSourceSchema("ds-1").Properties["Rogue"] = new NotionPropertySchema();

        var output = new StringWriter();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, output, prune: true);

        Assert.Contains("PRUNE rogue property \"Rogue\"", output.ToString());
        Assert.False(client.DataSourceSchema("ds-1").Properties.ContainsKey("Rogue"));
    }

    [Fact]
    public void Run_WarnsRogueProperty_ButLeavesIt_WithoutPrune()
    {
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        client.DataSourceSchema("ds-1").Properties["Rogue"] = new NotionPropertySchema();

        var output = new StringWriter();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, output);

        Assert.Contains("WARN rogue property \"Rogue\"", output.ToString());
        Assert.True(client.DataSourceSchema("ds-1").Properties.ContainsKey("Rogue")); // untouched
    }

    /// <summary>A single-type model whose Slice declares the DR 030 attention layer: an engine-computed
    /// last-activity date plus the stale/attention formulas that read it.</summary>
    private void SeedLastActivitySpine()
    {
        WriteModel("""
            {
              "objects": [
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": {
                    "title": { "type": "title" },
                    "status": { "type": "select", "options": ["in-progress", "in-review", "done"] },
                    "needs-human": { "type": "checkbox" },
                    "last-activity": { "type": "date", "engineComputed": true },
                    "stale": { "type": "formula", "expression": "and(prop(\"status\") == \"in-progress\", and(not empty(prop(\"last-activity\")), dateBetween(now(), prop(\"last-activity\"), \"days\") > 3))" },
                    "attention": { "type": "formula", "expression": "or(prop(\"needs-human\"), prop(\"stale\"))" } } }
              ]
            }
            """);
        File.Delete(Path.Combine(_dydoRoot, "project", "slices", "spine-task.md")); // ctor seed, off-model
    }

    private string TaskPath(string localId) =>
        Path.Combine(_dydoRoot, "project", "slices", localId + ".md");

    private string? LastActivityInBase(string localId) =>
        new BaseSnapshotStore(BaseSnapshotStore.PathFor(_dydoRoot, "notion-slice")).GetLastActivity(localId);

    [Fact]
    public void Run_LastActivity_BumpsOnGenuineRepoChange_NotOnExternalWriteOrNoOp_NeverInFrontmatter()
    {
        SeedLastActivitySpine();
        Seed("project/slices/task-1", "---\ntitle: Task 1\nstatus: in-progress\n---\n\nBody one.");
        File.SetLastWriteTimeUtc(TaskPath("task-1"), new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc));

        var client = new FakeNotionClient();
        // Tick 1: create. last-activity initialises from the file's mtime and is written one-way to Notion.
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var page = client.QueryDataSource("ds-1").Single();
        Assert.Equal("2026-06-20", page.Properties["last-activity"].Date!.Start);
        Assert.Equal("2026-06-20", LastActivityInBase("task-1"));
        // Engine-owned: it must never round-trip into the repo frontmatter (that would loop every sync).
        Assert.DoesNotContain("last-activity", File.ReadAllText(TaskPath("task-1")));

        // Tick 2: nothing changed — no bump, Notion value untouched.
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        Assert.Equal("2026-06-20", client.QueryDataSource("ds-1").Single().Properties["last-activity"].Date!.Start);
        Assert.Equal("2026-06-20", LastActivityInBase("task-1"));

        // Tick 3: a genuine repo-side edit bumps last-activity to the file's new mtime.
        File.WriteAllText(TaskPath("task-1"), "---\ntitle: Task 1\nstatus: in-progress\n---\n\nBody one EDITED.");
        File.SetLastWriteTimeUtc(TaskPath("task-1"), new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        Assert.Equal("2026-07-01", client.QueryDataSource("ds-1").Single().Properties["last-activity"].Date!.Start);
        Assert.Equal("2026-07-01", LastActivityInBase("task-1"));

        // Tick 4: an external-to-repo write (Notion edit flowing back to the repo) is NOT repo activity —
        // last-activity must hold, even though the engine rewrites the repo file (bumping its mtime).
        var external = client.QueryDataSource("ds-1").Single();
        external.Properties["status"] = new NotionPropertyValue { Type = "select", Select = new NotionSelectOption { Name = "in-review" } };
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        Assert.Equal("2026-07-01", LastActivityInBase("task-1"));
        Assert.Equal("2026-07-01", client.QueryDataSource("ds-1").Single().Properties["last-activity"].Date!.Start);
        Assert.DoesNotContain("last-activity", File.ReadAllText(TaskPath("task-1"))); // still absent from frontmatter
    }

    [Fact]
    public void Run_ProvisionsEngineDate_AndDeferredFormulas_ForAttentionLayer()
    {
        SeedLastActivitySpine();
        Seed("project/slices/task-1", "---\ntitle: Task 1\nstatus: in-progress\n---\n\nBody.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        // The engine date and the leaf stale formula are provisioned in the create; attention (which reads
        // stale) is PATCHed on afterwards.
        var createSchema = client.CreatedDatabases.Single().InitialDataSource.Properties;
        Assert.NotNull(createSchema["last-activity"].Date);
        Assert.NotNull(createSchema["stale"].Formula);
        Assert.False(createSchema.ContainsKey("attention"));

        var attentionPatch = Assert.Single(client.DataSourceUpdates, u => u.Request.Properties.ContainsKey("attention"));
        Assert.Contains("stale", attentionPatch.Request.Properties["attention"].Formula!.Expression);
    }

    [Fact]
    public void Run_PostPass_EmitsChildFormulasAndRollups_BeforeParentReferents()
    {
        // Finding R2-1/R2-2: a parent's rollup targets a CHILD column that may itself be a deferred formula
        // (Sprint's attention-count → tasks/"attention") or a sibling formula projection (Campaign's
        // needs-human → sprints/"needs-human-count"). Real Notion validates a rollup's target property at
        // creation, so the post-pass must run CHILD-FIRST — AddRollups(type) then AddFormulas(type) per type,
        // children before parents — so every referent exists before the parent that reads it is emitted.
        WriteModel("""
            {
              "objects": [
                { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
                  "properties": {
                    "title": { "type": "title" },
                    "needs-human": { "type": "rollup", "rollupRelation": "sprints", "rollupProperty": "needs-human-count", "rollupFunction": "sum" },
                    "attention-count": { "type": "rollup", "rollupRelation": "sprints", "rollupProperty": "attention", "rollupFunction": "checked" },
                    "attention": { "type": "formula", "expression": "or(prop(\"needs-human\") > 0, prop(\"attention-count\") > 0)" } } },
                { "type": "Sprint", "dir": "project/sprints", "notionTitle": "Sprints",
                  "properties": {
                    "title": { "type": "title" },
                    "campaign": { "type": "relation", "to": "Campaign", "reverse": "sprints" },
                    "needs-human": { "type": "rollup", "rollupRelation": "tasks", "rollupProperty": "needs-human", "rollupFunction": "checked" },
                    "needs-human-count": { "type": "formula", "expression": "prop(\"needs-human\")" },
                    "attention-count": { "type": "rollup", "rollupRelation": "tasks", "rollupProperty": "attention", "rollupFunction": "checked" },
                    "attention": { "type": "formula", "expression": "or(prop(\"needs-human\") > 0, prop(\"attention-count\") > 0)" } } },
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": {
                    "title": { "type": "title" },
                    "status": { "type": "select", "options": ["in-progress", "done"] },
                    "sprint": { "type": "relation", "to": "Sprint", "reverse": "tasks" },
                    "needs-human": { "type": "checkbox" },
                    "stale": { "type": "formula", "expression": "prop(\"status\") == \"in-progress\"" },
                    "attention": { "type": "formula", "expression": "or(prop(\"needs-human\"), prop(\"stale\"))" } } }
              ]
            }
            """);

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var updates = client.DataSourceUpdates;
        int IndexOf(string ds, string key) =>
            updates.FindIndex(u => u.DataSourceId == ds && u.Request.Properties.ContainsKey(key));

        // Data sources are assigned parent-first at create time: Campaign=ds-1, Sprint=ds-2, Slice=ds-3.
        var taskAttention = IndexOf("ds-3", "attention");            // Slice's deferred attention formula
        var sprintAttentionCount = IndexOf("ds-2", "attention-count"); // Sprint rollup that TARGETS it
        var sprintNeedsHumanCount = IndexOf("ds-2", "needs-human-count"); // Sprint formula projection
        var campaignNeedsHuman = IndexOf("ds-1", "needs-human");        // Campaign rollup that SUMS it

        Assert.True(taskAttention >= 0 && sprintAttentionCount >= 0 && sprintNeedsHumanCount >= 0 && campaignNeedsHuman >= 0);
        Assert.True(taskAttention < sprintAttentionCount,
            "Slice.attention must be patched before Sprint's attention-count rollup references it");
        Assert.True(sprintNeedsHumanCount < campaignNeedsHuman,
            "Sprint.needs-human-count must be patched before Campaign's needs-human rollup sums it");

        // Finding R2-2: Campaign's needs-human is a supported rollup over the Sprint FORMULA projection —
        // never a rollup-of-rollup, which Notion rejects.
        var campaignRollup = updates.First(u => u.DataSourceId == "ds-1" && u.Request.Properties.ContainsKey("needs-human"))
            .Request.Properties["needs-human"].Rollup;
        Assert.Equal("needs-human-count", campaignRollup!.RollupPropertyName);
    }

    [Fact]
    public void Run_LastActivity_SeedsPreExistingBaseObject_OnNoOpTick_AndLandsOnNotionPage()
    {
        // Finding R2-3 / finding 1: an object already in the base snapshot from a pre-slice sync carries no
        // last-activity. On a no-op tick the engine must SEED it from the file mtime AND push that seeded
        // value ONTO the Notion page — no reconcile path carries an upsert for a stalled item, so without an
        // engine-computed refresh the page would keep an empty last-activity forever and stale could never
        // fire. Seeding writes only engine-internal store state / the one-way engine column, never frontmatter,
        // and a subsequent no-op tick must issue no repeated write.
        SeedLastActivitySpine();
        Seed("project/slices/task-1", "---\ntitle: Task 1\nstatus: in-progress\n---\n\nBody.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        // Simulate a base written by the pre-slice engine: the object is present but the last-activity map is
        // empty (the old file format had no such map), and the page carries no last-activity either.
        var snapPath = BaseSnapshotStore.PathFor(_dydoRoot, "notion-slice");
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(snapPath))!;
        node["lastActivity"] = new System.Text.Json.Nodes.JsonObject();
        File.WriteAllText(snapPath, node.ToJsonString());
        Assert.Null(LastActivityInBase("task-1"));
        var page = client.QueryDataSource("ds-1").Single();
        page.Properties.Remove("last-activity");
        client.LastActivityWrites.Clear();

        // The stalled task's file is untouched for days; a no-op tick seeds last-activity from its mtime.
        File.SetLastWriteTimeUtc(TaskPath("task-1"), new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc));
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        Assert.Equal("2026-06-25", LastActivityInBase("task-1"));
        Assert.DoesNotContain("last-activity", File.ReadAllText(TaskPath("task-1")));
        // The seeded value lands on the Notion PAGE, not just the internal store.
        Assert.Equal("2026-06-25", client.QueryDataSource("ds-1").Single().Properties["last-activity"].Date!.Start);
        Assert.Single(client.LastActivityWrites);

        // Idempotent: a further no-op tick sees the page already in sync and issues no repeated write.
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        Assert.Single(client.LastActivityWrites);
        Assert.Equal("2026-06-25", client.QueryDataSource("ds-1").Single().Properties["last-activity"].Date!.Start);
    }

    [Fact]
    public void Run_LastActivity_StampsAndPushes_NotionCreatedObject()
    {
        // Finding 1: an object created on the Notion side (CreateToRepo) never reaches RecordActivity's repo
        // arm — its repo file does not exist yet — so it kept an EMPTY last-activity forever and could never
        // go stale. The engine must stamp it (from now, since there is no file mtime) AND push that value onto
        // the existing page, idempotently.
        SeedLastActivitySpine();

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // provision ds-1, no docs

        // A human creates a task directly on the board; it maps to a repo file on the next tick.
        client.SeedPage("ext-task", new Dictionary<string, DynaDocs.Sync.Notion.Dtos.NotionPropertyValue>
        {
            ["title"] = new() { Type = "title", Title = NotionRichText.Of("Board-made task") },
            ["status"] = new() { Type = "select", Select = new NotionSelectOption { Name = "in-progress" } },
        }, dataSourceId: "ds-1");
        client.LastActivityWrites.Clear();

        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        // The Notion-created object was filed to the repo AND stamped both in the store and on the page.
        Assert.True(File.Exists(TaskPath("ext-task")));
        var seeded = LastActivityInBase("ext-task");
        Assert.NotNull(seeded);
        Assert.Equal(seeded, client.QueryDataSource("ds-1").Single(p => p.Id == "ext-task").Properties["last-activity"].Date!.Start);
        Assert.DoesNotContain("last-activity", File.ReadAllText(TaskPath("ext-task")));
        Assert.Equal(["ext-task"], client.LastActivityWrites);

        // Idempotent: the now-repo-backed, unchanged object issues no further engine-computed write.
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        Assert.Equal(["ext-task"], client.LastActivityWrites);
    }

    [Fact]
    public void Run_PageArchivedAndRepoFileDeletedBetweenTicks_DoesNotWedgeSync_SiblingKeepsSyncing()
    {
        // Finding F1: a page archived/trashed in Notion AND its repo file deleted in the same inter-tick
        // window leaves base-present / repo-null / external-null — ReconcileEngine returns None, but the
        // seeded last-activity survives. The engine-computed refresh must NOT fall back to the base id and
        // enqueue a property write against the (now archived) page: real Notion 400s that write, throwing
        // mid-Apply before the base advances, permanently wedging the type's sync with no self-heal.
        SeedLastActivitySpine();
        Seed("project/slices/task-1", "---\ntitle: Task 1\nstatus: in-progress\n---\n\nBody one.");
        Seed("project/slices/task-2", "---\ntitle: Task 2\nstatus: in-progress\n---\n\nBody two.");

        var client = new FakeNotionClient();
        // Tick 1: both pages created, each with a seeded last-activity in the base snapshot.
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == t);
        var archivedPageId = PageByTitle("Task 1").Id;

        // Inter-tick window: Task 1's page is archived in Notion AND its repo file is deleted; Task 2 gets a
        // genuine repo edit, proving the batch still applies end-to-end for a sibling of the same type.
        PageByTitle("Task 1").Archived = true;
        File.Delete(TaskPath("task-1"));
        File.WriteAllText(TaskPath("task-2"), "---\ntitle: Task 2\nstatus: done\n---\n\nBody two EDITED.");
        client.LastActivityWrites.Clear();

        // Two further ticks must not throw (the hardened fake 400s any property write on an archived page).
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        // No property write was ever attempted against the archived page — its state is untouched.
        Assert.DoesNotContain(archivedPageId, client.LastActivityWrites);
        Assert.Equal("in-progress", PageByTitle("Task 1").Properties["status"].Select!.Name);
        Assert.True(PageByTitle("Task 1").Archived);

        // The engine keeps functioning for the type: the sibling's edit propagated to Notion.
        Assert.Equal("done", PageByTitle("Task 2").Properties["status"].Select!.Name);
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

    /// <summary>A single-type Slice model whose only relation is the blocked-by self-relation.</summary>
    private void SeedSelfRelationSpine()
    {
        WriteModel("""
            {
              "objects": [
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": {
                    "title": { "type": "title" },
                    "blocked-by": { "type": "relation", "to": "Slice", "reverse": "blocks" } } }
              ]
            }
            """);
        File.Delete(Path.Combine(_dydoRoot, "project", "slices", "spine-task.md")); // ctor seed, off-model
    }

    [Fact]
    public void Run_PushThenTargetBecomesResolvable_DetectsRepoAddition_PushesNeverBlanks()
    {
        // Finding 1 (PushToExternal path). task-a is ALREADY in the base (synced alone on tick 1). Then in one
        // edit its body changes AND it gains `blocked-by: task-b`, where task-b is created the SAME tick — so
        // the relation is unresolvable and the adapter omits it, while the body edit still drives a
        // PushToExternal. The base must record only what was externalized (the body, NOT the dropped relation).
        // An un-normalized base would keep the raw `blocked-by`; once task-b resolves the next tick, the engine
        // would compare the now-resolvable base entry against the external's empty relation, misread it as an
        // external deletion, and WriteToRepo would blank the repo value (the overlay cannot protect a field
        // that is now resolvable). Normalized, it is instead detected as a repo-side addition and pushed.
        // The body edit is essential: adding only the unresolvable relation is masked by the field normalizer
        // (it reads as no change), so no push would fire on tick 2 and the raw entry never enters the base.
        SeedSelfRelationSpine();
        Seed("project/slices/task-a", "---\ntitle: A\n---\n\nA.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1: task-a alone

        // task-b appears AND task-a is edited (body) and gains the blocker, in the same inter-tick window.
        Seed("project/slices/task-b", "---\ntitle: B\n---\n\nB.");
        Seed("project/slices/task-a", "---\ntitle: A\nblocked-by: task-b\n---\n\nA EDITED.");
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 2: push body; relation unresolvable
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 3: task-b resolves

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == t);

        // The addition was pushed on the resolving tick: task-a's page points at task-b's page.
        Assert.Equal(PageByTitle("B").Id, PageByTitle("A").Properties["blocked-by"].Relation!.Single().Id);
        // The repo edit was never blanked.
        Assert.Contains("blocked-by: task-b", File.ReadAllText(TaskPath("task-a")));
    }

    [Fact]
    public void Run_PartialMultiRelation_NewTargetResolvesLater_PushedNotBlanked()
    {
        // Finding 1 (partial multi-value, PushToExternal path). task-x is established in the base with a single
        // resolvable blocker (task-a). Then in one edit its body changes AND it gains a second blocker task-b
        // created the SAME tick — so only task-a is resolvable and the adapter pushes just that subset, while
        // the body edit drives the PushToExternal. The base must record only the resolvable subset (task-a);
        // an un-normalized base keeping the raw pair would, once task-b resolves next tick, read the external's
        // single-value relation as a deletion and WriteToRepo would blank task-b out of the pair. Normalized,
        // the second target is detected as a repo-side addition and pushed.
        SeedSelfRelationSpine();
        Seed("project/slices/task-a", "---\ntitle: A\n---\n\nA.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1: task-a alone

        Seed("project/slices/task-x", "---\ntitle: X\nblocked-by: task-a\n---\n\nX.");
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 2: create task-x, task-a resolves

        // task-b appears AND task-x is edited (body) and gains the second blocker, in the same window.
        Seed("project/slices/task-b", "---\ntitle: B\n---\n\nB.");
        Seed("project/slices/task-x", "---\ntitle: X\nblocked-by: task-a, task-b\n---\n\nX EDITED.");
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 3: push body; task-b unresolvable
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 4: task-b resolves

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == t);

        Assert.Equal(
            [PageByTitle("A").Id, PageByTitle("B").Id],
            PageByTitle("X").Properties["blocked-by"].Relation!.Select(r => r.Id));
        Assert.Contains("blocked-by: task-a, task-b", File.ReadAllText(TaskPath("task-x")));
    }

    [Fact]
    public void Run_SameTickExternalEdit_PendingRelationEntry_SurvivesRewrite_PushesOnResolvingTick()
    {
        // Finding 1a (WriteToRepo path, spine-level). task-x is established in the base blocked by the single
        // resolvable task-a. Then, in one inter-tick window: a new blocker task-b appears (unresolvable this tick)
        // AND a colleague renames task-x on the board. The board rename drives WriteToRepo; the per-entry overlay
        // must keep the pending task-b on the repo file (the whole-field overlay could not, since blocked-by keeps
        // a non-empty resolvable subset), so it survives and pushes once task-b resolves the next tick.
        SeedSelfRelationSpine();
        Seed("project/slices/task-a", "---\ntitle: A\n---\n\nA.");
        Seed("project/slices/task-x", "---\ntitle: X\nblocked-by: task-a\n---\n\nX.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1: create both
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 2: task-a resolves, pushed

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == t);
        Assert.Equal(PageByTitle("A").Id, PageByTitle("X").Properties["blocked-by"].Relation!.Single().Id);

        // Same inter-tick window: task-b appears (unresolvable this tick), task-x gains it, and the board renames task-x.
        Seed("project/slices/task-b", "---\ntitle: B\n---\n\nB.");
        var xPath = Path.Combine(_dydoRoot, "project", "slices", "task-x.md");
        File.WriteAllText(xPath, "---\ntitle: X\nblocked-by: task-a, task-b\n---\n\nX.");
        PageByTitle("X").Properties["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("X Renamed") };

        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 3: WriteToRepo (rename), task-b pending

        var fileAfterWrite = File.ReadAllText(xPath);
        Assert.Contains("X Renamed", fileAfterWrite);                    // the board rename landed
        Assert.Contains("blocked-by: task-a, task-b", fileAfterWrite);   // the pending entry survived the rewrite

        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 4: task-b resolves -> push

        Assert.Equal(
            [PageByTitle("A").Id, PageByTitle("B").Id],
            PageByTitle("X Renamed").Properties["blocked-by"].Relation!.Select(r => r.Id));
    }

    [Fact]
    public void Run_ExternalPageDeleted_WhilePendingRelationEntryExists_NoSilentFileDeletion()
    {
        // Finding 1b (DeleteOne path, spine-level). task-x carries a resolvable blocker (task-a) plus a pending,
        // un-pushed entry (task-b, created the same tick). Its Notion page is then archived. The delete-unchanged
        // branch guards with HasUnpushedRelation — the repo's raw entries are compared against the base's recorded
        // ones, so the pending entry counts as un-pushed work: the file must NOT be silently deleted — instead the
        // page is resurrected and the pending entry preserved.
        SeedSelfRelationSpine();
        Seed("project/slices/task-a", "---\ntitle: A\n---\n\nA.");
        Seed("project/slices/task-x", "---\ntitle: X\nblocked-by: task-a\n---\n\nX.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 2: task-a resolves, pushed

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == t);
        var oldXId = PageByTitle("X").Id;

        // Same window: a pending blocker task-b appears, task-x gains it, and task-x's page is archived in Notion.
        Seed("project/slices/task-b", "---\ntitle: B\n---\n\nB.");
        var xPath = Path.Combine(_dydoRoot, "project", "slices", "task-x.md");
        File.WriteAllText(xPath, "---\ntitle: X\nblocked-by: task-a, task-b\n---\n\nX.");
        PageByTitle("X").Archived = true;

        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 3: page gone, pending entry present

        // The file was NOT silently deleted, and the pending entry is intact.
        Assert.True(File.Exists(xPath));
        Assert.Contains("blocked-by: task-a, task-b", File.ReadAllText(xPath));
        // It was resurrected as a fresh, non-archived page rather than the deletion propagating.
        var live = client.QueryDataSource("ds-1").Where(p => !p.Archived && NotionRichText.Flatten(p.Properties["title"].Title) == "X").ToList();
        var resurrected = Assert.Single(live);
        Assert.NotEqual(oldXId, resurrected.Id);
    }

    [Fact]
    public void Run_ExternalPageArchived_RepoDocCarriesLocalOnlyKeys_FileDeleted()
    {
        // The wave-5 1b regression, spine-level: a real dydo doc carries permanently-local, out-of-schema
        // frontmatter keys ("area", "type") the Notion schema has no column for, so the adapter never persists
        // them and reads them back absent — as EVERY real sprint-task/issue does. Archiving the page on the board
        // must DELETE the repo file: the local-only keys are not un-pushed work, so they must not force the
        // archive to be misread as an edit and silently re-create the page on every tick.
        WriteModel("""
            {
              "objects": [
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": { "title": { "type": "title" },
                    "status": { "type": "select", "options": ["in-progress", "done"] } } }
              ]
            }
            """);
        File.Delete(Path.Combine(_dydoRoot, "project", "slices", "spine-task.md")); // ctor seed, off-model
        Seed("project/slices/task-1", "---\ntitle: T1\nstatus: in-progress\narea: project\ntype: context\n---\n\nBody.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // create the page

        // The board archives the page; the repo file is untouched since push — only its local-only keys differ
        // from the base, which is recorded normalized and so never held them.
        client.QueryDataSource("ds-1").Single().Archived = true;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        // The archive propagated: the file is gone, no page was resurrected, and the base entry is retired.
        Assert.False(File.Exists(TaskPath("task-1")));
        Assert.DoesNotContain(client.QueryDataSource("ds-1"), p => !p.Archived);
        Assert.Null(new BaseSnapshotStore(BaseSnapshotStore.PathFor(_dydoRoot, "notion-slice")).Get("task-1"));
    }

    [Fact]
    public void Run_BothDeletedThenGitRestoreIdentical_RetiresBase_RestoredFileRoundTripsAsNewCreate()
    {
        // Finding 2: when a task is deleted in the repo AND its page archived in Notion, the stale base entry
        // must be retired. Otherwise a later git-restore of a file equal to the old base hits DeleteOne's
        // unchanged branch and is silently deleted; the retired id must also vanish from the persisted snapshot.
        WriteModel("""
            {
              "objects": [
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": { "title": { "type": "title" },
                    "status": { "type": "select", "options": ["open", "done"] } } }
              ]
            }
            """);
        File.Delete(Path.Combine(_dydoRoot, "project", "slices", "spine-task.md"));
        var content = "---\ntitle: T1\nstatus: open\n---\n\nBody.";
        Seed("project/slices/task-1", content);

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        var archivedId = client.QueryDataSource("ds-1").Single().Id;

        // Both sides vanish in the same window: repo file deleted, Notion page archived.
        File.Delete(TaskPath("task-1"));
        client.QueryDataSource("ds-1").Single().Archived = true;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        // The base entry (and the archived page id) is retired from the persisted snapshot.
        var snapPath = BaseSnapshotStore.PathFor(_dydoRoot, "notion-slice");
        Assert.Null(new BaseSnapshotStore(snapPath).Get("task-1"));
        Assert.DoesNotContain(archivedId, File.ReadAllText(snapPath));

        // git restores the file with content identical to the old base. It must be treated as a NEW create,
        // round-tripping to Notion — never silently deleted.
        File.WriteAllText(TaskPath("task-1"), content);
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        Assert.True(File.Exists(TaskPath("task-1")));
        var live = client.QueryDataSource("ds-1").Where(p => !p.Archived).ToList();
        var recreated = Assert.Single(live);
        Assert.NotEqual(archivedId, recreated.Id);                        // a fresh page, not the archived one
        Assert.Equal(recreated.Id, new BaseSnapshotStore(snapPath).Get("task-1")!.ExternalId);
    }

    [Fact]
    public void Run_TwoRelationFieldsSharingStemAcrossTargetTypes_ResolvePerFieldTarget()
    {
        // Finding 3: Slice has two relations to different types (sprint -> Sprint, blocked-by -> Slice).
        // A Sprint doc and a Slice doc share the stem "alpha"; a merged map keyed by bare stem would send
        // one field to the wrong database's page. Per-field resolution keeps each pointed at its target type.
        WriteModel("""
            {
              "objects": [
                { "type": "Sprint", "dir": "project/sprints", "notionTitle": "Sprints",
                  "properties": { "title": { "type": "title" } } },
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": {
                    "title": { "type": "title" },
                    "sprint": { "type": "relation", "to": "Sprint" },
                    "blocked-by": { "type": "relation", "to": "Slice", "reverse": "blocks" } } }
              ]
            }
            """);
        File.Delete(Path.Combine(_dydoRoot, "project", "slices", "spine-task.md"));
        File.Delete(Path.Combine(_dydoRoot, "project", "sprints", "notion-sync.md"));
        Seed("project/sprints/alpha", "---\ntitle: Sprint Alpha\n---\n\nS.");
        Seed("project/slices/alpha", "---\ntitle: Task Alpha\n---\n\nT.");
        Seed("project/slices/worker", "---\ntitle: Worker\nsprint: alpha\nblocked-by: alpha\n---\n\nW.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        NotionPage PageIn(string ds, string title) =>
            client.QueryDataSource(ds).Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == title);
        var sprintAlpha = PageIn("ds-1", "Sprint Alpha");
        var taskAlpha = PageIn("ds-2", "Task Alpha");
        var worker = PageIn("ds-2", "Worker");

        Assert.NotEqual(sprintAlpha.Id, taskAlpha.Id);
        Assert.Equal(sprintAlpha.Id, worker.Properties["sprint"].Relation!.Single().Id);    // -> Sprint database
        Assert.Equal(taskAlpha.Id, worker.Properties["blocked-by"].Relation!.Single().Id);  // -> Slice database
    }

    [Fact]
    public void Run_UserFrontmatterKeyCollidingWithComputedProperty_SurvivesTicksUnchanged()
    {
        // Finding 4: a user's `done: true` frontmatter key collides with a computed formula property named
        // `done`. ToProperties never writes it and ToFields drops it on read, so the field normalizer must
        // drop it too — else the base records it once and it is silently deleted from the repo a tick later.
        WriteModel("""
            {
              "objects": [
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": {
                    "title": { "type": "title" },
                    "status": { "type": "select", "options": ["in-progress", "done"] },
                    "done": { "type": "formula", "expression": "prop(\"status\") == \"done\"" } } }
              ]
            }
            """);
        File.Delete(Path.Combine(_dydoRoot, "project", "slices", "spine-task.md"));
        Seed("project/slices/task-1", "---\ntitle: T1\nstatus: in-progress\ndone: true\n---\n\nBody.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var file = File.ReadAllText(TaskPath("task-1"));
        Assert.Contains("done: true", file);          // never deleted
        Assert.Contains("status: in-progress", file);  // and untouched
        // The computed key was never pushed to Notion as a stored property.
        Assert.False(client.QueryDataSource("ds-1").Single().Properties.ContainsKey("done"));
    }

    [Fact]
    public void Run_MidProvisionFailure_PersistsCreatedDatabases_RetryReusesThem_NoDuplicates()
    {
        // Finding 5 + review R2-1: a create throwing mid-provision (rate limit, network) must not lose the
        // databases already created — state is saved after EACH create, so the retry reuses the first N-1 and
        // creates only the rest. Crucially the reused parents' rollup/formula post-pass NEVER ran on the failed
        // run (the throw preceded it), so the retry must re-run the post-pass for those recorded-but-unpassed
        // types — else their attention-layer rollups/formulas would be silently, permanently missing.
        WriteModel("""
            {
              "objects": [
                { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
                  "properties": {
                    "title": { "type": "title" },
                    "progress": { "type": "rollup", "rollupRelation": "sprints", "rollupProperty": "done", "rollupFunction": "percent_checked" },
                    "health": { "type": "formula", "expression": "prop(\"progress\") > 0.5" } } },
                { "type": "Sprint", "dir": "project/sprints", "notionTitle": "Sprints",
                  "properties": {
                    "title": { "type": "title" },
                    "campaign": { "type": "relation", "to": "Campaign", "reverse": "sprints" },
                    "done": { "type": "checkbox" },
                    "task-progress": { "type": "rollup", "rollupRelation": "tasks", "rollupProperty": "done", "rollupFunction": "percent_checked" },
                    "sprint-health": { "type": "formula", "expression": "prop(\"task-progress\") > 0.5" } } },
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": {
                    "title": { "type": "title" },
                    "sprint": { "type": "relation", "to": "Sprint", "reverse": "tasks" },
                    "done": { "type": "checkbox" } } }
              ]
            }
            """);
        var client = new FakeNotionClient { FailCreateDatabaseAfter = 2 }; // Campaign, Sprint succeed; Slice throws

        Assert.Throws<NotionApiException>(
            () => NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()));

        var provisionPath = NotionProvisioner.PathFor(_dydoRoot);
        Assert.True(File.Exists(provisionPath));
        var recorded = new NotionProvisioner(client, provisionPath);
        Assert.NotNull(recorded.Lookup("Campaign"));
        Assert.NotNull(recorded.Lookup("Sprint"));
        Assert.Null(recorded.Lookup("Slice")); // the throwing create left no record
        Assert.Equal(2, client.CreatedDatabases.Count);
        // The recorded parents own a post-pass that never ran — the throw preceded the post-pass entirely.
        Assert.True(recorded.PostPassPending("Campaign"));
        Assert.True(recorded.PostPassPending("Sprint"));
        Assert.Empty(client.DataSourceUpdates);

        // Retry with a healthy client reuses Campaign + Sprint and creates only Slice — no duplicates.
        client.FailCreateDatabaseAfter = null;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        Assert.Equal(3, client.CreatedDatabases.Count);

        // The retry ran the deferred post-pass for the REUSED parents: their rollups and deferred formulas were
        // PATCHed onto their existing data sources (ds-1 Campaign, ds-2 Sprint), now that Slice exists.
        Assert.Contains(client.DataSourceUpdates, u => u.DataSourceId == "ds-1" && u.Request.Properties.ContainsKey("progress"));
        Assert.Contains(client.DataSourceUpdates, u => u.DataSourceId == "ds-1" && u.Request.Properties.ContainsKey("health"));
        Assert.Contains(client.DataSourceUpdates, u => u.DataSourceId == "ds-2" && u.Request.Properties.ContainsKey("task-progress"));
        Assert.Contains(client.DataSourceUpdates, u => u.DataSourceId == "ds-2" && u.Request.Properties.ContainsKey("sprint-health"));

        // Completion is now persisted, so a further idempotent tick re-runs no post-pass.
        var updatesAfterRetry = client.DataSourceUpdates.Count;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());
        Assert.Equal(updatesAfterRetry, client.DataSourceUpdates.Count);
        Assert.False(new NotionProvisioner(client, provisionPath).PostPassPending("Campaign"));
    }

    /// <summary>A three-type rollup/formula model (Campaign/Sprint/Slice) whose parents each own
    /// post-pass rollups and deferred formulas, used to exercise mid-post-pass crash recovery.</summary>
    private void WriteRollupModel() => WriteModel("""
        {
          "objects": [
            { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
              "properties": {
                "title": { "type": "title" },
                "progress": { "type": "rollup", "rollupRelation": "sprints", "rollupProperty": "done", "rollupFunction": "percent_checked" },
                "health": { "type": "formula", "expression": "prop(\"progress\") > 0.5" } } },
            { "type": "Sprint", "dir": "project/sprints", "notionTitle": "Sprints",
              "properties": {
                "title": { "type": "title" },
                "campaign": { "type": "relation", "to": "Campaign", "reverse": "sprints" },
                "done": { "type": "checkbox" },
                "task-progress": { "type": "rollup", "rollupRelation": "tasks", "rollupProperty": "done", "rollupFunction": "percent_checked" },
                "sprint-health": { "type": "formula", "expression": "prop(\"task-progress\") > 0.5" } } },
            { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
              "properties": {
                "title": { "type": "title" },
                "sprint": { "type": "relation", "to": "Sprint", "reverse": "tasks" },
                "done": { "type": "checkbox" } } }
          ]
        }
        """);

    [Fact]
    public void Run_CrashDuringPostPass_PersistsPerTypeCompletion_RetryResumesUnfinishedOnly()
    {
        // Finding 10: a throw DURING the rollup/formula post-pass must not force an already-completed type to
        // re-run. The post-pass runs child-first (Slice -> Sprint -> Campaign) and MarkPostPassDone persists
        // per type immediately, so a crash in Campaign's pass — after Sprint's two patches landed — leaves Sprint
        // done and only Campaign pending. The retry resumes Campaign alone.
        WriteRollupModel();
        var client = new FakeNotionClient { FailUpdateDataSourceAfter = 2 }; // Sprint's two patches land; Campaign's first throws

        Assert.Throws<NotionApiException>(
            () => NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()));

        var provisionPath = NotionProvisioner.PathFor(_dydoRoot);
        var recorded = new NotionProvisioner(client, provisionPath);
        Assert.False(recorded.PostPassPending("Slice")); // leaf: no post-pass work, marked done first
        Assert.False(recorded.PostPassPending("Sprint"));     // completed and persisted before the crash
        Assert.True(recorded.PostPassPending("Campaign"));    // crashed mid-post-pass, still pending
        Assert.Equal(2, client.DataSourceUpdates.Count);      // exactly Sprint's two patches

        // Retry: only Campaign's post-pass re-runs (both patches on ds-1), Sprint's does NOT.
        client.FailUpdateDataSourceAfter = null;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var newUpdates = client.DataSourceUpdates.Skip(2).ToList();
        Assert.All(newUpdates, u => Assert.Equal("ds-1", u.DataSourceId)); // Campaign is ds-1
        Assert.Contains(newUpdates, u => u.Request.Properties.ContainsKey("progress"));
        Assert.Contains(newUpdates, u => u.Request.Properties.ContainsKey("health"));
        Assert.False(new NotionProvisioner(client, provisionPath).PostPassPending("Campaign"));
    }

    [Fact]
    public void Run_DryRun_RecordedButUnpassedBoard_PreviewsExactlyThePendingPostPass()
    {
        // Finding 10: after a crash left Campaign recorded-but-unpassed, a dry-run must preview EXACTLY the
        // pending post-pass work — Campaign's rollup + formula — and nothing for the already-completed types.
        WriteRollupModel();
        var client = new FakeNotionClient { FailUpdateDataSourceAfter = 2 };
        Assert.Throws<NotionApiException>(
            () => NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()));

        client.FailUpdateDataSourceAfter = null;
        client.DataSourceUpdates.Clear();
        var output = new StringWriter();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: true, output);

        var lines = output.ToString().Split('\n');
        // Every database already exists, so all are reused; only Campaign still owes its post-pass.
        Assert.Contains(lines, l => l.Contains("Campaign") && l.Contains("would add rollup properties"));
        Assert.Contains(lines, l => l.Contains("Campaign") && l.Contains("would add formula properties"));
        Assert.DoesNotContain(lines, l => l.Contains("Sprint") && l.Contains("would add"));
        Assert.DoesNotContain(lines, l => l.Contains("Slice") && l.Contains("would add"));
        Assert.Empty(client.DataSourceUpdates); // dry-run wrote nothing
    }

    [Fact]
    public void Run_DryRun_ReusedDatabases_DoesNotClaimRollupOrFormulaPostPass()
    {
        // Finding 8: the rollup/formula post-pass runs only for CREATED databases, never reused ones. A dry-run
        // where every database already exists must not claim post-pass work a real run would never perform.
        WriteModel("""
            {
              "objects": [
                { "type": "Campaign", "dir": "project/campaigns", "notionTitle": "Campaigns",
                  "properties": {
                    "title": { "type": "title" },
                    "progress": { "type": "rollup", "rollupRelation": "sprints", "rollupProperty": "done", "rollupFunction": "percent_checked" } } },
                { "type": "Sprint", "dir": "project/sprints", "notionTitle": "Sprints",
                  "properties": {
                    "title": { "type": "title" },
                    "campaign": { "type": "relation", "to": "Campaign", "reverse": "sprints" } } }
              ]
            }
            """);
        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // provision for real

        var output = new StringWriter();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: true, output);

        var text = output.ToString();
        Assert.Contains("reuse data source", text);
        Assert.DoesNotContain("would add rollup", text);
        Assert.DoesNotContain("would add formula", text);
    }

    [Fact]
    public void Run_AllUnresolvableRelation_RealEmptyRelationEcho_ConvergesNoChurn_ThenPushesCleanlyOnResolve()
    {
        // Finding 6, spine-level with the REALISTIC Notion echo. task-x is blocked by task-later, which does not
        // exist yet — so its relation is all-unresolvable and the adapter omits it on create. Real Notion returns
        // every schema property, so task-x's page reads blocked-by back as an EMPTY relation (EchoEmptyRelations).
        // The normalized base never recorded that key, so the empty echo must be treated as absent: no repeated
        // WriteToRepo churn while task-later is missing, and a CLEAN push — never a phantom conflict — once it
        // resolves.
        SeedSelfRelationSpine();
        Seed("project/slices/task-x", "---\ntitle: X\nblocked-by: task-later\n---\n\nX body.");

        var client = new FakeNotionClient { EchoEmptyRelations = true };
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1: create task-x

        var xPath = TaskPath("task-x");
        var afterCreate = File.ReadAllText(xPath);

        // Several no-op ticks while task-later is still missing: the empty-relation echo must not churn the file
        // (no phantom `blocked-by:` clear written back) and must report no conflict.
        for (var i = 0; i < 3; i++)
        {
            var output = new StringWriter();
            NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, output);
            Assert.DoesNotContain("conflict", output.ToString());
            Assert.Equal(afterCreate, File.ReadAllText(xPath)); // byte-identical: no WriteToRepo churn
        }

        // task-later appears; the blocker now resolves. It must push cleanly, no phantom merge conflict.
        Seed("project/slices/task-later", "---\ntitle: Later\n---\n\nLater body.");
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // create task-later
        var resolveOutput = new StringWriter();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, resolveOutput); // task-later resolves

        Assert.DoesNotContain("conflict", resolveOutput.ToString());

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == t);
        Assert.Equal(PageByTitle("Later").Id, PageByTitle("X").Properties["blocked-by"].Relation!.Single().Id);
        Assert.Contains("blocked-by: task-later", File.ReadAllText(xPath)); // repo value never blanked
    }

    [Fact]
    public void Run_ExternalPageArchived_RepoHasAllUnresolvableRelation_ResurrectsNotDeleted_ProductionChain()
    {
        // Finding 10 (COVERAGE), against the PRODUCTION NotionSyncAdapter chain — the real NormalizeFields and its
        // IsRelationKey empty-probe, not a test-double normalizer. task-x carries an all-unresolvable relation
        // (blocked-by: task-ghost, never created) that the normalizer drops WHOLE. When its page is archived, the
        // all-unresolvable delete guard must recognise the dropped relation as un-pushed work (the empty-value
        // probe proves blocked-by is a relation, told apart from a local-only key) and resurrect the page rather
        // than silently deleting the file and losing task-ghost forever.
        SeedSelfRelationSpine();
        Seed("project/slices/task-x", "---\ntitle: X\nblocked-by: task-ghost\n---\n\nX body.");

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // create task-x, blocker omitted

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => NotionRichText.Flatten(p.Properties["title"].Title) == t);
        var oldXId = PageByTitle("X").Id;

        // The board archives task-x's page; task-x's repo file is untouched but carries the all-unresolvable relation.
        PageByTitle("X").Archived = true;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        var xPath = TaskPath("task-x");
        Assert.True(File.Exists(xPath)); // not silently deleted
        Assert.Contains("blocked-by: task-ghost", File.ReadAllText(xPath));
        var live = client.QueryDataSource("ds-1").Where(p => !p.Archived && NotionRichText.Flatten(p.Properties["title"].Title) == "X").ToList();
        var resurrected = Assert.Single(live);
        Assert.NotEqual(oldXId, resurrected.Id); // resurrected as a fresh page, not the archived one
    }

    /// <summary>A single-type Slice model with a couple of plain docs, for the re-provision base-reset tests.</summary>
    private void SeedReprovisionSpine()
    {
        WriteModel("""
            {
              "objects": [
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": { "title": { "type": "title" },
                    "status": { "type": "select", "options": ["open", "done"] } } }
              ]
            }
            """);
        File.Delete(Path.Combine(_dydoRoot, "project", "slices", "spine-task.md")); // ctor seed, off-model
        Seed("project/slices/task-1", "---\ntitle: T1\nstatus: open\n---\n\nBody 1.");
        Seed("project/slices/task-2", "---\ntitle: T2\nstatus: open\n---\n\nBody 2.");
    }

    [Fact]
    public void Run_ReprovisionDefinitiveNotFound_ResetsBaseSnapshot_RepushesAsCreates_ZeroRepoDeletions()
    {
        // Finding 1 (HIGH). The recorded database is definitively gone (404/object_not_found), so the type
        // re-provisions into a fresh EMPTY database. The type's base snapshot still points at the OLD database's
        // pages, so reconciling it would read every external as ExternalDeleted and mass-delete the repo. The base
        // must be reset on re-provision so every repo doc re-pushes as a CREATE — ZERO repo file deletions.
        SeedReprovisionSpine();

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1: ds-1 + 2 pages
        Assert.Equal(2, client.QueryDataSource("ds-1").Count);

        // The recorded database is definitively gone; the next tick re-provisions into a fresh database.
        client.FailRetrieveDatabase = new NotionApiException(404, "{\"code\":\"object_not_found\"}");
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 2: re-provision

        Assert.True(File.Exists(TaskPath("task-1")));  // never deleted
        Assert.True(File.Exists(TaskPath("task-2")));
        Assert.Equal(2, client.CreatedDatabases.Count);        // a fresh database was minted
        Assert.Equal(2, client.QueryDataSource("ds-2").Count); // both docs re-pushed as creates into it
    }

    [Fact]
    public void Run_ReprovisionThenAbortBeforeReconcile_BaseResetIsDurable_RetryHasZeroRepoDeletions()
    {
        // Finding 1 (HIGH), DURABILITY (review R2-1). A re-provision mints a fresh EMPTY database, but the tick
        // then ABORTS before Reconcile — CheckDrift makes a live API call per type between Provision and Reconcile,
        // and a transient 429/5xx there (or a throw in the adapter's external read, or a process kill) leaves the
        // fresh database recorded while an in-memory-only reset never persists. So the reset MUST be durable at
        // mint time: the snapshot file is deleted the instant the database is minted. Otherwise the NEXT run
        // reuses the now-valid empty database, mints nothing, never resets, and the stale snapshot makes it read
        // every base+repo pair as an external delete — mass-deleting the repo. The retry must delete ZERO files.
        SeedReprovisionSpine();

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1: ds-1 + 2 pages
        Assert.Equal(2, client.QueryDataSource("ds-1").Count);

        // Tick 2: the recorded database is definitively gone, forcing a re-provision into a fresh database — but
        // CheckDrift then throws (a transient drift-probe failure), aborting the tick before Reconcile ever runs.
        client.FailRetrieveDatabase = new NotionApiException(404, "{\"code\":\"object_not_found\"}");
        client.FailRetrieveDataSource = new NotionApiException(429, "simulated transient drift-probe failure");
        Assert.Throws<NotionApiException>(
            () => NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()));

        // The fresh database was minted, and its base snapshot was durably cleared at mint time (file deleted),
        // even though the aborted tick never reached the end-of-tick base Save.
        Assert.Equal(2, client.CreatedDatabases.Count);
        Assert.False(File.Exists(BaseSnapshotStore.PathFor(_dydoRoot, "notion-slice")));

        // Tick 3: a healthy retry. Lookup reuses the now-valid empty ds-2 (nothing minted this run). With a durable
        // reset the base is empty, so both docs re-push as CREATES — never read as external deletes. Zero deletions.
        client.FailRetrieveDatabase = null;
        client.FailRetrieveDataSource = null;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        Assert.True(File.Exists(TaskPath("task-1")));
        Assert.True(File.Exists(TaskPath("task-2")));
        Assert.Equal(2, client.QueryDataSource("ds-2").Count); // both re-pushed as creates into the fresh database
    }

    [Fact]
    public void Run_ReprovisionDataSourceMismatch_ResetsBaseSnapshot_ZeroRepoDeletions()
    {
        // Finding 1 variant. The database still EXISTS but no longer owns the recorded data source (it was
        // replaced), so StillValid returns false with no exception. The re-provision path resets the base
        // identically — no repo deletions, docs re-pushed as creates into the new data source.
        SeedReprovisionSpine();

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1

        // The database now reports a DIFFERENT data source than the one recorded.
        client.Databases["db-1"].DataSources = [new NotionDataSourceRef { Id = "ds-orphan", Name = "Tasks" }];
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 2: re-provision

        Assert.True(File.Exists(TaskPath("task-1")));
        Assert.True(File.Exists(TaskPath("task-2")));
        Assert.Equal(2, client.CreatedDatabases.Count);
        Assert.Equal(2, client.QueryDataSource("ds-2").Count);
    }

    /// <summary>A parent Sprint + child Slice model whose child carries a `sprint` relation, seeded with one
    /// of each, for the cross-type re-provision test (finding 1).</summary>
    private void SeedParentChildRelationSpine()
    {
        WriteModel("""
            {
              "objects": [
                { "type": "Sprint", "dir": "project/sprints", "notionTitle": "Sprints",
                  "properties": { "title": { "type": "title" } } },
                { "type": "Slice", "dir": "project/slices", "notionTitle": "Tasks",
                  "properties": { "title": { "type": "title" },
                    "sprint": { "type": "relation", "to": "Sprint" } } }
              ]
            }
            """);
        File.Delete(Path.Combine(_dydoRoot, "project", "sprints", "notion-sync.md"));     // ctor seed, off-model
        File.Delete(Path.Combine(_dydoRoot, "project", "slices", "spine-task.md")); // ctor seed, off-model
        Seed("project/sprints/sprint-7", "---\ntitle: Sprint 7\n---\n\nParent.");
        Seed("project/slices/task-1", "---\ntitle: Task 1\nsprint: sprint-7\n---\n\nChild.");
    }

    [Fact]
    public void Run_ReprovisionParent_ChildRelationSurvives_RePushesToNewParentPage_ZeroSilentClears()
    {
        // Finding 1 (HIGH, the cross-type crux — spine-level acceptance bar). A PARENT type's database 404s (deleted
        // or unshared), so it re-provisions into a fresh EMPTY database and its page is re-created with a NEW id. Its
        // CHILD type reuses its own still-valid database, so the child's relation on the board still points at the
        // OLD parent page — a raw id the read cannot resolve, leaving the external's resolvable subset EMPTY. The
        // child's repo value still resolves (the write map holds the NEW parent page) and equals base. The engine
        // must treat the board echo as STALE: keep the child's relation in the repo (never clear it), and RE-PUSH it
        // so the child page points at the new parent page. Converges in <=2 ticks with zero silent clears.
        SeedParentChildRelationSpine();

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1: provision both

        NotionPage Child() => client.QueryDataSource("ds-2").Single(p => !p.Archived);
        var oldParentPageId = client.QueryDataSource("ds-1").Single().Id;
        Assert.Equal(oldParentPageId, Child().Properties["sprint"].Relation!.Single().Id); // baseline: child -> old parent page

        // The PARENT database is definitively gone; only it re-provisions on the next tick (the child reuses ds-2).
        client.NotFoundDatabaseIds.Add("db-1");
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 2: parent re-mints; child stale echo -> re-push

        // The child's relation SURVIVED in the repo — never silently cleared to empty.
        Assert.Contains("sprint: sprint-7", File.ReadAllText(TaskPath("task-1")));

        // The child page was RE-PUSHED to the NEW parent page (a fresh database ds-3), not left dangling or cleared.
        var newParentPageId = client.QueryDataSource("ds-3").Single().Id;
        Assert.NotEqual(oldParentPageId, newParentPageId);
        Assert.Equal(newParentPageId, Child().Properties["sprint"].Relation!.Single().Id);

        // Tick 3: the relation has converged — the board now resolves it, so the child's sprint is stable (no
        // conflict, still pointed at the new parent page, still `sprint: sprint-7` in frontmatter). The relation no
        // longer churns: the finding-1 stale echo is gone once the board points at the re-minted pages.
        var output = new StringWriter();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, output);
        Assert.DoesNotContain("conflict", output.ToString());
        Assert.Contains("sprint: sprint-7", File.ReadAllText(TaskPath("task-1")));
        Assert.Equal(newParentPageId, Child().Properties["sprint"].Relation!.Single().Id);
    }

    [Fact]
    public void Run_ReprovisionSnapshotDeleteFails_AbortsBeforeMintingDatabase()
    {
        // Finding 2 (ordering). The base-snapshot reset now runs BEFORE the mint. If the delete FAILS (a share-lock
        // from AV / OneDrive / another process on the snapshot file), the re-provision must abort BEFORE any database
        // is created — a data-preserving order — rather than minting a fresh database against a stale snapshot that
        // would mass-delete the repo next run. Here the snapshot file is held open exclusively, so DeleteSnapshot
        // throws and no new database is minted.
        SeedReprovisionSpine();

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1: ds-1 + 2 pages
        Assert.Single(client.CreatedDatabases);

        // The recorded database is definitively gone, so tick 2 would re-provision — but the snapshot delete fails.
        client.FailRetrieveDatabase = new NotionApiException(404, "{\"code\":\"object_not_found\"}");
        using (new UndeletableFile(BaseSnapshotStore.PathFor(_dydoRoot, "notion-slice")))
            Assert.Throws<IOException>(
                () => NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()));

        // Delete precedes create: the failed reset aborted before any fresh database was minted.
        Assert.Single(client.CreatedDatabases);
    }

    [Fact]
    public void Run_ReprovisionSnapshotDeletedThenCreateThrows_RetryReminsFresh_ZeroRepoDeletions()
    {
        // Finding 1 (the NEW crash window opened by the wave-8 delete-before-create ordering). The reset now
        // deletes the snapshot BEFORE the mint, so a crash can land AFTER a SUCCESSFUL snapshot delete but
        // BEFORE provisioner.Create — leaving the snapshot file gone while provision.json still records the
        // now-dead database. The ordering claims this is data-preserving ("just re-mints next run"): the retry
        // must see the recorded database still gone, re-provision into a fresh EMPTY database, and re-push every
        // repo doc as a CREATE against the empty base — ZERO repo deletions. This pins that exact window, which
        // the delete-FAILURE test (aborts before the window) and the post-mint abort test (the OLD window) do not.
        SeedReprovisionSpine();

        var client = new FakeNotionClient();
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1: ds-1 + 2 pages
        Assert.Equal(2, client.QueryDataSource("ds-1").Count);

        // Tick 2: the recorded database is definitively gone (404), forcing a re-provision. The snapshot delete
        // succeeds, but the mint then throws — the create knob fires the instant zero creates remain permitted,
        // so provisioner.Create fails immediately after DeleteSnapshot removed the file. This is the crash window.
        var snapshotPath = BaseSnapshotStore.PathFor(_dydoRoot, "notion-slice");
        client.FailRetrieveDatabase = new NotionApiException(404, "{\"code\":\"object_not_found\"}");
        client.FailCreateDatabaseAfter = 0;
        Assert.Throws<NotionApiException>(
            () => NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()));

        // The window's exact state: the snapshot file is gone (the delete succeeded), yet no fresh database was
        // minted (the create threw) and provision.json still records the dead database db-1.
        Assert.False(File.Exists(snapshotPath));
        Assert.Single(client.CreatedDatabases);
        Assert.Contains("db-1", File.ReadAllText(NotionProvisioner.PathFor(_dydoRoot)));

        // Tick 3: the transient create failure clears, but the database is still genuinely gone (FailRetrieveDatabase
        // stays set — that IS the reason we re-provision; clearing it would resurrect a phantom-valid db-1 whose stale
        // orphan pages the empty base cannot map, filing junk page-id repo docs instead of re-minting). The retry
        // re-provisions into a fresh EMPTY database (ds-2) and, against the durably-cleared base, re-pushes both docs
        // as creates. No repo file is ever deleted.
        client.FailCreateDatabaseAfter = null;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter());

        Assert.True(File.Exists(TaskPath("task-1")));  // never deleted
        Assert.True(File.Exists(TaskPath("task-2")));
        Assert.Equal(2, client.CreatedDatabases.Count);        // a fresh database was minted on the retry
        Assert.Equal(2, client.QueryDataSource("ds-2").Count); // both docs re-pushed as creates into it
    }

    [Fact]
    public void Run_RelationLifecycle_PendingResolvesRetiresBoardEdits_NoImmortalRawId_ArchivePropagates()
    {
        // GATES multi-tick lifecycle + finding 3, against the production NotionSyncAdapter chain. The blocked-by
        // relation runs end to end: pending → resolves → its target RETIRES → the board edits while still
        // referencing the archived page → the board archives the doc. The overlay must never inject the retired
        // target's raw Notion page id into frontmatter (finding 3), and the archive must ultimately delete the
        // repo file — not resurrect forever as a conflict blocked by an immortal raw-id entry.
        SeedSelfRelationSpine();
        Seed("project/slices/task-x", "---\ntitle: X\nblocked-by: task-later\n---\n\nX body.");

        var client = new FakeNotionClient { EchoEmptyRelations = true };
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1: create task-x (blocker omitted)
        Seed("project/slices/task-later", "---\ntitle: Later\n---\n\nLater body.");
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 2: create task-later
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 3: blocker resolves, pushed

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => !p.Archived && NotionRichText.Flatten(p.Properties["title"].Title) == t);
        var laterPageId = PageByTitle("Later").Id;
        Assert.Equal(laterPageId, PageByTitle("X").Properties["blocked-by"].Relation!.Single().Id);

        // Retire task-later: delete its repo file AND archive its page; its local↔page mapping is gone next tick.
        File.Delete(TaskPath("task-later"));
        PageByTitle("Later").Archived = true;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 4: task-later retires

        // The board renames task-x while its relation still points at task-later's archived page — which now
        // renders as a raw Notion page id on read. The overlay must NOT plant that raw id in the repo file.
        PageByTitle("X").Properties["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("X Renamed") };
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 5: WriteToRepo (rename)

        var fileX = File.ReadAllText(TaskPath("task-x"));
        Assert.Contains("X Renamed", fileX);
        Assert.DoesNotContain(laterPageId, fileX); // finding 3: no immortal raw Notion page id in frontmatter

        // The board archives task-x: with no un-pushed raw-id entry, the delete propagates rather than resurrecting.
        PageByTitle("X Renamed").Archived = true;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 6: archive propagates
        Assert.False(File.Exists(TaskPath("task-x")));
    }

    [Fact]
    public void Run_RelationLifecycle_PartialMultiValue_OneTargetRetires_BoardEdit_NoImmortalRawId_ArchivePropagates()
    {
        // Round-3 defect (finding 3, PENDING/pass-through branch) end to end against the production
        // NotionSyncAdapter chain. Unlike the single-entry lifecycle above (whole-field-invisible shape), task-x
        // has a TWO-target self-relation `blocked-by: task-a, task-later`: task-a stays live while task-later
        // RETIRES (file deleted + page archived) with the board still referencing its archived page — which
        // renders as a raw Notion page id on read. On a co-occurring board edit the overlay must strip the raw id
        // (finding 3) and drop the retired recorded entry (finding 4), leaving only the live task-a; the board
        // archive of task-x must then delete the repo file, not resurrect forever behind an immortal raw-id blocker.
        SeedSelfRelationSpine();
        Seed("project/slices/task-a", "---\ntitle: A\n---\n\nA body.");
        Seed("project/slices/task-x", "---\ntitle: X\nblocked-by: task-a, task-later\n---\n\nX body.");

        var client = new FakeNotionClient { EchoEmptyRelations = true };
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 1: create task-a, task-x (blockers omitted)
        Seed("project/slices/task-later", "---\ntitle: Later\n---\n\nLater body.");
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 2: create task-later; task-a resolves, pushed
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 3: task-later resolves, both pushed

        NotionPage PageByTitle(string t) =>
            client.QueryDataSource("ds-1").Single(p => !p.Archived && NotionRichText.Flatten(p.Properties["title"].Title) == t);
        var laterPageId = PageByTitle("Later").Id;
        Assert.Equal([PageByTitle("A").Id, laterPageId], PageByTitle("X").Properties["blocked-by"].Relation!.Select(r => r.Id));

        // Retire task-later: delete its repo file AND archive its page; its local↔page mapping is gone next tick.
        File.Delete(TaskPath("task-later"));
        PageByTitle("Later").Archived = true;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 4: task-later retires

        // The board renames task-x while its relation still references task-later's archived page (renders raw on read).
        PageByTitle("X").Properties["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("X Renamed") };
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 5: WriteToRepo (rename)

        var fileX = File.ReadAllText(TaskPath("task-x"));
        Assert.Contains("X Renamed", fileX);
        Assert.Contains("blocked-by: task-a", fileX);   // the live target survives as a local id
        Assert.DoesNotContain(laterPageId, fileX);      // finding 3: no immortal raw Notion page id in frontmatter
        Assert.DoesNotContain("task-later", fileX);     // finding 4: the retired recorded target was cleared

        // The board archives task-x: with no un-pushed raw-id/retired entry, the delete propagates.
        PageByTitle("X Renamed").Archived = true;
        NotionSpineSync.Run(client, _dydoRoot, "parent-page", dryRun: false, new StringWriter()); // tick 6: archive propagates
        Assert.False(File.Exists(TaskPath("task-x")));
    }
}
