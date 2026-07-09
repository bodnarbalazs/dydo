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
    private static NotionClient Client(FakeHttpMessageHandler handler, Action<TimeSpan>? sleep = null) =>
        new(new HttpClient(handler), "secret-token", TimeSpan.Zero, sleep);

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
    public void WriteRequest_OmitsNullReadOnlyFields_LikeRichTextPlainText()
    {
        // NotionRichText.Of sets only text.content; plain_text is read-only and null on write. Notion's
        // write API rejects a null plain_text, so it must be absent from the serialized body entirely.
        var handler = new FakeHttpMessageHandler().Enqueue("""{"id":"p1","properties":{}}""");

        var req = new NotionPageCreateRequest
        {
            Parent = new NotionParent { DataSourceId = "ds1" },
            Properties = new() { ["Name"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("Hi") } },
        };
        Client(handler).CreatePage(req);

        var body = handler.Requests.Single().Body;
        Assert.DoesNotContain("plain_text", body);
        Assert.Contains("\"content\":\"Hi\"", body);
    }

    [Fact]
    public void CreateDatabase_RelationProperty_SerializesSinglePropertyTypeOnTheWire()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(
            """{"id":"db-new","data_sources":[{"id":"ds-new","name":"Sprints"}]}""");

        var req = new NotionDatabaseCreateRequest
        {
            Parent = new NotionDatabaseParent { PageId = "parent-page" },
            Title = NotionRichText.Of("Sprints"),
            InitialDataSource = new NotionInitialDataSource
            {
                Properties = new()
                {
                    ["campaign"] = new NotionPropertySchema { Relation = new NotionRelationSchema { DataSourceId = "ds-parent" } },
                },
            },
        };
        Client(handler).CreateDatabase(req);

        // Assert the wire value, not just the object graph: a relation schema must carry the
        // single_property discriminator or Notion rejects/ignores the relation config.
        Assert.Contains("\"type\":\"single_property\"", handler.Requests.Single().Body);
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
    public void UpdatePageMarkdown_PatchesMarkdownPath_WithReplaceContentCommandShape()
    {
        // The live 400 ('body.type should be defined'): PATCH /v1/pages/{id}/markdown is a DISCRIMINATED command,
        // not a flat { markdown } object. The body must carry the replace_content variant — top-level type
        // discriminator, the markdown under new_str (NOT "markdown"), and allow_deleting_content NESTED inside the
        // replace_content object — or Notion rejects it (DR 035 §1).
        var handler = new FakeHttpMessageHandler().Enqueue("""{"object":"page_markdown","id":"p1","markdown":"body","truncated":false}""");

        Client(handler).UpdatePageMarkdown("p1", "# Title\n\nbody", allowDeletingContent: true);

        var req = handler.Requests.Single();
        Assert.Equal("PATCH", req.Method);
        Assert.Equal("/v1/pages/p1/markdown", req.Path);
        Assert.Contains("\"type\":\"replace_content\"", req.Body);
        Assert.Contains("\"replace_content\":{", req.Body);
        Assert.Contains("\"new_str\":\"# Title\\n\\nbody\"", req.Body); // markdown is new_str, with real \n
        Assert.Contains("\"allow_deleting_content\":true", req.Body);
        Assert.DoesNotContain("\"markdown\":", req.Body);              // never the retired flat markdown field
    }

    [Fact]
    public void UpdatePageMarkdown_ChildSafe_NestsAllowDeletingContentFalse()
    {
        // A folder-body update passes allow_deleting_content:false so the destructive replace never trashes the
        // page's child pages (makenotion/notion-mcp-server#171) — the flag lives INSIDE replace_content, not top level.
        var handler = new FakeHttpMessageHandler().Enqueue("""{"object":"page_markdown","id":"p1","markdown":"x","truncated":false}""");

        Client(handler).UpdatePageMarkdown("p1", "x", allowDeletingContent: false);

        Assert.Contains("\"replace_content\":{\"new_str\":\"x\",\"allow_deleting_content\":false}", handler.Requests.Single().Body);
    }

    [Fact]
    public void GetPageMarkdown_GetsMarkdownPath_AndSurfacesTruncatedFlag()
    {
        // The GET envelope is { object, id, markdown, truncated, unknown_block_ids }; the truncated flag must be
        // surfaced so a body past Notion's ~20k-block export ceiling is never persisted as if whole (DR 035 §4).
        var handler = new FakeHttpMessageHandler().Enqueue(
            """{"object":"page_markdown","id":"p1","markdown":"partial","truncated":true,"unknown_block_ids":["b7"]}""");

        var resp = Client(handler).GetPageMarkdown("p1");

        var req = handler.Requests.Single();
        Assert.Equal("GET", req.Method);
        Assert.Equal("/v1/pages/p1/markdown", req.Path);
        Assert.Equal("partial", resp.Markdown);
        Assert.True(resp.Truncated);
        Assert.Equal(["b7"], resp.UnknownBlockIds!);
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
    public void CreatePage_UnderPageParent_SerializesPageIdParent()
    {
        var handler = new FakeHttpMessageHandler().Enqueue("""{"id":"child","properties":{}}""");

        var req = new NotionPageCreateRequest
        {
            Parent = NotionParent.Page("parent-page"),
            Properties = new() { ["title"] = new NotionPropertyValue { Type = "title", Title = NotionRichText.Of("Understand") } },
        };
        var page = Client(handler).CreatePage(req);

        var body = handler.Requests.Single().Body;
        Assert.Contains("\"type\":\"page_id\"", body);
        Assert.Contains("\"page_id\":\"parent-page\"", body);
        // A page parent must not also carry a data_source_id, or Notion rejects the ambiguous parent.
        Assert.DoesNotContain("data_source_id", body);
        Assert.Equal("child", page.Id);
    }

    [Fact]
    public void GetChildPages_GetsChildrenPath_AndProjectsOnlyChildPageBlocks()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(
            """{"results":[{"type":"child_page","id":"cp1","child_page":{"title":"Understand"}},{"type":"paragraph","id":"b1"}],"has_more":false}""");

        var pages = Client(handler).GetChildPages("root");

        Assert.Equal("/v1/blocks/root/children", handler.Requests[0].Path);
        // The paragraph block is body content, not a nested page — only the child_page block is returned.
        Assert.Equal(["cp1"], pages.Select(p => p.Id));
        Assert.Equal("Understand", pages.Single().Title);
    }

    [Fact]
    public void AppendBlockChildren_ChunksAt100ChildrenPerRequest()
    {
        // 250 children exceed Notion's 100-per-append cap, so the client must split them across 3 PATCHes.
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < 3; i++)
            handler.Enqueue("""{"results":[]}""");

        var children = Enumerable.Range(0, 250)
            .Select(i => new NotionBlock { Type = "paragraph", Paragraph = new NotionBlockBody { RichText = NotionRichText.Of("l" + i) } })
            .ToList();
        Client(handler).AppendBlockChildren("p1", new NotionAppendChildrenRequest { Children = children });

        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Equal("PATCH", r.Method));
        Assert.All(handler.Requests, r => Assert.Equal("/v1/blocks/p1/children", r.Path));
    }

    [Fact]
    public void AppendBlockChildren_Exactly100Children_IssuesOneRequest()
    {
        // The 100-per-append cap is inclusive: exactly 100 children fit in a single PATCH.
        var handler = new FakeHttpMessageHandler().Enqueue("""{"results":[]}""");

        var children = Enumerable.Range(0, 100)
            .Select(i => new NotionBlock { Type = "paragraph", Paragraph = new NotionBlockBody { RichText = NotionRichText.Of("l" + i) } })
            .ToList();
        Client(handler).AppendBlockChildren("p1", new NotionAppendChildrenRequest { Children = children });

        Assert.Single(handler.Requests);
    }

    [Fact]
    public void AppendBlockChildren_Exactly200Children_IssuesTwoRequests()
    {
        // 200 children split into exactly two full 100-child PATCHes — no empty third request.
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < 2; i++)
            handler.Enqueue("""{"results":[]}""");

        var children = Enumerable.Range(0, 200)
            .Select(i => new NotionBlock { Type = "paragraph", Paragraph = new NotionBlockBody { RichText = NotionRichText.Of("l" + i) } })
            .ToList();
        Client(handler).AppendBlockChildren("p1", new NotionAppendChildrenRequest { Children = children });

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void AppendBlockChildren_EmptyChildren_IssuesNoRequest()
    {
        var handler = new FakeHttpMessageHandler();

        Client(handler).AppendBlockChildren("p1", new NotionAppendChildrenRequest { Children = [] });

        Assert.Empty(handler.Requests);
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

    [Fact]
    public void TransientServerError_RetriesAndSucceeds()
    {
        // A single transient 503 must not abort the call — the client retries and succeeds on the 200.
        var handler = new FakeHttpMessageHandler()
            .Enqueue("""{"message":"unavailable"}""", HttpStatusCode.ServiceUnavailable)
            .Enqueue("""{"id":"db1","data_sources":[]}""");

        var db = Client(handler, sleep: _ => { }).RetrieveDatabase("db1");

        Assert.Equal("db1", db.Id);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void RateLimited_HonorsRetryAfterHeader_ThenSucceeds()
    {
        // On a 429 the client waits exactly the server-supplied Retry-After (2s here), not a computed
        // backoff — captured via the injected sleep so no real waiting happens.
        var delays = new List<TimeSpan>();
        var handler = new FakeHttpMessageHandler()
            .Enqueue("""{"message":"rate limited"}""", (HttpStatusCode)429, retryAfterSeconds: 2)
            .Enqueue("""{"id":"db1","data_sources":[]}""");

        var db = Client(handler, sleep: delays.Add).RetrieveDatabase("db1");

        Assert.Equal("db1", db.Id);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(TimeSpan.FromSeconds(2), Assert.Single(delays));
    }

    [Fact]
    public void PersistentServerError_ThrowsAfterExactlyMaxAttempts()
    {
        // A 500 that never clears is surfaced as today — but only after the bounded retry budget is spent.
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < NotionClient.MaxAttempts; i++)
            handler.Enqueue("""{"message":"server error"}""", HttpStatusCode.InternalServerError);

        var ex = Assert.Throws<NotionApiException>(() => Client(handler, sleep: _ => { }).RetrieveDatabase("db1"));

        Assert.Equal(500, ex.StatusCode);
        Assert.Equal(NotionClient.MaxAttempts, handler.Requests.Count);
    }

    [Fact]
    public void HardClientError_FailsImmediately_WithNoRetry()
    {
        // The archived-ancestor 400 is a hard error: it must fail on the first try, with no retry and no
        // backoff wait, so operator mistakes aren't silently hammered MaxAttempts times.
        var sleepCalls = 0;
        var handler = new FakeHttpMessageHandler().Enqueue(
            """{"object":"error","status":400,"code":"validation_error","message":"Can't edit a block that is archived."}""",
            HttpStatusCode.BadRequest);

        var ex = Assert.Throws<NotionApiException>(
            () => Client(handler, sleep: _ => sleepCalls++).RetrieveDatabase("db1"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Single(handler.Requests);
        Assert.Equal(0, sleepCalls);
    }

    [Fact]
    public void TransientTransportThrow_RetriesAndSucceeds()
    {
        // A forcibly-closed socket surfaces as HttpRequestException before any response exists — the
        // status-code retry never sees it, so SendWithRetry must catch the throw and retry to the 200.
        var handler = new FakeHttpMessageHandler()
            .EnqueueThrow(new HttpRequestException("connection forcibly closed"))
            .Enqueue("""{"id":"db1","data_sources":[]}""");

        var db = Client(handler, sleep: _ => { }).RetrieveDatabase("db1");

        Assert.Equal("db1", db.Id);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void PersistentTransportThrow_WrapsInNotionApiException_AfterMaxAttempts()
    {
        // A transport failure that never clears must not escape as a raw HttpRequestException (that
        // crashed the whole sync unhandled) — it's wrapped in NotionApiException so the caller's existing
        // catch surfaces a clean ToolError. Assert.Throws<NotionApiException> also proves it's NOT the raw
        // HttpRequestException.
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < NotionClient.MaxAttempts; i++)
            handler.EnqueueThrow(new HttpRequestException("connection forcibly closed"));

        var ex = Assert.Throws<NotionApiException>(() => Client(handler, sleep: _ => { }).RetrieveDatabase("db1"));

        Assert.Contains("connection forcibly closed", ex.Message);
        Assert.Equal(NotionClient.MaxAttempts, handler.Requests.Count);
    }

    [Fact]
    public void TimeoutThrow_RetriesAndSucceeds()
    {
        // A client timeout surfaces as TaskCanceledException (no external CancellationToken is ever passed,
        // so a cancellation here can only be a timeout) — likewise transient and retried to the 200.
        var handler = new FakeHttpMessageHandler()
            .EnqueueThrow(new TaskCanceledException("timed out"))
            .Enqueue("""{"id":"db1","data_sources":[]}""");

        var db = Client(handler, sleep: _ => { }).RetrieveDatabase("db1");

        Assert.Equal("db1", db.Id);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void PersistentTimeoutThrow_WrapsInNotionApiException_AfterMaxAttempts()
    {
        // A timeout that never clears is wrapped just like a socket reset — never an unhandled crash.
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < NotionClient.MaxAttempts; i++)
            handler.EnqueueThrow(new TaskCanceledException("timed out"));

        Assert.Throws<NotionApiException>(() => Client(handler, sleep: _ => { }).RetrieveDatabase("db1"));

        Assert.Equal(NotionClient.MaxAttempts, handler.Requests.Count);
    }
}
