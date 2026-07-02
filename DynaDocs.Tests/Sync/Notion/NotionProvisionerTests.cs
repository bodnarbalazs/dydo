namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Sync.Notion.Provisioning;

public class NotionProvisionerTests : IDisposable
{
    private readonly string _dir;
    private readonly string _statePath;
    private readonly SyncModel _model;

    public NotionProvisionerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "dydo-notion-prov-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        _statePath = Path.Combine(_dir, "provision.json");
        // Auto-seeds and loads the default Campaign -> Sprint -> Task model; provisioning is model-driven.
        _model = SyncModelLoader.Load(Path.Combine(_dir, "dydo"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Lookup_UnrecordedType_ReturnsNull()
    {
        var provisioner = new NotionProvisioner(new FakeNotionClient(), _statePath);
        Assert.Null(provisioner.Lookup("Campaign"));
    }

    [Fact]
    public void Create_PostsDatabase_UnderParentPage_WithTitleAndSchema()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);

        var record = provisioner.Create(_model.Object("Campaign"), "parent-page", new Dictionary<string, string>());

        var request = Assert.Single(client.CreatedDatabases);
        Assert.Equal("page_id", request.Parent.Type);
        Assert.Equal("parent-page", request.Parent.PageId);
        Assert.Equal("dydo Campaigns", NotionRichText.Flatten(request.Title));
        Assert.Equal("🚀", request.Icon!.Emoji);
        // Exactly one title property; the select carries its options from the model.
        Assert.NotNull(request.InitialDataSource.Properties["title"].Title);
        Assert.Equal(["proposed", "active", "done", "abandoned"], request.InitialDataSource.Properties["status"].Select!.Options.Select(o => o.Name));
        Assert.Equal("Campaign", record.ObjectType);
        Assert.Equal("ds-1", record.DataSourceId);
    }

    [Fact]
    public void Create_RelationProperty_ReferencesResolvedParentDataSourceId()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var resolved = new Dictionary<string, string> { ["Campaign"] = "ds-campaign" };

        provisioner.Create(_model.Object("Sprint"), "parent-page", resolved);

        var request = Assert.Single(client.CreatedDatabases);
        var relation = request.InitialDataSource.Properties["campaign"].Relation;
        Assert.NotNull(relation);
        Assert.Equal("ds-campaign", relation!.DataSourceId);
        Assert.NotNull(relation.SingleProperty);
    }

    [Fact]
    public void Create_DateAndRichTextProperties_BuildEmptyConfigs()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var type = new SyncObjectType
        {
            Type = "Note",
            NotionTitle = "Notes",
            Properties = new()
            {
                ["title"] = new SyncPropertyDef { Type = "title" },
                ["body"] = new SyncPropertyDef { Type = "rich_text" },
                ["due"] = new SyncPropertyDef { Type = "date" },
            },
        };

        provisioner.Create(type, "parent-page", new Dictionary<string, string>());

        var request = Assert.Single(client.CreatedDatabases);
        Assert.NotNull(request.InitialDataSource.Properties["body"].RichText);
        Assert.NotNull(request.InitialDataSource.Properties["due"].Date);
    }

    [Fact]
    public void Create_UnsupportedPropertyType_Throws()
    {
        var provisioner = new NotionProvisioner(new FakeNotionClient(), _statePath);
        var type = new SyncObjectType
        {
            Type = "Bad",
            NotionTitle = "Bad",
            Properties = new() { ["x"] = new SyncPropertyDef { Type = "checkbox" } },
        };

        Assert.Throws<SyncModelException>(() => provisioner.Create(type, "parent-page", new Dictionary<string, string>()));
    }

    [Fact]
    public void SaveThenReload_RecordedAndStillValidType_IsReused()
    {
        var client = new FakeNotionClient();
        var first = new NotionProvisioner(client, _statePath);
        var created = first.Create(_model.Object("Campaign"), "parent-page", new Dictionary<string, string>());
        first.Save();

        // A fresh provisioner over the same state + client: the database is retrievable and still
        // owns the recorded data source, so the type is reused rather than re-created.
        var second = new NotionProvisioner(client, _statePath);
        var reused = second.Lookup("Campaign");

        Assert.NotNull(reused);
        Assert.Equal(created.DataSourceId, reused!.DataSourceId);
    }

    [Fact]
    public void RecordedType_WhoseDatabaseIsGone_IsNotReused()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        provisioner.Create(_model.Object("Campaign"), "parent-page", new Dictionary<string, string>());
        provisioner.Save();

        // A client that no longer knows the database (RetrieveDatabase returns an empty DB with no
        // matching data source) must not reuse the stale record.
        var freshClient = new FakeNotionClient();
        var reloaded = new NotionProvisioner(freshClient, _statePath);
        Assert.Null(reloaded.Lookup("Campaign"));
    }
}
