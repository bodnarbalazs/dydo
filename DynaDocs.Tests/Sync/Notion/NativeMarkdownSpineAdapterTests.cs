namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

public sealed class NativeMarkdownSpineAdapterTests
{
    private static readonly Dictionary<string, string> Schema = new() { ["title"] = "title", ["status"] = "select", [NotionSyncAdapter.WriteIdProperty] = "rich_text" };

    private static SyncUpsert Upsert(string? externalId = null, bool writeBody = true, string? operationId = "op") => new()
    {
        LocalId = "local", ExternalId = externalId, WriteBody = writeBody, OperationId = operationId,
        Fields = [new SyncField { Key = "title", Value = "Title" }, new SyncField { Key = "status", Value = "open" }], Body = "# body",
    };

    private static NotionPage Seed(FakeNotionClient client, string id = "page") =>
        client.SeedPage(id, new Dictionary<string, NotionPropertyValue>
        {
            ["title"] = new() { Type = "title", Title = NotionRichText.Of("Title") },
            ["status"] = new() { Type = "select", Select = new NotionSelectOption { Name = "open" } },
        }, dataSourceId: "ds");

    [Fact]
    public void NativeMarkdownSpine_Read_UsesMarkdownNotBlocks()
    {
        var client = new FakeNotionClient(); Seed(client); client.SetPageMarkdown("page", "# native");
        var record = Assert.Single(new NotionSyncAdapter(client, "ds", Schema).ReadExternalState());
        Assert.Equal("# native", record.Body); Assert.Equal(1, client.MarkdownReadCalls); Assert.Equal(0, client.GetBlockChildrenCalls);
    }

    [Fact]
    public void NativeMarkdownSpine_Read_TruncationNeverReturnsPartialBody()
    {
        var client = new FakeNotionClient(); Seed(client); client.SetPageMarkdown("page", "partial"); client.TruncatedReadFor.Add("page");
        var record = Assert.Single(new NotionSyncAdapter(client, "ds", Schema).ReadExternalState());
        Assert.Equal(SyncBodyReadStatus.Truncated, record.BodyReadStatus); Assert.Equal("", record.Body);
    }

    [Fact]
    public void NativeMarkdownSpine_Create_PutsBodyInMarkdownField()
    {
        var client = new FakeNotionClient(); var assigned = new Dictionary<string, string>();
        new NotionSyncAdapter(client, "ds", Schema).Apply(new SyncChangeSet { Upserts = { Upsert() } }, assigned, [], []);
        Assert.Equal("# body", client.StoredMarkdown(assigned["local"])); Assert.Equal(0, Assert.Single(client.CreateChildCounts));
    }

    [Fact]
    public void NativeMarkdownSpine_Update_UsesMarkdownEndpoint()
    {
        var client = new FakeNotionClient(); Seed(client);
        new NotionSyncAdapter(client, "ds", Schema).Apply(new SyncChangeSet { Upserts = { Upsert("page") } }, new Dictionary<string, string>(), [], []);
        Assert.Equal(["page"], client.MarkdownUpdates); Assert.Equal("# body", client.StoredMarkdown("page"));
    }

    [Fact]
    public void NativeMarkdownSpine_PropertyOnly_HasNoBodyOrStructuralRequests()
    {
        var client = new FakeNotionClient(); Seed(client);
        new NotionSyncAdapter(client, "ds", Schema).Apply(new SyncChangeSet { Upserts = { Upsert("page", false, null) } }, new Dictionary<string, string>(), [], []);
        Assert.Equal(0, client.MarkdownReadCalls); Assert.Equal(0, client.MarkdownWriteCalls); Assert.Equal(0, client.StructuralChildCalls);
    }

    [Fact]
    public void NativeMarkdownSpine_UpdateLeaf_AllowsDeletingContent()
    {
        var client = new FakeNotionClient(); Seed(client);
        new NotionSyncAdapter(client, "ds", Schema).Apply(new SyncChangeSet { Upserts = { Upsert("page") } }, new Dictionary<string, string>(), [], []);
        Assert.Equal(("page", true), Assert.Single(client.MarkdownUpdateCalls)); Assert.Equal(1, client.StructuralChildCalls);
    }

