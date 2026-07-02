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
            var runner1 = new SyncRunner(adapter, new BaseSnapshotStore(snapPath), id => Path.Combine(dir, id + ".md"));
            Assert.Throws<NotionApiException>(() => runner1.Run(docs));

            // Exactly one create's id survived into the persisted base.
            var baseAfter = new BaseSnapshotStore(snapPath);
            var recorded = docs.Select(d => baseAfter.Get(d.LocalId)?.ExternalId).Where(id => id != null).ToList();
            Assert.Single(recorded);
            Assert.Single(client.QueryDataSource("ds1")); // only the 1st page was created

            // Retry with a healthy client: the survivor is not duplicated.
            client.FailCreateAfter = null;
            var runner2 = new SyncRunner(adapter, new BaseSnapshotStore(snapPath), id => Path.Combine(dir, id + ".md"));
            runner2.Run(docs);

            Assert.Equal(3, client.QueryDataSource("ds1").Count); // exactly three, no duplicate
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
            SyncRunner Runner() => new(adapter, store, id => Path.Combine(dir, id + ".md"));

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
    public void Relation_MultiTarget_OmittedFromWriteBack_AndWarns()
    {
        // A relation holding two targets is read as its first ref only. Writing that single ref back would
        // delete the second target, so the relation is omitted from the update and a warning is produced.
        var client = new FakeNotionClient();
        client.SeedPage(
            "s1",
            new()
            {
                ["title"] = Title("Sprint 7"),
                ["campaign"] = new NotionPropertyValue { Type = "relation", Relation = [new() { Id = "c1" }, new() { Id = "c2" }] },
            },
            dataSourceId: "ds2");
        var schema = new Dictionary<string, string> { ["title"] = "title", ["campaign"] = "relation" };
        var localToPage = new Dictionary<string, string> { ["c1"] = "c1" };
        var pageToLocal = new Dictionary<string, string> { ["c1"] = "c1", ["c2"] = "c2" };
        var warnings = new StringWriter();
        var adapter = new NotionSyncAdapter(client, "ds2", schema, localToPage, pageToLocal, warnings: warnings);

        adapter.ReadExternalState();

        Assert.Contains("campaign", warnings.ToString());
        Assert.Contains("Sprint 7", warnings.ToString());

        var changes = new SyncChangeSet();
        changes.Upserts.Add(new SyncUpsert
        {
            LocalId = "s1", ExternalId = "s1",
            Fields = [new SyncField { Key = "title", Value = "Renamed" }, new SyncField { Key = "campaign", Value = "c1" }],
            Body = "",
        });
        adapter.Apply(changes, new Dictionary<string, string>());

        var page = client.QueryDataSource("ds2").Single();
        Assert.Equal(2, page.Properties["campaign"].Relation!.Count); // both targets untouched
        Assert.Equal("Renamed", NotionRichText.Flatten(page.Properties["title"].Title));
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
            var runner = new SyncRunner(adapter, store, id => Path.Combine(dir, id + ".md"));

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
