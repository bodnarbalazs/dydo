namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

public class NotionSyncAdapterTests
{
    private static NotionBlock Paragraph(string text, string id) => new()
    {
        Type = "paragraph", Id = id,
        Paragraph = new NotionBlockBody { RichText = NotionRichText.Of(text) },
    };

    private static NotionPropertyValue Title(string s) =>
        new() { Type = "title", Title = NotionRichText.Of(s) };

    [Fact]
    public void PartialApplyFailure_PersistsCreatedIds_NoDuplicateOnRetry()
    {
        // The 2nd of 3 creates throws mid-batch. The 1st page's id must be persisted in the base so a
        // retry does not re-create it (a duplicate row) — created pages carry no stable re-match key.
        var client = new FakeNotionClient();
        var schema = new Dictionary<string, string> { ["Name"] = "title" };
        var adapter = new NotionSyncAdapter(client, "ds1", schema);

        var dir = Path.Combine(Path.GetTempPath(), "dydo-notion-partial-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var snapPath = Path.Combine(dir, "snap.json");
            SyncDoc Doc(string id) => new()
            {
                LocalId = id, Fields = [new SyncField { Key = "Name", Value = id }],
                Body = "", SourcePath = Path.Combine(dir, id + ".md"),
            };
            var docs = new List<SyncDoc> { Doc("t1"), Doc("t2"), Doc("t3") };

            client.FailCreateAfter = 1; // 1st create succeeds, 2nd throws
            var runner1 = new SyncRunner(adapter, new BaseSnapshotStore(snapPath), (id, _, _) => Path.Combine(dir, id + ".md"));
            Assert.Throws<NotionApiException>(() => runner1.Run(docs));

            // Exactly one create's id survived into the persisted base.
            var baseAfter = new BaseSnapshotStore(snapPath);
            var recorded = docs.Select(d => baseAfter.Get(d.LocalId)?.ExternalId).Where(id => id != null).ToList();
            Assert.Single(recorded);
            Assert.Single(client.QueryDataSource("ds1")); // only the 1st page was created

            // Retry with a healthy client: the survivor is not duplicated.
            client.FailCreateAfter = null;
            var runner2 = new SyncRunner(adapter, new BaseSnapshotStore(snapPath), (id, _, _) => Path.Combine(dir, id + ".md"));
            runner2.Run(docs);

            Assert.Equal(3, client.QueryDataSource("ds1").Count); // exactly three, no duplicate
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void UnresolvedRelation_IsIdempotentAcrossTicks_NoRepoRewrite()
    {
        // A SprintTask whose `sprint:` points at a sprint that does not resolve to a Notion page id
        // round-trips lossily: the relation is omitted on write and reads back empty. Without the field
        // normalizer the engine reads that as an external edit and BLANKS the repo value. Assert the repo
        // file is byte-unchanged across a re-sync — the dangling reference is preserved, never erased.
        var client = new FakeNotionClient();
        var schema = new Dictionary<string, string> { ["title"] = "title", ["sprint"] = "relation" };
        // Empty relation map -> `sprint: ghost` never resolves to a page id.
        var adapter = new NotionSyncAdapter(client, "ds1", schema, relationLocalToPageId: new Dictionary<string, string>());

        var dir = Path.Combine(Path.GetTempPath(), "dydo-notion-rel-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var store = new BaseSnapshotStore(Path.Combine(dir, "snap.json"));
            var repoPath = Path.Combine(dir, "t.md");
            SyncRunner Runner() => new(adapter, store, (id, _, _) => Path.Combine(dir, id + ".md"));

            var doc = new SyncDoc
            {
                LocalId = "t",
                Fields = [new SyncField { Key = "title", Value = "Task" }, new SyncField { Key = "sprint", Value = "ghost" }],
                Body = "body", SourcePath = repoPath,
            };
            Runner().Run([doc]);              // creates the page (unresolvable relation omitted)
            SyncDocFile.Write(repoPath, doc); // materialize the raw repo file
            var before = File.ReadAllText(repoPath);

            var result = Runner().Run([SyncDocFile.Read(repoPath, "t", repoPath)]);

            Assert.All(result.Results, r => Assert.Equal(ReconcileAction.None, r.Action));
            Assert.Equal(before, File.ReadAllText(repoPath)); // sprint: ghost preserved, no WriteToRepo
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GenuineExternalFieldEdit_StillWritesToRepo()
    {
        // The field normalizer must not over-mask: a real external value change is still detected and
        // written back to the repo.
        var client = new FakeNotionClient();
        var schema = new Dictionary<string, string> { ["Status"] = "select" };
        var adapter = new NotionSyncAdapter(client, "ds1", schema);

        var dir = Path.Combine(Path.GetTempPath(), "dydo-notion-edit-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var store = new BaseSnapshotStore(Path.Combine(dir, "snap.json"));
            var repoPath = Path.Combine(dir, "t.md");
            SyncRunner Runner() => new(adapter, store, (id, _, _) => Path.Combine(dir, id + ".md"));

            var doc = new SyncDoc
            {
                LocalId = "t", Fields = [new SyncField { Key = "Status", Value = "open" }],
                Body = "body", SourcePath = repoPath,
            };
            Runner().Run([doc]);
            SyncDocFile.Write(repoPath, doc);

            // Colleague changes Status in Notion.
            var page = client.QueryDataSource("ds1").Single();
            page.Properties["Status"] = new NotionPropertyValue { Type = "select", Select = new NotionSelectOption { Name = "done" } };

            Runner().Run([SyncDocFile.Read(repoPath, "t", repoPath)]);

            Assert.Equal("done", SyncDocFile.Read(repoPath, "t", repoPath).GetField("Status"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void GenuineExternalBodyEdit_IsDetected_WritesToRepo()
    {
        // A real body text change in Notion (not a lossy-normalization-only difference) must be detected
        // and written back — the body normalizer does not under-trigger.
        var client = new FakeNotionClient();
        var schema = new Dictionary<string, string> { ["Status"] = "select" };
        var adapter = new NotionSyncAdapter(client, "ds1", schema);

        var dir = Path.Combine(Path.GetTempPath(), "dydo-notion-body-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var store = new BaseSnapshotStore(Path.Combine(dir, "snap.json"));
            var repoPath = Path.Combine(dir, "t.md");
            SyncRunner Runner() => new(adapter, store, (id, _, _) => Path.Combine(dir, id + ".md"));

            var doc = new SyncDoc
            {
                LocalId = "t", Fields = [new SyncField { Key = "Status", Value = "open" }],
                Body = "line one", SourcePath = repoPath,
            };
            Runner().Run([doc]);
            SyncDocFile.Write(repoPath, doc);

            var page = client.QueryDataSource("ds1").Single();
            client.SetBlockChildren(page.Id, NotionBlockConverter.ToBlocks("line one CHANGED"));

            Runner().Run([SyncDocFile.Read(repoPath, "t", repoPath)]);

            Assert.Contains("line one CHANGED", SyncDocFile.Read(repoPath, "t", repoPath).Body);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ApplyFailure_NonCreatePush_BaseHoldsPreEditValue_ForRetry()
    {
        // A non-create push (property update of an existing external object) throws. The base must NOT
        // advance to the edited value, or the edit would be silently considered synced and never re-pushed.
        var client = new FakeNotionClient();
        var schema = new Dictionary<string, string> { ["status"] = "select" };
        var adapter = new NotionSyncAdapter(client, "ds1", schema);

        var dir = Path.Combine(Path.GetTempPath(), "dydo-notion-pushfail-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var snapPath = Path.Combine(dir, "snap.json");
            SyncDoc Doc(string status) => new()
            {
                LocalId = "t", Fields = [new SyncField { Key = "status", Value = status }],
                Body = "b", SourcePath = Path.Combine(dir, "t.md"),
            };
            SyncRunner Runner() => new(adapter, new BaseSnapshotStore(snapPath), (id, _, _) => Path.Combine(dir, id + ".md"));

            Runner().Run([Doc("open")]); // create; base t -> {status: open}

            client.FailUpdate = true;
            Assert.Throws<NotionApiException>(() => Runner().Run([Doc("done")])); // push of the edit throws

            Assert.Equal("open", new BaseSnapshotStore(snapPath).Get("t")!.GetField("status")); // base unchanged
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ApplyFailure_WithDeleteInBatch_BaseEntryRetained_NoResurrection()
    {
        // A delete's archive call throws. The base entry for the deleted object must be RETAINED, so the
        // delete is retried next tick rather than the object reappearing (base dropped => seen as external-new).
        var client = new FakeNotionClient();
        var schema = new Dictionary<string, string> { ["status"] = "select" };
        var adapter = new NotionSyncAdapter(client, "ds1", schema);

        var dir = Path.Combine(Path.GetTempPath(), "dydo-notion-delfail-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var snapPath = Path.Combine(dir, "snap.json");
            SyncRunner Runner() => new(adapter, new BaseSnapshotStore(snapPath), (id, _, _) => Path.Combine(dir, id + ".md"));

            var doc = new SyncDoc
            {
                LocalId = "t", Fields = [new SyncField { Key = "status", Value = "open" }],
                Body = "b", SourcePath = Path.Combine(dir, "t.md"),
            };
            Runner().Run([doc]); // create; base has t

            client.FailUpdate = true;
            Assert.Throws<NotionApiException>(() => Runner().Run([])); // repo dropped t -> delete -> archive throws

            Assert.NotNull(new BaseSnapshotStore(snapPath).Get("t")); // base entry retained
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void MultiLineBodyWithBlankLines_IsIdempotentAcrossTicks_NoRepoRewrite()
    {
        // A body with blank lines round-trips lossily through Notion blocks. An untouched doc must not be
        // seen as changed on the next tick, or the repo file would be rewritten with the stripped body.
        var client = new FakeNotionClient();
        var schema = new Dictionary<string, string> { ["Status"] = "select" };
        var adapter = new NotionSyncAdapter(client, "ds1", schema);

        var dir = Path.Combine(Path.GetTempPath(), "dydo-notion-blank-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var store = new BaseSnapshotStore(Path.Combine(dir, "snap.json"));
            var repoPath = Path.Combine(dir, "t.md");
            SyncRunner Runner() => new(adapter, store, (id, _, _) => Path.Combine(dir, id + ".md"));

            var doc = new SyncDoc
            {
                LocalId = "t", Fields = [new SyncField { Key = "Status", Value = "open" }],
                Body = "para one\n\npara two\n\n\npara three", SourcePath = repoPath,
            };
            Runner().Run([doc]);
            SyncDocFile.Write(repoPath, doc); // materialize the raw repo file
            var before = File.ReadAllText(repoPath);

            var result = Runner().Run([SyncDocFile.Read(repoPath, "t", repoPath)]);

            Assert.All(result.Results, r => Assert.Equal(ReconcileAction.None, r.Action));
            Assert.Equal(before, File.ReadAllText(repoPath)); // byte-unchanged, no WriteToRepo
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ReplaceBody_AppendFails_LeavesExistingBodyIntact()
    {
        // Append-before-delete: if the append throws, the previously-existing blocks must NOT be deleted,
        // so the body is preserved (never emptied).
        var client = new FakeNotionClient();
        client.SeedPage("p1", new() { ["Name"] = Title("x") }, [Paragraph("old body", "b1")]);
        var adapter = new NotionSyncAdapter(client, "ds1");
        adapter.ReadExternalState();
        client.FailAppend = true;

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "p1", ExternalId = "p1",
            Fields = [new SyncField { Key = "Name", Value = "renamed" }],
            Body = "new body",
        });

        Assert.Throws<NotionApiException>(() => adapter.Apply(changes, new Dictionary<string, string>()));

        Assert.DoesNotContain("b1", client.DeletedBlocks);
        Assert.Equal("old body", NotionBlockConverter.FromBlocks(client.GetBlockChildren("p1")));
    }

    [Fact]
    public void Relation_MultiValueEdit_RemovingOneTarget_PropagatesToNotion()
    {
        // Since fea7915 the mapper round-trips ALL relation targets. A blocked-by holding two targets in
        // Notion must be fully editable from the repo: dropping one blocker locally writes the remaining
        // single target back (the property is NOT omitted), so a task can be un-blocked from the repo side.
        var client = new FakeNotionClient();
        client.SeedPage(
            "t1",
            new()
            {
                ["title"] = Title("Task"),
                ["blocked-by"] = new NotionPropertyValue { Type = "relation", Relation = [new() { Id = "pa" }, new() { Id = "pb" }] },
            });
        var schema = new Dictionary<string, string> { ["title"] = "title", ["blocked-by"] = "relation" };
        var localToPage = new Dictionary<string, string> { ["a"] = "pa", ["b"] = "pb" };
        var pageToLocal = new Dictionary<string, string> { ["pa"] = "a", ["pb"] = "b" };
        var adapter = new NotionSyncAdapter(client, "ds1", schema, localToPage, pageToLocal);

        // Read sees both targets, rendered as comma-joined local ids.
        var record = adapter.ReadExternalState().Single();
        Assert.Equal("a, b", record.Fields.First(f => f.Key == "blocked-by").Value);

        // The repo keeps only one blocker; the update must propagate that removal, not leave both.
        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "t1", ExternalId = "t1",
            Fields = [new SyncField { Key = "title", Value = "Task" }, new SyncField { Key = "blocked-by", Value = "a" }],
            Body = "",
        });
        adapter.Apply(changes, new Dictionary<string, string>());

        var page = client.QueryDataSource("ds1").Single();
        Assert.Equal(["pa"], page.Properties["blocked-by"].Relation!.Select(r => r.Id));
    }

    [Fact]
    public void ReadExternalState_WithExplicitSchema_DropsPropertiesAbsentFromSchema()
    {
        // A provisioned data source carries dual-relation reverse columns (the view-only "blocks" of a
        // blocked-by pair) and may carry rogue columns a colleague added. Neither is canonical; only
        // properties the model schema knows may reach frontmatter (DR 029 §6), or a raw page-id UUID or a
        // stray column would pollute the repo source of truth.
        var client = new FakeNotionClient();
        client.SeedPage(
            "t1",
            new()
            {
                ["title"] = Title("Task"),
                ["blocks"] = new NotionPropertyValue { Type = "relation", Relation = [new() { Id = "other" }] },
                ["Rogue"] = new NotionPropertyValue { Type = "rich_text", RichText = NotionRichText.Of("colleague note") },
            });
        var schema = new Dictionary<string, string> { ["title"] = "title", ["blocked-by"] = "relation" };
        var adapter = new NotionSyncAdapter(client, "ds1", schema);

        var record = adapter.ReadExternalState().Single();

        Assert.Contains(record.Fields, f => f.Key == "title");
        Assert.DoesNotContain(record.Fields, f => f.Key == "blocks"); // dual-property reverse, not in schema
        Assert.DoesNotContain(record.Fields, f => f.Key == "Rogue");  // rogue column, not in schema
    }

    [Fact]
    public void ReadExternalState_MapsPagesToRecords_WithFieldsAndBody()
    {
        var client = new FakeNotionClient();
        client.SeedPage(
            "p1",
            new() { ["Name"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("My Task") } },
            [Paragraph("hello body", "b1")]);

        var records = new NotionSyncAdapter(client, "ds1").ReadExternalState();

        var rec = Assert.Single(records);
        Assert.Equal("p1", rec.ExternalId);
        Assert.Equal("My Task", rec.Fields.First(f => f.Key == "Name").Value);
        Assert.Equal("hello body", rec.Body);
    }

    [Fact]
    public void ReadExternalState_SkipsArchivedPages()
    {
        var client = new FakeNotionClient();
        var page = client.SeedPage("p1", new() { ["Name"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("x") } });
        page.Archived = true;

        Assert.Empty(new NotionSyncAdapter(client, "ds1").ReadExternalState());
    }

    [Fact]
    public void Apply_Create_PostsNewPage_AndReturnsIdKeyedByLocalId()
    {
        var client = new FakeNotionClient();
        // A schema must exist for properties to map; seed an existing page to infer "Name" -> title.
        client.SeedPage("seed", new() { ["Name"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("seed") } });
        var adapter = new NotionSyncAdapter(client, "ds1");
        adapter.ReadExternalState(); // captures schema

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "task-a", ExternalId = null,
            Fields = [new SyncField { Key = "Name", Value = "Brand new" }],
            Body = "# Heading\n\npara",
        });

        var assigned = new Dictionary<string, string>();
        adapter.Apply(changes, assigned);

        Assert.True(assigned.ContainsKey("task-a"));
        var newPage = client.QueryDataSource("ds1").First(p => p.Id == assigned["task-a"]);
        Assert.Equal("Brand new", NotionRichText.Flatten(newPage.Properties["Name"].Title));
        Assert.NotEmpty(client.GetBlockChildren(assigned["task-a"]));
    }

    [Fact]
    public void Apply_Update_PatchesProperties_AndReplacesBody()
    {
        var client = new FakeNotionClient();
        client.SeedPage(
            "p1",
            new() { ["Name"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("old") } },
            [Paragraph("old body", "b1")]);
        var adapter = new NotionSyncAdapter(client, "ds1");
        adapter.ReadExternalState();

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "p1", ExternalId = "p1",
            Fields = [new SyncField { Key = "Name", Value = "new" }],
            Body = "new body",
        });
        adapter.Apply(changes, new Dictionary<string, string>());

        var page = client.QueryDataSource("ds1").Single();
        Assert.Equal("new", NotionRichText.Flatten(page.Properties["Name"].Title));
        Assert.Contains("b1", client.DeletedBlocks);          // old body block archived
        Assert.Contains("p1", client.AppendedTo);             // new body appended
        Assert.Equal("new body", NotionBlockConverter.FromBlocks(client.GetBlockChildren("p1")));
    }

    [Fact]
    public void Apply_Delete_ArchivesPage()
    {
        var client = new FakeNotionClient();
        client.SeedPage("p1", new() { ["Name"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("x") } });
        var adapter = new NotionSyncAdapter(client, "ds1");
        adapter.ReadExternalState();

        var changes = new SyncChangeSet();
        changes.Deletes.Add("p1");
        adapter.Apply(changes, new Dictionary<string, string>());

        Assert.True(client.QueryDataSource("ds1").Single().Archived);
    }

    [Fact]
    public void Apply_WithExplicitSchema_CreatesPage_OnEmptyDataSource()
    {
        // A freshly provisioned data source has no rows to infer from; the explicit schema lets the
        // adapter still map fields to typed properties.
        var client = new FakeNotionClient();
        var schema = new Dictionary<string, string> { ["title"] = "title", ["status"] = "select" };
        var adapter = new NotionSyncAdapter(client, "ds1", schema);

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "t", ExternalId = null,
            Fields = [new SyncField { Key = "title", Value = "Fresh" }, new SyncField { Key = "status", Value = "active" }],
            Body = "body",
        });
        var assigned = new Dictionary<string, string>();
        adapter.Apply(changes, assigned);

        var page = client.QueryDataSource("ds1").Single(p => p.Id == assigned["t"]);
        Assert.Equal("Fresh", NotionRichText.Flatten(page.Properties["title"].Title));
        Assert.Equal("active", page.Properties["status"].Select!.Name);
    }

    [Fact]
    public void Apply_Relation_WritesParentPageId_FromLocalIdMap()
    {
        var client = new FakeNotionClient();
        var schema = new Dictionary<string, string> { ["title"] = "title", ["campaign"] = "relation" };
        var localToPage = new Dictionary<string, string> { ["dydo-2-0"] = "campaign-page" };
        var adapter = new NotionSyncAdapter(client, "ds2", schema, relationLocalToPageId: localToPage);

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "s", ExternalId = null,
            Fields = [new SyncField { Key = "title", Value = "Sprint" }, new SyncField { Key = "campaign", Value = "dydo-2-0" }],
            Body = "",
        });
        var assigned = new Dictionary<string, string>();
        adapter.Apply(changes, assigned);

        var page = client.QueryDataSource("ds2").Single(p => p.Id == assigned["s"]);
        Assert.Equal("campaign-page", page.Properties["campaign"].Relation!.Single().Id);
    }

    [Fact]
    public void ReadExternalState_Relation_MapsPageIdToParentLocalId()
    {
        var client = new FakeNotionClient();
        client.SeedPage(
            "s1",
            new()
            {
                ["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("Sprint") },
                ["campaign"] = new NotionPropertyValue { Type = "relation", Relation = [new NotionRelationRef { Id = "campaign-page" }] },
            },
            dataSourceId: "ds2");
        var schema = new Dictionary<string, string> { ["title"] = "title", ["campaign"] = "relation" };
        var pageToLocal = new Dictionary<string, string> { ["campaign-page"] = "dydo-2-0" };
        var adapter = new NotionSyncAdapter(client, "ds2", schema, relationPageIdToLocalId: pageToLocal);

        var record = adapter.ReadExternalState().Single();
        Assert.Equal("dydo-2-0", record.Fields.First(f => f.Key == "campaign").Value);
    }

    [Fact]
    public void RoundTrip_EngineThroughAdapter_PushesRepoDocToNotion()
    {
        // Wire the real SyncRunner through the Notion adapter + fake client end-to-end.
        var client = new FakeNotionClient();
        client.SeedPage("seed", new() { ["Status"] = new NotionPropertyValue { Type = "select", Select = new NotionSelectOption { Name = "x" } } });
        var adapter = new NotionSyncAdapter(client, "ds1");

        var dir = Path.Combine(Path.GetTempPath(), "dydo-notion-rt-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            var store = new BaseSnapshotStore(Path.Combine(dir, "snap.json"));
            var runner = new SyncRunner(adapter, store, (id, _, _) => Path.Combine(dir, id + ".md"));

            runner.Run([new SyncDoc
            {
                LocalId = "t", Fields = [new SyncField { Key = "Status", Value = "open" }],
                Body = "body", SourcePath = Path.Combine(dir, "t.md"),
            }]);

            var created = client.QueryDataSource("ds1").First(p => p.Id != "seed");
            Assert.Equal("open", created.Properties["Status"].Select!.Name);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
