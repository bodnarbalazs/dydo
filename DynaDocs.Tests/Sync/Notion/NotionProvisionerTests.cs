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
        Assert.Equal("Campaigns", NotionRichText.Flatten(request.Title));
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

        provisioner.Create(_model.Object("Slice"), "parent-page",
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
    public void Create_SliceFormulas_AreAllInline_SinceNoneReadsARollupOrFormula()
    {
        // Notion rejects a formula that references another formula property (notion-sync-formula-fix), so
        // Slice.attention inlines the `stale` staleness condition rather than reading prop("stale").
        // Every Slice formula (done, stale, attention) now reads only stored properties, so all three
        // are created inline and NONE is deferred. The deferral paths stay covered elsewhere: rollup-reading
        // formulas by the Sprint tests, and a formula-reads-formula by AddFormulas_NotionRejectsExpression's
        // synthetic type.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);

        provisioner.Create(_model.Object("Slice"), "parent-page",
            new Dictionary<string, string> { ["Sprint"] = "ds-sprint" });

        var props = Assert.Single(client.CreatedDatabases).InitialDataSource.Properties;
        Assert.NotNull(props["done"].Formula);
        Assert.NotNull(props["stale"].Formula);
        Assert.NotNull(props["attention"].Formula);
        Assert.NotNull(props["last-activity"].Date);
        Assert.False(NotionProvisioner.HasDeferredFormulas(_model.Object("Slice")));
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
    public void DefaultModel_NoFormulaReferencesAnotherFormula_WhichNotionForbids()
    {
        // Notion (API 2026-03-11) rejects a formula whose expression references another formula property with
        // "Type error with formula" — verified live against every attention/health/stale combination
        // (notion-sync-formula-fix). Guard the shipped model against a regression: no formula may read
        // prop("<otherFormula>"); compose by inlining the referenced body instead.
        foreach (var type in _model.Objects)
        {
            var formulaNames = type.Properties.Where(p => p.Value.Type == "formula").Select(p => p.Key).ToList();
            foreach (var (name, def) in type.Properties.Where(p => p.Value.Type == "formula"))
                foreach (var other in formulaNames.Where(f => f != name))
                    Assert.DoesNotContain($"prop(\"{other}\")", def.Expression ?? "");
        }
    }

    [Fact]
    public void AddViews_EmitsDeclaredViews_WithOrderHideFilterAndGrouping()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        provisioner.Create(_model.Object("Issue"), "parent-page", new Dictionary<string, string> { ["Release"] = "ds-release" });

        Assert.True(NotionProvisioner.HasViews(_model.Object("Issue")));
        provisioner.AddViews(_model.Object("Issue"));

        var byName = client.CreatedViews.ToDictionary(v => v.Name);
        Assert.Contains("Open", byName.Keys);
        Assert.Contains("🚨 Needs Attention", byName.Keys);
        Assert.Contains("By Area", byName.Keys);

        // Open: status != resolved, sorted by severity ascending — options are critical-first, and Notion
        // sorts a select by option position, so ascending puts critical/high on top (issue #215).
        var open = byName["Open"];
        Assert.Equal("table", open.Type);
        Assert.Equal("resolved", open.Filter!.Select!.DoesNotEqual);
        Assert.Equal("severity", open.Sorts!.Single().Property);
        Assert.Equal("ascending", open.Sorts!.Single().Direction);
        // Compute-only helper hidden; a human column visible; the fake ids each property by name.
        var props = open.Configuration!.Properties!;
        Assert.False(props.Single(p => p.PropertyId == "last-activity").Visible);
        Assert.True(props.Single(p => p.PropertyId == "status").Visible);

        // Needs Attention targets needs-human — a checkbox on Issue (Notion can't filter the attention formula).
        Assert.True(byName["🚨 Needs Attention"].Filter!.Checkbox!.EqualsValue);
        // ...and sorts severity ascending too — the other half of the #215 fix (critical/high on top).
        Assert.Equal("severity", byName["🚨 Needs Attention"].Sorts!.Single().Property);
        Assert.Equal("ascending", byName["🚨 Needs Attention"].Sorts!.Single().Direction);

        // By Area is a board grouped by the area select.
        var board = byName["By Area"];
        Assert.Equal("board", board.Type);
        Assert.Equal("area", board.Configuration!.GroupBy!.PropertyId);
    }

    [Fact]
    public void Create_AmbiguousServerError_AdoptsAlreadyCreatedDatabaseByTitle_NoDuplicate()
    {
        // A CreateDatabase that 500s may have landed server-side (it no longer blind-retries a 5xx). The
        // provisioner must re-search, match the already-created database by title, and adopt it — never mint a
        // duplicate board (ns-5).
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);

        client.CreateDatabaseSucceedsThenAmbiguous5xx = true; // database persists + becomes discoverable, then 500s
        var record = provisioner.Create(_model.Object("Campaign"), "parent-page",
            new Dictionary<string, string> { ["Release"] = "ds-release" });

        Assert.Single(client.CreatedDatabases);        // one create — adopted, not re-created
        Assert.Equal("db-1", record.DatabaseId);
        Assert.Equal("ds-1", record.DataSourceId);
        // The adopted ids were persisted, so a reload reuses the database rather than minting another.
        var reloaded = new NotionProvisioner(client, _statePath);
        Assert.Equal("ds-1", reloaded.Lookup("Campaign")!.DataSourceId);
    }

    [Fact]
    public void Create_AmbiguousServerError_NoMatch_ReCreatesDatabase()
    {
        // When the ambiguous create truly failed (nothing persisted, nothing discoverable), the re-search finds
        // no match, so the database is re-created — the other half of the recovery (ns-5).
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);

        client.CreateDatabaseFailsAmbiguously = true; // 500s before persisting — search finds nothing
        var record = provisioner.Create(_model.Object("Campaign"), "parent-page",
            new Dictionary<string, string> { ["Release"] = "ds-release" });

        Assert.Single(client.CreatedDatabases); // exactly one surviving database — the re-created one
        Assert.Equal("db-1", record.DatabaseId);
    }

    [Fact]
    public void Create_AmbiguousServerError_SameTitleDifferentParent_IsNotAdopted_ReCreates()
    {
        // Search is workspace-wide; a same-titled board under ANOTHER parent page must never be adopted (review
        // major 2 — ns-1 supports multiple parents per token). The recovery confirms the hit's database sits
        // under THIS create's parent page; a mismatch falls through to re-create.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);

        // A foreign "Campaigns" board, discoverable, but owned by a DIFFERENT parent page.
        client.Databases["foreign-db"] = new NotionDatabase
        {
            Id = "foreign-db",
            Parent = new NotionParent { Type = "page_id", PageId = "other-parent" },
            DataSources = [new NotionDataSourceRef { Id = "foreign-ds", Name = "Campaigns" }],
        };
        client.DiscoverableDataSources.Add(new NotionSearchResult
        {
            Id = "foreign-ds", Object = "data_source", Name = "Campaigns",
            Parent = new NotionParent { Type = "database_id", DatabaseId = "foreign-db" },
        });

        client.CreateDatabaseFailsAmbiguously = true; // our own create fails ambiguously
        var record = provisioner.Create(_model.Object("Campaign"), "parent-page",
            new Dictionary<string, string> { ["Release"] = "ds-release" });

        // The foreign board was skipped (wrong parent) and a fresh database minted under our parent instead.
        Assert.NotEqual("foreign-db", record.DatabaseId);
        Assert.NotEqual("foreign-ds", record.DataSourceId);
        Assert.Equal("db-1", record.DatabaseId);
        Assert.Single(client.CreatedDatabases);
    }

    [Fact]
    public void Create_AmbiguousServerError_ParentEchoedDashed_ConfigUndashed_StillAdopts()
    {
        // Notion echoes parent page ids as dashed UUIDs while config typically holds the undashed form; the
        // adoption comparison must canonicalize both sides or recovery would silently never fire and mint a
        // duplicate board — the exact failure it exists to prevent.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);

        // The lost create landed server-side under OUR parent, echoed in dashed form.
        client.Databases["lost-db"] = new NotionDatabase
        {
            Id = "lost-db",
            Parent = new NotionParent { Type = "page_id", PageId = "1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d" },
            DataSources = [new NotionDataSourceRef { Id = "lost-ds", Name = "Campaigns" }],
        };
        client.DiscoverableDataSources.Add(new NotionSearchResult
        {
            Id = "lost-ds", Object = "data_source", Name = "Campaigns",
            Parent = new NotionParent { Type = "database_id", DatabaseId = "lost-db" },
        });

        client.CreateDatabaseFailsAmbiguously = true;
        var record = provisioner.Create(_model.Object("Campaign"), "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d",
            new Dictionary<string, string> { ["Release"] = "ds-release" });

        Assert.Equal("lost-db", record.DatabaseId);
        Assert.Equal("lost-ds", record.DataSourceId);
        Assert.Empty(client.CreatedDatabases);
    }

    [Fact]
    public void AddViews_AmbiguousViewCreate_AdoptsExistingViewByName_NoDuplicate()
    {
        // A CreateView that 500s may have landed server-side. The provisioner lists views, finds one already
        // carrying the name, and adopts it rather than re-creating a duplicate (ns-5).
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var record = provisioner.Create(_model.Object("Issue"), "parent-page",
            new Dictionary<string, string> { ["Release"] = "ds-release" });

        client.CreateViewSucceedsThenAmbiguous5xx = true; // the first declared view lands, then its create 500s
        provisioner.AddViews(_model.Object("Issue"));

        var declared = _model.Object("Issue").Views!;
        // Each declared view exists exactly once — the adopted one was recorded once and never re-created.
        Assert.Equal(declared.Count, client.CreatedViews.Count);
        var remaining = client.ListViews(record.DatabaseId);
        foreach (var view in declared)
            Assert.Equal(1, remaining.Count(v => v.Name == view.Name));
    }

    [Fact]
    public void AddViews_AmbiguousViewCreate_NoExistingView_ReCreates()
    {
        // When the ambiguous view create left nothing behind, the list-by-name finds no match, so the view is
        // re-created — the other half of the recovery (ns-5).
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var record = provisioner.Create(_model.Object("Issue"), "parent-page",
            new Dictionary<string, string> { ["Release"] = "ds-release" });

        client.CreateViewFailsAmbiguously = true; // the first view create 500s before recording anything
        provisioner.AddViews(_model.Object("Issue"));

        var declared = _model.Object("Issue").Views!;
        var remaining = client.ListViews(record.DatabaseId);
        foreach (var view in declared)
            Assert.Equal(1, remaining.Count(v => v.Name == view.Name));
    }

    [Fact]
    public void AddViews_TimelineView_CarriesRollupDateAxis()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        provisioner.Create(_model.Object("Campaign"), "parent-page", new Dictionary<string, string> { ["Release"] = "ds-release" });
        provisioner.AddRollups(_model.Object("Campaign"));
        provisioner.AddFormulas(_model.Object("Campaign"));

        provisioner.AddViews(_model.Object("Campaign"));

        var roadmap = client.CreatedViews.Single(v => v.Type == "timeline");
        Assert.Equal("start", roadmap.Configuration!.DatePropertyId);
        Assert.Equal("end", roadmap.Configuration!.EndDatePropertyId);
    }

    [Fact]
    public void AddViews_RemovesTheAutoCreatedDefaultView()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var record = provisioner.Create(_model.Object("Issue"), "parent-page",
            new Dictionary<string, string> { ["Release"] = "ds-release" });

        provisioner.AddViews(_model.Object("Issue"));

        // The default view present at create time is deleted; only the declared views remain.
        Assert.Contains(client.DeletedViews, id => id.StartsWith("default-", StringComparison.Ordinal));
        var remaining = client.ListViews(record.DatabaseId);
        Assert.DoesNotContain(remaining, v => v.Id.StartsWith("default-", StringComparison.Ordinal));
        Assert.Equal(_model.Object("Issue").Views!.Count, remaining.Count);
    }

    [Fact]
    public void AddFormulas_NotionRejectsExpression_ErrorNamesTypePropertyAndExpression()
    {
        // Notion's schema rejection ("Type error with formula") names neither the object type nor the
        // property, so a bare failure is unactionable against a five-type board. The provisioner must tag it
        // with what it was pushing (finding: notion-sync-formula-fix). Widget.derived reads the `done` formula,
        // so it is deferred to AddFormulas — the pass that PATCHes it one at a time.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var type = new SyncObjectType
        {
            Type = "Widget",
            NotionTitle = "Widgets",
            Properties = new()
            {
                ["title"] = new SyncPropertyDef { Type = "title" },
                ["status"] = new SyncPropertyDef { Type = "select", Options = ["open", "done"] },
                ["done"] = new SyncPropertyDef { Type = "formula", Expression = "prop(\"status\") == \"done\"" },
                ["derived"] = new SyncPropertyDef { Type = "formula", Expression = "not empty(prop(\"done\"))" },
            },
        };
        provisioner.Create(type, "parent-page", new Dictionary<string, string>());
        client.FailUpdateDataSourceAfter = 0; // the deferred-formula PATCH is rejected, as live Notion would

        var ex = Assert.Throws<NotionApiException>(() => provisioner.AddFormulas(type));
        Assert.Contains("Widget.derived", ex.Message);
        Assert.Contains("not empty(prop(\"done\"))", ex.Message); // the exact expression the operator must fix
        Assert.Equal(429, ex.StatusCode); // the underlying status is preserved through the re-wrap
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
        provisioner.Create(_model.Object("Slice"), "parent-page",
            new Dictionary<string, string> { ["Sprint"] = "ds-sprint" });
        client.DataSourceUpdates.Clear();

        Assert.False(NotionProvisioner.HasRollups(_model.Object("Slice")));
        provisioner.AddRollups(_model.Object("Slice"));
        Assert.Empty(client.DataSourceUpdates);
    }

    private static SyncObjectType SelfRelationType() => new()
    {
        Type = "Slice",
        NotionTitle = "Sprint Tasks",
        Properties = new()
        {
            ["title"] = new SyncPropertyDef { Type = "title" },
            ["sprint"] = new SyncPropertyDef { Type = "relation", To = "Sprint" },
            ["blocked-by"] = new SyncPropertyDef { Type = "relation", To = "Slice" },
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
        var reused = second.Lookup("Slice");
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
    public void Lookup_TrashedDatabase_IsNotReused_AndLogsReMint()
    {
        // ns-3. A database moved to Notion trash still 200s on retrieval with in_trash:true — it does NOT 404 —
        // so the provisioner would happily reuse it and sync the spine into a dead board. It must detect the
        // trashed state and re-mint instead: Lookup returns null (the same re-provision path as a 404), and one
        // clear line names the trashed database.
        var client = new FakeNotionClient();
        var log = new StringWriter();
        var provisioner = new NotionProvisioner(client, _statePath, log);
        var created = provisioner.Create(_model.Object("Campaign"), "parent-page",
            new Dictionary<string, string> { ["Release"] = "ds-release" });

        client.Databases[created.DatabaseId].InTrash = true;
        Assert.Null(provisioner.Lookup("Campaign"));
        Assert.Contains("trashed", log.ToString());
        Assert.Contains(created.DatabaseId, log.ToString());
    }

    [Fact]
    public void Lookup_ArchivedDatabase_IsNotReused()
    {
        // The archived flag is the other half of Notion's soft-delete state and is treated identically to
        // in_trash: an archived database is re-minted, never reused.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var created = provisioner.Create(_model.Object("Campaign"), "parent-page",
            new Dictionary<string, string> { ["Release"] = "ds-release" });

        client.Databases[created.DatabaseId].Archived = true;
        Assert.Null(provisioner.Lookup("Campaign"));
    }

    [Fact]
    public void Lookup_NotTrashedDatabase_IsReused()
    {
        // in_trash:false (the default, pinned explicitly here) is the healthy case — the recorded database is
        // reused exactly as today, no re-mint.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var created = provisioner.Create(_model.Object("Campaign"), "parent-page",
            new Dictionary<string, string> { ["Release"] = "ds-release" });

        client.Databases[created.DatabaseId].InTrash = false;
        var reused = provisioner.Lookup("Campaign");
        Assert.NotNull(reused);
        Assert.Equal(created.DataSourceId, reused!.DataSourceId);
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

    // --- ns-11: additive-only model-evolution pass (ApplyModelAdditions) ---

    private static SyncObjectType AdditiveBaseType() => new()
    {
        Type = "Thing",
        NotionTitle = "Things",
        Properties = new()
        {
            ["title"] = new SyncPropertyDef { Type = "title" },
            ["status"] = new SyncPropertyDef { Type = "select", Options = ["open", "done"] },
        },
    };

    [Fact]
    public void ApplyModelAdditions_AddsMissingProperty_LeavesExistingUntouched()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var record = provisioner.Create(AdditiveBaseType(), "parent-page", new Dictionary<string, string>());
        client.DataSourceUpdates.Clear();

        var evolved = AdditiveBaseType();
        evolved.Properties["priority"] = new SyncPropertyDef { Type = "select", Options = ["P0", "P1"] };
        provisioner.ApplyModelAdditions(evolved, client.RetrieveDataSource(record.DataSourceId), new Dictionary<string, string>(), new StringWriter());

        var (_, update) = Assert.Single(client.DataSourceUpdates);
        Assert.Equal(["P0", "P1"], update.Properties["priority"].Select!.Options.Select(o => o.Name));
        Assert.False(update.Properties.ContainsKey("title"));  // an unchanged property is never re-sent
        Assert.False(update.Properties.ContainsKey("status"));
        Assert.Null(update.Title);                             // title unchanged — a pure schema addition
        Assert.Single(client.CreatedDatabases);                // added in place, never re-minted
    }

    [Fact]
    public void ApplyModelAdditions_AddsMissingSelectOption_WithoutTouchingExistingColors()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var record = provisioner.Create(AdditiveBaseType(), "parent-page", new Dictionary<string, string>());
        // Notion owns option colors: simulate the live board having assigned them to the existing options.
        var liveStatus = client.DataSourceSchema(record.DataSourceId).Properties["status"].Select!;
        liveStatus.Options.Single(o => o.Name == "open").Color = "blue";
        liveStatus.Options.Single(o => o.Name == "done").Color = "green";
        client.DataSourceUpdates.Clear();

        var evolved = AdditiveBaseType();
        evolved.Properties["status"] = new SyncPropertyDef { Type = "select", Options = ["open", "done", "blocked"] };
        provisioner.ApplyModelAdditions(evolved, client.RetrieveDataSource(record.DataSourceId), new Dictionary<string, string>(), new StringWriter());

        var (_, update) = Assert.Single(client.DataSourceUpdates);
        var options = update.Properties["status"].Select!.Options;
        Assert.Equal(["open", "done", "blocked"], options.Select(o => o.Name));
        Assert.Equal("blue", options.Single(o => o.Name == "open").Color);   // existing colors echoed back, untouched
        Assert.Equal("green", options.Single(o => o.Name == "done").Color);
        Assert.Null(options.Single(o => o.Name == "blocked").Color);          // new option: no color, Notion owns it
    }

    [Fact]
    public void ApplyModelAdditions_RecasedLiveOption_IsNotReAddedAsNearDuplicate()
    {
        // The sprint locked option names to normalized casing: a human recasing "open" -> "Open" on the board
        // must NOT make the model's "open" a fresh addition every tick. Name matching is case-insensitive.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var record = provisioner.Create(AdditiveBaseType(), "parent-page", new Dictionary<string, string>());
        client.DataSourceSchema(record.DataSourceId).Properties["status"].Select!.Options
            .Single(o => o.Name == "open").Name = "Open"; // recased on the board
        client.DataSourceUpdates.Clear();

        provisioner.ApplyModelAdditions(AdditiveBaseType(), client.RetrieveDataSource(record.DataSourceId), new Dictionary<string, string>(), new StringWriter());

        Assert.Empty(client.DataSourceUpdates); // "open" already present (case-insensitively) — no near-duplicate
    }

    [Fact]
    public void ApplyModelAdditions_RenamesTitle_OnNotionTitleChange_ExactlyOnce_NoReMint()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var record = provisioner.Create(AdditiveBaseType(), "parent-page", new Dictionary<string, string>());
        client.DataSourceUpdates.Clear();

        var renamed = AdditiveBaseType();
        renamed.NotionTitle = "Widgets";
        provisioner.ApplyModelAdditions(renamed, client.RetrieveDataSource(record.DataSourceId), new Dictionary<string, string>(), new StringWriter());

        var (dataSourceId, update) = Assert.Single(client.DataSourceUpdates);
        Assert.Equal(record.DataSourceId, dataSourceId);
        Assert.Equal("Widgets", NotionRichText.Flatten(update.Title));
        Assert.Empty(update.Properties);            // a pure rename touches no schema
        Assert.Single(client.CreatedDatabases);     // renamed in place, never re-minted

        // The new title is recorded, so a second pass is a no-op — exactly one UpdateDataSource title call.
        provisioner.ApplyModelAdditions(renamed, client.RetrieveDataSource(record.DataSourceId), new Dictionary<string, string>(), new StringWriter());
        Assert.Single(client.DataSourceUpdates);
    }

    [Fact]
    public void ApplyModelAdditions_RenamePatchThrows_KeepsOldTitle_SoNextTickRetries()
    {
        // The record must NOT advance to the new title until the PATCH succeeds: a failed rename that had already
        // recorded the new title would no-op forever. On a throw the OLD title stays recorded, so the next tick
        // re-issues the (idempotent, payload-identical) rename.
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var record = provisioner.Create(AdditiveBaseType(), "parent-page", new Dictionary<string, string>());

        var renamed = AdditiveBaseType();
        renamed.NotionTitle = "Widgets";
        client.FailUpdateDataSourceAfter = 0; // the rename PATCH throws
        Assert.Throws<NotionApiException>(() =>
            provisioner.ApplyModelAdditions(renamed, client.RetrieveDataSource(record.DataSourceId), new Dictionary<string, string>(), new StringWriter()));

        // A fresh provisioner over the persisted state still holds the OLD title — the rename was not recorded.
        Assert.Equal("Things", NotionProvisioner.LoadTracked(_statePath).Single().NotionTitle);

        // Next tick, the PATCH succeeds and the rename lands.
        client.FailUpdateDataSourceAfter = null;
        var retry = new NotionProvisioner(client, _statePath);
        retry.ApplyModelAdditions(renamed, client.RetrieveDataSource(record.DataSourceId), new Dictionary<string, string>(), new StringWriter());
        Assert.Equal("Widgets", NotionRichText.Flatten(Assert.Single(client.DataSourceUpdates).Request.Title));
        Assert.Equal("Widgets", NotionProvisioner.LoadTracked(_statePath).Single().NotionTitle);
    }

    [Fact]
    public void ApplyModelAdditions_NeverRetypesOrDeletes_DestructiveDriftLeftToTheWarnPath()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var record = provisioner.Create(AdditiveBaseType(), "parent-page", new Dictionary<string, string>());
        var live = client.DataSourceSchema(record.DataSourceId).Properties;
        live["Owner"] = new NotionPropertySchema { RichText = new NotionEmptyConfig() };  // rogue, not in the model
        live["status"] = new NotionPropertySchema { Checkbox = new NotionEmptyConfig() };  // live type != model's select
        client.DataSourceUpdates.Clear();

        provisioner.ApplyModelAdditions(AdditiveBaseType(), client.RetrieveDataSource(record.DataSourceId), new Dictionary<string, string>(), new StringWriter());

        // Additive is strictly non-destructive: it never deletes the rogue column nor retypes status back to a
        // select — those stay a warn/--prune decision in the drift check.
        Assert.Empty(client.DataSourceUpdates);
    }

    [Fact]
    public void ApplyModelAdditions_InSync_IsNoOp()
    {
        var client = new FakeNotionClient();
        var provisioner = new NotionProvisioner(client, _statePath);
        var record = provisioner.Create(AdditiveBaseType(), "parent-page", new Dictionary<string, string>());
        client.DataSourceUpdates.Clear();

        provisioner.ApplyModelAdditions(AdditiveBaseType(), client.RetrieveDataSource(record.DataSourceId), new Dictionary<string, string>(), new StringWriter());
        Assert.Empty(client.DataSourceUpdates);
    }

    [Fact]
    public void ApplyModelAdditions_LegacyRecordWithoutTitle_SeedsWithoutRenaming()
    {
        // A record written before ns-11 carries no notionTitle. The live board already shows the title it was
        // provisioned with, so the pass seeds the record from the model WITHOUT a rename PATCH.
        var client = new FakeNotionClient();
        File.WriteAllText(_statePath,
            "{\"types\":[{\"objectType\":\"Thing\",\"databaseId\":\"db-x\",\"dataSourceId\":\"ds-x\",\"postPassDone\":true}]}");
        client.DataSourceSchema("ds-x").Properties["title"] = new NotionPropertySchema { Title = new NotionEmptyConfig() };
        var provisioner = new NotionProvisioner(client, _statePath);

        var type = new SyncObjectType
        {
            Type = "Thing",
            NotionTitle = "Things",
            Properties = new() { ["title"] = new SyncPropertyDef { Type = "title" } },
        };
        provisioner.ApplyModelAdditions(type, client.RetrieveDataSource("ds-x"), new Dictionary<string, string>(), new StringWriter());

        Assert.Empty(client.DataSourceUpdates);  // no rename PATCH on the seed tick
        Assert.Equal("Things", NotionProvisioner.LoadTracked(_statePath).Single().NotionTitle);  // seed persisted
    }
}
