namespace DynaDocs.Tests.Sync.Notion;

using System.Net;
using System.Net.Http;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// Drives <see cref="NotionClient"/> against a fake handler: verifies request paths, the data-source
/// parent shape, the pinned Notion-Version + bearer headers, pagination, and error surfacing — all
/// without a live network call.
/// </summary>
public class NotionClientTests
{
    private static NotionClient Client(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), "secret-token", TimeSpan.Zero);

    [Fact]
    public void EveryRequest_CarriesNotionVersionAndBearerToken()
    {
        var handler = new FakeHttpMessageHandler().Enqueue("""{"id":"db1","data_sources":[]}""");
        Client(handler).RetrieveDatabase("db1");

        var req = handler.Requests.Single();
        Assert.Equal(NotionClient.NotionVersion, req.NotionVersion);
        Assert.Equal("Bearer secret-token", req.Authorization);
    }

    [Fact]
    public void RetrieveDatabase_GetsCorrectPath_AndParsesDataSources()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue("""{"id":"db1","data_sources":[{"id":"ds1","name":"Tasks"}]}""");

        var db = Client(handler).RetrieveDatabase("db1");

        var req = handler.Requests.Single();
        Assert.Equal("GET", req.Method);
        Assert.Equal("/v1/databases/db1", req.Path);
        Assert.Equal("ds1", db.DataSources.Single().Id);
    }

    [Fact]
    public void QueryDataSource_PostsToQueryPath_AndFollowsPagination()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue("""{"results":[{"id":"p1","properties":{}}],"has_more":true,"next_cursor":"c2"}""")
            .Enqueue("""{"results":[{"id":"p2","properties":{}}],"has_more":false,"next_cursor":null}""");

        var pages = Client(handler).QueryDataSource("ds1");

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal("POST", r.Method));
        Assert.All(handler.Requests, r => Assert.Equal("/v1/data_sources/ds1/query", r.Path));
        // First request has no cursor; the second carries the cursor from page one.
        Assert.DoesNotContain("start_cursor", handler.Requests[0].Body);
        Assert.Contains("c2", handler.Requests[1].Body);
        Assert.Equal(["p1", "p2"], pages.Select(p => p.Id));
    }

    [Fact]
    public void CreatePage_PostsToPages_WithDataSourceParent()
    {
        var handler = new FakeHttpMessageHandler().Enqueue("""{"id":"new-page","properties":{}}""");

        var req = new NotionPageCreateRequest
        {
            Parent = new NotionParent { DataSourceId = "ds1" },
            Properties = new() { ["Name"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("Hi") } },
        };
        var page = Client(handler).CreatePage(req);

        var captured = handler.Requests.Single();
        Assert.Equal("POST", captured.Method);
        Assert.Equal("/v1/pages", captured.Path);
        Assert.Contains("\"data_source_id\":\"ds1\"", captured.Body);
        Assert.Contains("\"type\":\"data_source_id\"", captured.Body);
        Assert.Equal("new-page", page.Id);
    }

    [Fact]
    public void CreateDatabase_PostsToDatabases_WithPageParent()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(
            """{"id":"db-new","data_sources":[{"id":"ds-new","name":"dydo Campaigns"}]}""");

        var req = new NotionDatabaseCreateRequest
        {
            Parent = new NotionDatabaseParent { PageId = "parent-page" },
            Title = NotionRichText.Of("dydo Campaigns"),
            InitialDataSource = new NotionInitialDataSource
            {
                Properties = new()
                {
                    ["title"] = new NotionPropertySchema { Title = new NotionEmptyConfig() },
                    ["campaign"] = new NotionPropertySchema { Relation = new NotionRelationSchema { DataSourceId = "ds-parent" } },
                },
            },
        };
        var db = Client(handler).CreateDatabase(req);

        var captured = handler.Requests.Single();
        Assert.Equal("POST", captured.Method);
        Assert.Equal("/v1/databases", captured.Path);
        Assert.Contains("\"type\":\"page_id\"", captured.Body);
        Assert.Contains("\"page_id\":\"parent-page\"", captured.Body);
        Assert.Contains("\"data_source_id\":\"ds-parent\"", captured.Body);
        // The data-source model requires the schema under initial_data_source, NOT a top-level
        // "properties" map (which Notion silently ignores) — guard the live-found regression.
        Assert.Contains("\"initial_data_source\"", captured.Body);
        Assert.Equal("ds-new", db.DataSources.Single().Id);
    }

    [Fact]
    public void UpdatePage_PatchesPagePath()
    {
        var handler = new FakeHttpMessageHandler().Enqueue("""{"id":"p1","properties":{}}""");

        Client(handler).UpdatePage("p1", new NotionPageUpdateRequest { Archived = true });

        var req = handler.Requests.Single();
        Assert.Equal("PATCH", req.Method);
        Assert.Equal("/v1/pages/p1", req.Path);
        Assert.Contains("\"archived\":true", req.Body);
    }

    [Fact]
    public void GetBlockChildren_GetsChildrenPath_AndPaginatesViaQueryString()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue("""{"results":[{"type":"paragraph","id":"b1"}],"has_more":true,"next_cursor":"c2"}""")
            .Enqueue("""{"results":[{"type":"paragraph","id":"b2"}],"has_more":false}""");

        var blocks = Client(handler).GetBlockChildren("p1");

        Assert.Equal("/v1/blocks/p1/children", handler.Requests[0].Path);
        Assert.Contains("start_cursor=c2", handler.Requests[1].Query);
        Assert.Equal(["b1", "b2"], blocks.Select(b => b.Id));
    }

    [Fact]
    public void AppendBlockChildren_PatchesChildrenPath()
    {
        var handler = new FakeHttpMessageHandler().Enqueue("""{"results":[]}""");

        Client(handler).AppendBlockChildren("p1", new NotionAppendChildrenRequest
        {
            Children = [new NotionBlock { Type = "paragraph", Paragraph = new NotionBlockBody { RichText = NotionRichText.Of("x") } }],
        });

        var req = handler.Requests.Single();
        Assert.Equal("PATCH", req.Method);
        Assert.Equal("/v1/blocks/p1/children", req.Path);
    }

    [Fact]
    public void DeleteBlock_SendsDelete()
    {
        var handler = new FakeHttpMessageHandler().Enqueue("""{"id":"b1"}""");

        Client(handler).DeleteBlock("b1");

        var req = handler.Requests.Single();
        Assert.Equal("DELETE", req.Method);
        Assert.Equal("/v1/blocks/b1", req.Path);
    }

    [Fact]
    public void SearchDataSources_PostsSearch_AndFiltersToDataSourceObjects()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(
            """{"results":[{"id":"ds1","object":"data_source"},{"id":"x","object":"page"}]}""");

        var ids = Client(handler).SearchDataSources();

        var req = handler.Requests.Single();
        Assert.Equal("/v1/search", req.Path);
        Assert.Contains("\"value\":\"data_source\"", req.Body);
        Assert.Equal(["ds1"], ids);
    }

    [Fact]
    public void NonSuccessStatus_ThrowsNotionApiException_WithStatusAndBody()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(
            """{"message":"unauthorized"}""", HttpStatusCode.Unauthorized);

        var ex = Assert.Throws<NotionApiException>(() => Client(handler).RetrieveDatabase("db1"));
        Assert.Equal(401, ex.StatusCode);
        Assert.Contains("unauthorized", ex.Message);
    }
}
