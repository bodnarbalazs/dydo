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
