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

        var assigned = adapter.Apply(changes);

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
        adapter.Apply(changes);

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
        adapter.Apply(changes);

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
        var assigned = adapter.Apply(changes);

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
        var assigned = adapter.Apply(changes);

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
