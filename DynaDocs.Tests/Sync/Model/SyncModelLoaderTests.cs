namespace DynaDocs.Tests.Sync.Model;

using DynaDocs.Sync.Model;

public class SyncModelLoaderTests : IDisposable
{
    private readonly string _dir;
    private readonly string _dydoRoot;

    public SyncModelLoaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dydo-syncmodel-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoRoot = Path.Combine(_dir, "dydo");
        Directory.CreateDirectory(_dydoRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private void WriteModel(string json)
    {
        var path = SyncModelLoader.PathFor(_dydoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    [Fact]
    public void Load_MissingFile_AutoSeedsDefaultModel_ToDisk()
    {
        var path = SyncModelLoader.PathFor(_dydoRoot);
        Assert.False(File.Exists(path));

        var model = SyncModelLoader.Load(_dydoRoot);

        Assert.True(File.Exists(path));
        Assert.Equal(["Release", "Campaign", "Sprint", "Slice", "Issue", "Task", "FutureFeature"], model.Objects.Select(o => o.Type));
    }

    [Fact]
    public void Load_DefaultModel_CarriesCanonicalDirsTitlesAndPropertyShapes()
    {
        var model = SyncModelLoader.Load(_dydoRoot);

        var campaign = model.Object("Campaign");
        Assert.Equal("project/campaigns", campaign.Dir);
        Assert.Equal("Campaigns", campaign.NotionTitle);
        Assert.Equal("🚀", campaign.Icon);
        Assert.Equal("title", campaign.Properties["title"].Type);
        Assert.Equal(["proposed", "active", "done", "abandoned"], campaign.Properties["status"].Options!);

        var sprint = model.Object("Sprint");
        Assert.Equal("relation", sprint.Properties["campaign"].Type);
        Assert.Equal("Campaign", sprint.Properties["campaign"].To);
        Assert.Equal("number", sprint.Properties["seq"].Type);

        var slice = model.Object("Slice");
        Assert.Equal("project/slices", slice.Dir);
        Assert.Equal("Sprint Tasks", slice.NotionTitle);
        Assert.Equal("Sprint", slice.Properties["sprint"].To);
    }

    [Fact]
    public void Load_DefaultModel_CarriesReleaseAndIssueTypes()
    {
        var model = SyncModelLoader.Load(_dydoRoot);

        var release = model.Object("Release");
        Assert.Equal("project/releases", release.Dir);
        Assert.Equal("Releases", release.NotionTitle);
        Assert.Equal("rich_text", release.Properties["specRef"].Type);
        Assert.Equal(["planned", "active", "shipped", "abandoned"], release.Properties["status"].Options!);

        // Campaign gains a relation to Release.
        var campaign = model.Object("Campaign");
        Assert.Equal("relation", campaign.Properties["release"].Type);
        Assert.Equal("Release", campaign.Properties["release"].To);

        var issue = model.Object("Issue");
        Assert.Equal("project/issues", issue.Dir);
        Assert.Equal(["triage", "open", "resolved"], issue.Properties["status"].Options!);
        Assert.Equal("select", issue.Properties["severity"].Type);
    }

    [Fact]
    public void Load_DefaultModel_CarriesColorsFormulaAndRollup()
    {
        var model = SyncModelLoader.Load(_dydoRoot);

        // DR 029 color language on a select option.
        var status = model.Object("Slice").Properties["status"];
        Assert.Equal("purple", status.Colors!["ready"]);
        Assert.Equal("red", status.Colors!["blocked"]);

        // DR 029 §2 word priorities with colors.
        Assert.Equal(["Urgent", "High", "Normal", "Low"], model.Object("Campaign").Properties["priority"].Options!);
        Assert.Equal("red", model.Object("Campaign").Properties["priority"].Colors!["Urgent"]);

        // Canonical dual-property relation carries its reverse name; a leaf relation does not.
        Assert.Equal("sprints", model.Object("Sprint").Properties["campaign"].Reverse);
        Assert.Equal("blocks", model.Object("Slice").Properties["blocked-by"].Reverse);
        Assert.Null(model.Object("Issue").Properties["fix-release"].Reverse);

        // Computed properties: a done formula and a progress rollup.
        var done = model.Object("Sprint").Properties["done"];
        Assert.Equal("formula", done.Type);
        Assert.Contains("status", done.Expression);

        var progress = model.Object("Sprint").Properties["progress"];
        Assert.Equal("rollup", progress.Type);
        Assert.Equal("tasks", progress.RollupRelation);
        Assert.Equal("done", progress.RollupProperty);
        Assert.Equal("percent_checked", progress.RollupFunction);

        // Canonical PM properties adopted in DR 030.
        Assert.Equal("checkbox", model.Object("Slice").Properties["needs-human"].Type);
        Assert.Equal(["feature", "bug", "chore", "spike", "docs"], model.Object("Slice").Properties["work-type"].Options!);
        Assert.Equal("date", model.Object("Sprint").Properties["start"].Type);
        Assert.Equal("date", model.Object("Sprint").Properties["end"].Type);
        Assert.Equal(["done", "wont-do", "duplicate", "cannot-reproduce", "superseded"], model.Object("Issue").Properties["resolution"].Options!);
    }

    [Fact]
    public void Load_DefaultModel_CarriesDR030AttentionLayer()
    {
        var model = SyncModelLoader.Load(_dydoRoot);

        // Engine-computed last-activity date on the leaf work items (DR 030 §3).
        var lastActivity = model.Object("Slice").Properties["last-activity"];
        Assert.Equal("date", lastActivity.Type);
        Assert.True(lastActivity.EngineComputed);
        Assert.True(model.Object("Issue").Properties["last-activity"].EngineComputed);

        // Staleness formula reads last-activity and status (DR 030 §3).
        var stale = model.Object("Slice").Properties["stale"];
        Assert.Equal("formula", stale.Type);
        Assert.Contains("last-activity", stale.Expression);
        Assert.Contains("dateBetween", stale.Expression);

        // Health formula on the parents (DR 030 §2).
        foreach (var parent in new[] { "Sprint", "Campaign", "Release" })
        {
            var health = model.Object(parent).Properties["health"];
            Assert.Equal("formula", health.Type);
            Assert.Contains("On Track", health.Expression);
        }
        Assert.Contains("gate-result", model.Object("Sprint").Properties["health"].Expression);

        // Canonical gate-result select on Sprint (pass=green, fail=red, no default).
        var gate = model.Object("Sprint").Properties["gate-result"];
        Assert.Equal(["pass", "fail"], gate.Options!);
        Assert.Equal("green", gate.Colors!["pass"]);
        Assert.False(gate.EngineComputed);

        // needs-human COUNT rollups on Sprint and Campaign (DR 030 §1).
        var sprintNeedsHuman = model.Object("Sprint").Properties["needs-human"];
        Assert.Equal("rollup", sprintNeedsHuman.Type);
        Assert.Equal("tasks", sprintNeedsHuman.RollupRelation);
        Assert.Equal("checked", sprintNeedsHuman.RollupFunction);
        // Campaign's needs-human sums a Sprint FORMULA projection (needs-human-count), not the Sprint
        // needs-human ROLLUP directly — Notion rejects a rollup-of-rollup, so the projection is required.
        var campaignNeedsHuman = model.Object("Campaign").Properties["needs-human"];
        Assert.Equal("rollup", campaignNeedsHuman.Type);
        Assert.Equal("sum", campaignNeedsHuman.RollupFunction);
        Assert.Equal("needs-human-count", campaignNeedsHuman.RollupProperty);
        var needsHumanCount = model.Object("Sprint").Properties["needs-human-count"];
        Assert.Equal("formula", needsHumanCount.Type);
        Assert.Contains("needs-human", needsHumanCount.Expression);

        // Campaign date rollups (earliest start / latest end across Sprints, DR 029/030).
        Assert.Equal("earliest_date", model.Object("Campaign").Properties["start"].RollupFunction);
        Assert.Equal("latest_date", model.Object("Campaign").Properties["end"].RollupFunction);

        // Attention composite on every type (DR 030 §4).
        foreach (var type in new[] { "Slice", "Issue", "Sprint", "Campaign", "Release" })
            Assert.Equal("formula", model.Object(type).Properties["attention"].Type);
    }

    [Fact]
    public void Load_PlainDateModel_WithoutEngineComputed_DefaultsFalse()
    {
        // A model with a plain date property and no engineComputed flag still loads; the new flag defaults
        // false, never breaking an older project's model file.
        WriteModel("""
            { "objects": [ { "type": "Note", "dir": "project/notes", "notionTitle": "Notes",
              "properties": {
                "title": { "type": "title" },
                "due": { "type": "date" } } } ] }
            """);

        var note = SyncModelLoader.Load(_dydoRoot).Object("Note");
        Assert.Equal("date", note.Properties["due"].Type);
        Assert.False(note.Properties["due"].EngineComputed);
    }

    [Fact]
    public void Load_PlainOptionsModel_LoadsBackwardCompatibly_WithoutColors()
    {
        // A pre-DR-029 model with plain string options and no colors/formula/rollup still loads:
        // the new fields default to null/false, never breaking an older project's model file.
        WriteModel("""
            { "objects": [ { "type": "Note", "dir": "project/notes", "notionTitle": "Notes",
              "properties": {
                "title": { "type": "title" },
                "status": { "type": "select", "options": ["open", "closed"] } } } ] }
            """);

        var note = SyncModelLoader.Load(_dydoRoot).Object("Note");
        Assert.Equal(["open", "closed"], note.Properties["status"].Options!);
        Assert.Null(note.Properties["status"].Colors);
        Assert.Null(note.Properties["title"].Expression);
    }

    [Fact]
    public void Load_DefaultModel_IssueStatusRoutesResolvedIntoResolvedSubfolder()
    {
        var issue = SyncModelLoader.Load(_dydoRoot).Object("Issue");

        var routing = issue.FolderRouting();
        Assert.NotNull(routing);
        Assert.Equal("status", routing.Value.Field);
        Assert.Equal("resolved", routing.Value.Folders["resolved"]);

        // Types without a folders map do not route.
        Assert.Null(SyncModelLoader.Load(_dydoRoot).Object("Slice").FolderRouting());
    }

    [Fact]
    public void Load_DefaultModel_CarriesTaskAndFutureFeatureTypes()
    {
        // DR 034: the two-altitude work funnel. Task is the tactical work unit (backlog partition, done
        // archive read from the changelog); FutureFeature is the strategic intake.
        var model = SyncModelLoader.Load(_dydoRoot);

        var task = model.Object("Task");
        Assert.Equal("project/tasks", task.Dir);
        Assert.Equal("Tasks", task.NotionTitle);
        Assert.Equal(["backlog", "in-progress", "in-review", "done"], task.Properties["status"].Options!);
        // backlog routes into tasks/backlog/; done is handler-placed (date-nested changelog) so it stays
        // unmapped and files at root — only backlog is in the folders map.
        var routing = task.FolderRouting();
        Assert.NotNull(routing);
        Assert.Equal("status", routing.Value.Field);
        Assert.Equal("backlog", routing.Value.Folders["backlog"]);
        Assert.False(routing.Value.Folders.ContainsKey("done"));

        var futureFeature = model.Object("FutureFeature");
        Assert.Equal("project/future-features", futureFeature.Dir);
        Assert.Equal("Future Features", futureFeature.NotionTitle);
        Assert.Equal(["raw", "shaping", "promoted", "dropped"], futureFeature.Properties["status"].Options!);
        // Low-volume strategic intake: no subfolder partition.
        Assert.Null(futureFeature.FolderRouting());
    }

    [Fact]
    public void Load_ExistingFile_IsReadVerbatim_NotReseeded()
    {
        WriteModel("""
            { "objects": [ { "type": "Note", "dir": "project/notes", "notionTitle": "Notes",
              "properties": { "title": { "type": "title" } } } ] }
            """);

        var model = SyncModelLoader.Load(_dydoRoot);

        var note = Assert.Single(model.Objects);
        Assert.Equal("Note", note.Type);
        Assert.Equal("project/notes", note.Dir);
    }

    [Fact]
    public void Load_EmptyModel_Throws()
    {
        WriteModel("""{ "objects": [] }""");
        Assert.Throws<SyncModelException>(() => SyncModelLoader.Load(_dydoRoot));
    }

    [Fact]
    public void InDependencyOrder_DefaultModel_PutsParentsBeforeChildren()
    {
        var ordered = SyncModelLoader.Load(_dydoRoot).InDependencyOrder();
        Assert.Equal(["Release", "Campaign", "Sprint", "Slice", "Issue", "Task", "FutureFeature"], ordered.Select(o => o.Type));
    }

    [Fact]
    public void InDependencyOrder_RegardlessOfFileOrder_SortsTopologically()
    {
        // Children declared before their parents in the file: the sort must still place parents first.
        WriteModel("""
            { "objects": [
              { "type": "Task", "dir": "t", "notionTitle": "T",
                "properties": { "title": { "type": "title" }, "sprint": { "type": "relation", "to": "Sprint" } } },
              { "type": "Sprint", "dir": "s", "notionTitle": "S",
                "properties": { "title": { "type": "title" }, "campaign": { "type": "relation", "to": "Campaign" } } },
              { "type": "Campaign", "dir": "c", "notionTitle": "C",
                "properties": { "title": { "type": "title" } } }
            ] }
            """);

        var ordered = SyncModelLoader.Load(_dydoRoot).InDependencyOrder();
        Assert.Equal(["Campaign", "Sprint", "Task"], ordered.Select(o => o.Type));
    }

    [Fact]
    public void InDependencyOrder_RelationCycle_Throws()
    {
        WriteModel("""
            { "objects": [
              { "type": "A", "dir": "a", "notionTitle": "A",
                "properties": { "title": { "type": "title" }, "b": { "type": "relation", "to": "B" } } },
              { "type": "B", "dir": "b", "notionTitle": "B",
                "properties": { "title": { "type": "title" }, "a": { "type": "relation", "to": "A" } } }
            ] }
            """);

        var ex = Assert.Throws<SyncModelException>(() => SyncModelLoader.Load(_dydoRoot).InDependencyOrder());
        Assert.Contains("cycle", ex.Message);
    }

    [Fact]
    public void InDependencyOrder_SelfRelation_IsLegal_DoesNotThrow()
    {
        // A self-relation (a property whose target is its own type, e.g. blocked-by) is a within-type
        // edge, not a cross-type cycle. It must order fine and place parents before children as usual.
        WriteModel("""
            { "objects": [
              { "type": "Sprint", "dir": "s", "notionTitle": "S",
                "properties": { "title": { "type": "title" } } },
              { "type": "Slice", "dir": "t", "notionTitle": "T",
                "properties": { "title": { "type": "title" },
                  "sprint": { "type": "relation", "to": "Sprint" },
                  "blocked-by": { "type": "relation", "to": "Slice" } } }
            ] }
            """);

        var ordered = SyncModelLoader.Load(_dydoRoot).InDependencyOrder();
        Assert.Equal(["Sprint", "Slice"], ordered.Select(o => o.Type));
    }

    [Fact]
    public void InDependencyOrder_UnknownRelationTarget_Throws()
    {
        WriteModel("""
            { "objects": [
              { "type": "Sprint", "dir": "s", "notionTitle": "S",
                "properties": { "title": { "type": "title" }, "campaign": { "type": "relation", "to": "Campaign" } } }
            ] }
            """);

        var ex = Assert.Throws<SyncModelException>(() => SyncModelLoader.Load(_dydoRoot).InDependencyOrder());
        Assert.Contains("unknown type 'Campaign'", ex.Message);
    }

    [Fact]
    public void Object_UnknownType_Throws()
    {
        var model = SyncModelLoader.Load(_dydoRoot);
        Assert.Throws<SyncModelException>(() => model.Object("Nope"));
    }

    [Fact]
    public void ObjectType_FieldSchema_MapsEveryPropertyToItsType()
    {
        var sprint = SyncModelLoader.Load(_dydoRoot).Object("Sprint");
        var schema = sprint.FieldSchema();
        Assert.Equal("title", schema["title"]);
        Assert.Equal("number", schema["seq"]);
        Assert.Equal("relation", schema["campaign"]);
    }

    [Fact]
    public void ObjectType_RelationTargets_ReturnsDistinctReferencedTypes()
    {
        var task = SyncModelLoader.Load(_dydoRoot).Object("Slice");
        // sprint → Sprint, and the blocked-by self-relation → Slice.
        Assert.Equal(["Sprint", "Slice"], task.RelationTargets());

        var campaign = SyncModelLoader.Load(_dydoRoot).Object("Campaign");
        Assert.Equal(["Release"], campaign.RelationTargets());

        var release = SyncModelLoader.Load(_dydoRoot).Object("Release");
        Assert.Empty(release.RelationTargets());
    }
}
