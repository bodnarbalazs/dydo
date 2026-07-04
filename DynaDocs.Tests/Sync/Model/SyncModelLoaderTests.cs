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
        Assert.Equal(["Release", "Campaign", "Sprint", "SprintTask", "Issue"], model.Objects.Select(o => o.Type));
    }

    [Fact]
    public void Load_DefaultModel_CarriesCanonicalDirsTitlesAndPropertyShapes()
    {
        var model = SyncModelLoader.Load(_dydoRoot);

        var campaign = model.Object("Campaign");
        Assert.Equal("project/campaigns", campaign.Dir);
        Assert.Equal("dydo Campaigns", campaign.NotionTitle);
        Assert.Equal("🚀", campaign.Icon);
        Assert.Equal("title", campaign.Properties["title"].Type);
        Assert.Equal(["proposed", "active", "done", "abandoned"], campaign.Properties["status"].Options!);

        var sprint = model.Object("Sprint");
        Assert.Equal("relation", sprint.Properties["campaign"].Type);
        Assert.Equal("Campaign", sprint.Properties["campaign"].To);
        Assert.Equal("number", sprint.Properties["seq"].Type);

        var sprintTask = model.Object("SprintTask");
        Assert.Equal("project/sprint-tasks", sprintTask.Dir);
        Assert.Equal("dydo Sprint Tasks", sprintTask.NotionTitle);
        Assert.Equal("Sprint", sprintTask.Properties["sprint"].To);
    }

    [Fact]
    public void Load_DefaultModel_CarriesReleaseAndIssueTypes()
    {
        var model = SyncModelLoader.Load(_dydoRoot);

        var release = model.Object("Release");
        Assert.Equal("project/releases", release.Dir);
        Assert.Equal("dydo Releases", release.NotionTitle);
        Assert.Equal("rich_text", release.Properties["specRef"].Type);
        Assert.Equal(["planned", "active", "shipped", "abandoned"], release.Properties["status"].Options!);

        // Campaign gains a relation to Release.
        var campaign = model.Object("Campaign");
        Assert.Equal("relation", campaign.Properties["release"].Type);
        Assert.Equal("Release", campaign.Properties["release"].To);

        var issue = model.Object("Issue");
        Assert.Equal("project/issues", issue.Dir);
        Assert.Equal(["open", "closed"], issue.Properties["status"].Options!);
        Assert.Equal("select", issue.Properties["severity"].Type);
    }

    [Fact]
    public void Load_DefaultModel_IssueStatusRoutesClosedIntoClosedSubfolder()
    {
        var issue = SyncModelLoader.Load(_dydoRoot).Object("Issue");

        var routing = issue.FolderRouting();
        Assert.NotNull(routing);
        Assert.Equal("status", routing.Value.Field);
        Assert.Equal("closed", routing.Value.Folders["closed"]);

        // Types without a folders map do not route.
        Assert.Null(SyncModelLoader.Load(_dydoRoot).Object("SprintTask").FolderRouting());
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
        Assert.Equal(["Release", "Campaign", "Sprint", "SprintTask", "Issue"], ordered.Select(o => o.Type));
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
        var task = SyncModelLoader.Load(_dydoRoot).Object("SprintTask");
        Assert.Equal(["Sprint"], task.RelationTargets());

        var campaign = SyncModelLoader.Load(_dydoRoot).Object("Campaign");
        Assert.Equal(["Release"], campaign.RelationTargets());

        var release = SyncModelLoader.Load(_dydoRoot).Object("Release");
        Assert.Empty(release.RelationTargets());
    }
}