    [Fact]
    public void NativeMarkdownSpine_UpdateParent_PreservesChildrenAndEscapesTags()
    {
        var client = new FakeNotionClient(); Seed(client);
        client.CreatePage(new NotionPageCreateRequest { Parent = new NotionParent { PageId = "page" }, Properties = new() { ["title"] = new() { Type = "title", Title = NotionRichText.Of("A & B") } } });
        new NotionSyncAdapter(client, "ds", Schema).Apply(new SyncChangeSet { Upserts = { Upsert("page") } }, new Dictionary<string, string>(), [], []);
        Assert.Equal(("page", false), Assert.Single(client.MarkdownUpdateCalls)); Assert.Contains("A &amp; B", client.StoredMarkdown("page"));
    }

    [Fact]
    public void NativeMarkdownSpine_Receipt_ObservesCleanedNativeBody()
    {
        var client = new FakeNotionClient(); Seed(client); client.MarkdownReadTransform = body => body + "\r\n";
        var result = new NotionSyncAdapter(client, "ds", Schema).ApplyWithReceipts(new SyncChangeSet { Upserts = { Upsert("page") } }, new Dictionary<string, string>(), [], []);
        Assert.Equal("# body", Assert.Single(result.BodyWriteReceipts).ObservedExternalBody);
    }

    [Fact]
    public void NativeMarkdownSpine_Receipt_TruncatedReadIsWithheld()
    {
        var client = new FakeNotionClient(); Seed(client); client.TruncatedReadFor.Add("page");
        var result = new NotionSyncAdapter(client, "ds", Schema).ApplyWithReceipts(new SyncChangeSet { Upserts = { Upsert("page") } }, new Dictionary<string, string>(), [], []);
        Assert.Empty(result.BodyWriteReceipts);
    }

    [Fact]
    public void NativeMarkdownSpine_Read_FiltersReservedWriteId()
    {
        var client = new FakeNotionClient(); var page = Seed(client); page.Properties[NotionSyncAdapter.WriteIdProperty] = new() { Type = "rich_text", RichText = NotionRichText.Of("secret") };
        var record = Assert.Single(new NotionSyncAdapter(client, "ds", Schema).ReadExternalState());
        Assert.DoesNotContain(record.Fields, field => field.Key == NotionSyncAdapter.WriteIdProperty);
    }

    [Fact]
    public void NativeMarkdownSpine_BodyMutation_WritesOperationId()
    {
        var client = new FakeNotionClient(); Seed(client);
        new NotionSyncAdapter(client, "ds", Schema).Apply(new SyncChangeSet { Upserts = { Upsert("page", true, "uuid") } }, new Dictionary<string, string>(), [], []);
        Assert.Equal("uuid", NotionRichText.Flatten(client.QueryDataSource("ds").Single().Properties[NotionSyncAdapter.WriteIdProperty].RichText));
    }

    [Fact]
    public void NativeMarkdownSpine_AmbiguousCreate_AdoptsExactOperationId()
    {
        var client = new FakeNotionClient { CreatePageSucceedsThenAmbiguous5xx = true }; var assigned = new Dictionary<string, string>();
        new NotionSyncAdapter(client, "ds", Schema).Apply(new SyncChangeSet { Upserts = { Upsert(operationId: "uuid") } }, assigned, [], []);
        Assert.Equal("page-1", assigned["local"]); Assert.Single(client.QueryDataSource("ds"));
    }

    [Fact]
    public void NativeMarkdownSpine_AmbiguousCreate_DuplicateOperationIdThrows()
    {
        var client = new FakeNotionClient { CreatePageFailsAmbiguously = true };
        foreach (var id in new[] { "one", "two" }) { var page = Seed(client, id); page.Properties[NotionSyncAdapter.WriteIdProperty] = new() { Type = "rich_text", RichText = NotionRichText.Of("uuid") }; }
        Assert.Throws<AmbiguousCreateIdentityException>(() => new NotionSyncAdapter(client, "ds", Schema).Apply(new SyncChangeSet { Upserts = { Upsert(operationId: "uuid") } }, new Dictionary<string, string>(), [], []));
    }

    [Fact]
    public void NativeMarkdownSpine_AmbiguousCreate_DoesNotRecoverByTitle()
    {
        var client = new FakeNotionClient { CreatePageFailsAmbiguously = true }; Seed(client, "old"); var assigned = new Dictionary<string, string>();
        new NotionSyncAdapter(client, "ds", Schema).Apply(new SyncChangeSet { Upserts = { Upsert(operationId: "uuid") } }, assigned, [], []);
        Assert.Equal("page-1", assigned["local"]); Assert.Equal(2, client.QueryDataSource("ds").Count);
    }
}
