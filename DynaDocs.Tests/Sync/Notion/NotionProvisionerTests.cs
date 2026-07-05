namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion;
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

        var record = provisioner.Create(_model.Object("Campaign"), "parent-page", new Dictionary<string, string> { ["Release"] = "ds-release" });

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
        // Sprint.campaign is a dual-property relation (model names its "sprints" reverse), so Campaign
        // gains the reverse column a rollup reads — single_property is dropped.
        Assert.Equal("dual_property", relation.Type);
        Assert.Null(relation.SingleProperty);
        Assert.Equal("sprints", relation.DualProperty!.SyncedPropertyName);
    }

    [Fact]
    public void Create_SinglePropertyRelation_WhenNoReverseNamed()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var type = new SyncObjectType
        {
            Type = "Item",
            NotionTitle = "Items",
            Properties = new()
            {
                ["title"] = new SyncPropertyDef { Type = "title" },
                ["owner"] = new SyncPropertyDef { Type = "relation", To = "Owner" },
            },
        };

        provisioner.Create(type, "parent-page", new Dictionary<string, string> { ["Owner"] = "ds-owner" });

        var relation = Assert.Single(client.CreatedDatabases).InitialDataSource.Properties["owner"].Relation;
        Assert.Equal("single_property", relation!.Type);
        Assert.NotNull(relation.SingleProperty);
        Assert.Null(relation.DualProperty);
    }

    [Fact]
    public void Create_SelectProperty_EmitsOptionColorsFromModel()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);

        provisioner.Create(_model.Object("SprintTask"), "parent-page",
            new Dictionary<string, string> { ["Sprint"] = "ds-sprint" });

        var props = Assert.Single(client.CreatedDatabases).InitialDataSource.Properties;
        var options = props["status"].Select!.Options;
        Assert.Equal("gray", options.Single(o => o.Name == "backlog").Color);
        Assert.Equal("purple", options.Single(o => o.Name == "ready").Color);
        Assert.Equal("red", options.Single(o => o.Name == "blocked").Color);
        // The canonical needs-human checkbox provisions a checkbox schema.
        Assert.NotNull(props["needs-human"].Checkbox);
    }

    [Fact]
    public void Create_FormulaAndDate_BuildSchemas_RollupIsDeferred()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);

        provisioner.Create(_model.Object("Sprint"), "parent-page",
            new Dictionary<string, string> { ["Campaign"] = "ds-campaign" });

        var props = Assert.Single(client.CreatedDatabases).InitialDataSource.Properties;
        // Formula (done) and dates are in the create schema; the rollup (progress) is deferred to AddRollups.
        Assert.Contains("prop", props["done"].Formula!.Expression);
        Assert.NotNull(props["start"].Date);
        Assert.False(props.ContainsKey("progress"));
        Assert.Empty(client.DataSourceUpdates);
    }

    [Fact]
    public void AddRollups_PatchesRollupPropertiesOntoTheDataSource()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var record = provisioner.Create(_model.Object("Sprint"), "parent-page",
            new Dictionary<string, string> { ["Campaign"] = "ds-campaign" });

        Assert.True(NotionProvisioner.HasRollups(_model.Object("Sprint")));
        provisioner.AddRollups(_model.Object("Sprint"));

        var (dataSourceId, update) = Assert.Single(client.DataSourceUpdates);
        Assert.Equal(record.DataSourceId, dataSourceId);
        var rollup = update.Properties["progress"].Rollup;
        Assert.Equal("tasks", rollup!.RelationPropertyName);
        Assert.Equal("done", rollup.RollupPropertyName);
        Assert.Equal("percent_checked", rollup.Function);
    }

    [Fact]
    public void Create_DefersFormulasThatReadRollupsOrOtherFormulas_KeepsLeafFormulasInline()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);

        provisioner.Create(_model.Object("SprintTask"), "parent-page",
            new Dictionary<string, string> { ["Sprint"] = "ds-sprint" });

        var props = Assert.Single(client.CreatedDatabases).InitialDataSource.Properties;
        // done + stale read only stored props, so they are created inline; the engine-owned last-activity
        // date column is created too.
        Assert.NotNull(props["done"].Formula);
        Assert.NotNull(props["stale"].Formula);
        Assert.NotNull(props["last-activity"].Date);
        // attention reads the stale formula, so it is deferred past the create.
        Assert.False(props.ContainsKey("attention"));
    }

    [Fact]
    public void AddFormulas_PatchesDeferredFormulas_InDependencyOrder()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        provisioner.Create(_model.Object("Sprint"), "parent-page",
            new Dictionary<string, string> { ["Campaign"] = "ds-campaign" });
        provisioner.AddRollups(_model.Object("Sprint"));
        client.DataSourceUpdates.Clear();

        Assert.True(NotionProvisioner.HasDeferredFormulas(_model.Object("Sprint")));
        provisioner.AddFormulas(_model.Object("Sprint"));

        // health (reads rollups) is patched before attention (reads health).
        var formulaPatches = client.DataSourceUpdates
            .Where(u => u.Request.Properties.Keys.Any(k => k is "health" or "attention"))
            .Select(u => u.Request.Properties.Keys.Single())
            .ToList();
        Assert.Equal(["health", "attention"], formulaPatches);
        var health = client.DataSourceUpdates.Single(u => u.Request.Properties.ContainsKey("health"));
        Assert.Contains("On Track", health.Request.Properties["health"].Formula!.Expression);
    }

    [Fact]
    public void Create_EngineComputedDate_IsProvisionedAsAPlainDateColumn()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);

        provisioner.Create(_model.Object("Issue"), "parent-page",
            new Dictionary<string, string> { ["Release"] = "ds-release" });

        var props = Assert.Single(client.CreatedDatabases).InitialDataSource.Properties;
        Assert.NotNull(props["last-activity"].Date);
    }

    [Fact]
    public void AddRollups_NoRollupProperties_IsNoOp()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        provisioner.Create(_model.Object("SprintTask"), "parent-page",
            new Dictionary<string, string> { ["Sprint"] = "ds-sprint" });
        client.DataSourceUpdates.Clear();

        Assert.False(NotionProvisioner.HasRollups(_model.Object("SprintTask")));
        provisioner.AddRollups(_model.Object("SprintTask"));
        Assert.Empty(client.DataSourceUpdates);
    }

    private static SyncObjectType SelfRelationType() => new()
    {
        Type = "SprintTask",
        NotionTitle = "Sprint Tasks",
        Properties = new()
        {
            ["title"] = new SyncPropertyDef { Type = "title" },
            ["sprint"] = new SyncPropertyDef { Type = "relation", To = "Sprint" },
            ["blocked-by"] = new SyncPropertyDef { Type = "relation", To = "SprintTask" },
        },
    };

    [Fact]
    public void Create_SelfRelation_CreatesWithoutIt_DefersToPostPass()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var type = SelfRelationType();

        var record = provisioner.Create(type, "parent-page", new Dictionary<string, string> { ["Sprint"] = "ds-sprint" });

        // The create carries the title and the cross-type relation, but NOT the self-relation — and it
        // does NOT PATCH during Create anymore: the self-relation is deferred to the post-pass (finding 3).
        var request = Assert.Single(client.CreatedDatabases);
        Assert.True(request.InitialDataSource.Properties.ContainsKey("sprint"));
        Assert.False(request.InitialDataSource.Properties.ContainsKey("blocked-by"));
        Assert.Equal("ds-sprint", request.InitialDataSource.Properties["sprint"].Relation!.DataSourceId);
        Assert.Empty(client.DataSourceUpdates);
        Assert.True(NotionProvisioner.HasSelfRelations(type));

        // The post-pass PATCHes the self-relation onto the just-created data source, pointing at itself.
        provisioner.AddSelfRelations(type);
        var (dataSourceId, update) = Assert.Single(client.DataSourceUpdates);
        Assert.Equal(record.DataSourceId, dataSourceId);
        var selfRelation = update.Properties["blocked-by"].Relation;
        Assert.NotNull(selfRelation);
        Assert.Equal(record.DataSourceId, selfRelation!.DataSourceId);
    }

    [Fact]
    public void Create_PersistsRecord_BeforeSelfRelationPatch_SoARetryDoesNotReCreate()
    {
        // A crash DURING the self-relation PATCH must not lose the created database id (finding 3). The
        // self-relation now runs in the post-pass; a throw there leaves the record persisted, so a retry
        // reuses the database and the post-pass completes it — never a duplicate board.
        var client = new FakeNotionClient { FailUpdateDataSourceAfter = 0 }; // the first UpdateDataSource throws
        var first = new NotionProvisioner(client, _statePath);
        var type = SelfRelationType();

        var record = first.Create(type, "parent-page", new Dictionary<string, string> { ["Sprint"] = "ds-sprint" });
        Assert.Throws<NotionApiException>(() => first.AddSelfRelations(type));

        // The database was created exactly once, and the record was persisted the instant it existed.
        Assert.Single(client.CreatedDatabases);

        // Retry over the same state + client: the database is retrievable and still owns the recorded data
        // source, so the type is REUSED (Lookup non-null) — no second CreateDatabase.
        client.FailUpdateDataSourceAfter = null;
        var second = new NotionProvisioner(client, _statePath);
        var reused = second.Lookup("SprintTask");
        Assert.NotNull(reused);
        Assert.Equal(record.DataSourceId, reused!.DataSourceId);
        Assert.Single(client.CreatedDatabases); // still one — never duplicated

        // The post-pass completes the deferred self-relation on the reused database.
        second.AddSelfRelations(type);
        var patch = Assert.Single(client.DataSourceUpdates);
        Assert.Equal(record.DataSourceId, patch.DataSourceId);
        Assert.NotNull(patch.Request.Properties["blocked-by"].Relation);
    }

    [Fact]
    public void AddSelfRelations_NoSelfRelation_IsNoOp()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        provisioner.Create(_model.Object("Sprint"), "parent-page",
            new Dictionary<string, string> { ["Campaign"] = "ds-campaign" });
        client.DataSourceUpdates.Clear();

        Assert.False(NotionProvisioner.HasSelfRelations(_model.Object("Sprint")));
        provisioner.AddSelfRelations(_model.Object("Sprint"));
        Assert.Empty(client.DataSourceUpdates);
    }

    [Fact]
    public void Create_NonSelfRelationsOnly_StaySinglePass_NoDataSourceUpdate()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);

        provisioner.Create(_model.Object("Sprint"), "parent-page", new Dictionary<string, string> { ["Campaign"] = "ds-campaign" });

        Assert.Single(client.CreatedDatabases);
        Assert.Empty(client.DataSourceUpdates);
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
            Properties = new() { ["x"] = new SyncPropertyDef { Type = "people" } },
        };

        Assert.Throws<SyncModelException>(() => provisioner.Create(type, "parent-page", new Dictionary<string, string>()));
    }

    [Fact]
    public void SaveThenReload_RecordedAndStillValidType_IsReused()
    {
        var client = new FakeNotionClient();
        var first = new NotionProvisioner(client, _statePath);
        var created = first.Create(_model.Object("Campaign"), "parent-page", new Dictionary<string, string> { ["Release"] = "ds-release" });

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
        provisioner.Create(_model.Object("Campaign"), "parent-page", new Dictionary<string, string> { ["Release"] = "ds-release" });
        // A client that no longer knows the database (RetrieveDatabase returns an empty DB with no
        // matching data source) must not reuse the stale record.
        var freshClient = new FakeNotionClient();
        var reloaded = new NotionProvisioner(freshClient, _statePath);
        Assert.Null(reloaded.Lookup("Campaign"));
    }

    [Fact]
    public void Lookup_TransientProbeFailure_Propagates_NoReprovisionNoReuse()
    {
        // Finding 1 (SEVERE). A recorded type's validity probe hitting a TRANSIENT Notion failure (429 rate
        // limit, or a 5xx) must NOT be misread as a deleted database — that would re-create an empty data
        // source and the same tick mass-delete every repo doc pointing at the now-absent pages. The exception
        // must propagate so the whole tick aborts before any create or delete.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        provisioner.Create(_model.Object("Campaign"), "parent-page", new Dictionary<string, string> { ["Release"] = "ds-release" });
        var reloaded = new NotionProvisioner(client, _statePath);
        var createsBefore = client.CreatedDatabases.Count;
        client.FailRetrieveDatabase = new NotionApiException(429, "rate_limited");
        var thrown = Assert.Throws<NotionApiException>(() => reloaded.Lookup("Campaign"));
        Assert.Equal(429, thrown.StatusCode);
        Assert.Equal(createsBefore, client.CreatedDatabases.Count); // the aborted probe re-created nothing
    }

    [Fact]
    public void Lookup_ServerErrorProbeFailure_Propagates()
    {
        // A 5xx during the probe is equally transient — it must abort the tick, never re-provision.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        provisioner.Create(_model.Object("Campaign"), "parent-page", new Dictionary<string, string> { ["Release"] = "ds-release" });
        var reloaded = new NotionProvisioner(client, _statePath);
        client.FailRetrieveDatabase = new NotionApiException(502, "bad_gateway");
        Assert.Throws<NotionApiException>(() => reloaded.Lookup("Campaign"));
    }

    [Fact]
    public void Lookup_DefinitiveNotFound_ReprovisionsAsToday()
    {
        // A DEFINITIVE not-found (HTTP 404 / object_not_found) is a genuinely deleted database: the record is
        // not reused, so the type re-provisions — the behaviour preserved from before finding 1's fix.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        provisioner.Create(_model.Object("Campaign"), "parent-page", new Dictionary<string, string> { ["Release"] = "ds-release" });
        var reloaded = new NotionProvisioner(client, _statePath);
        client.FailRetrieveDatabase = new NotionApiException(404, "{\"code\":\"object_not_found\"}");
        Assert.Null(reloaded.Lookup("Campaign"));
    }

    [Fact]
    public void Lookup_ObjectNotFoundCodeWithNon404_TreatedAsGone()
    {
        // Notion surfaces object_not_found in the error body; the code — not only the raw 404 — marks a gone
        // database, so it re-provisions rather than aborting.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        provisioner.Create(_model.Object("Campaign"), "parent-page", new Dictionary<string, string> { ["Release"] = "ds-release" });
        var reloaded = new NotionProvisioner(client, _statePath);
        client.FailRetrieveDatabase = new NotionApiException(400, "{\"object\":\"error\",\"code\":\"object_not_found\"}");
        Assert.Null(reloaded.Lookup("Campaign"));
    }
}
