namespace DynaDocs.Sync.Notion;

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DynaDocs.Serialization;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper over the handful of Notion REST endpoints the sync adapter
/// uses (Decision 025 §6). Source-generated JSON only — no reflection, no SDK. The token is set as a
/// bearer header once and never logged. The <see cref="HttpClient"/> is injected so tests drive it
/// with a fake handler and no live network. A minimum inter-request interval honours Notion's
/// ~3 req/sec average rate limit.
/// </summary>
public sealed class NotionClient : INotionClient
{
    public const string BaseUrl = "https://api.notion.com/v1/";
    public const string NotionVersion = "2026-03-11";

    private static readonly TimeSpan DefaultMinInterval = TimeSpan.FromMilliseconds(334);

    private readonly HttpClient _http;
    private readonly TimeSpan _minInterval;
    private DateTime _lastRequestUtc = DateTime.MinValue;

    /// <param name="http">The transport. Tests pass one built over a fake handler.</param>
    /// <param name="token">Notion integration token — set as the bearer header, never logged.</param>
    /// <param name="minInterval">Minimum gap between requests; pass <see cref="TimeSpan.Zero"/> in tests.</param>
    public NotionClient(HttpClient http, string token, TimeSpan? minInterval = null)
    {
        _http = http;
        _minInterval = minInterval ?? DefaultMinInterval;
        if (_http.BaseAddress == null)
            _http.BaseAddress = new Uri(BaseUrl);
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        _http.DefaultRequestHeaders.Remove("Notion-Version");
        _http.DefaultRequestHeaders.Add("Notion-Version", NotionVersion);
    }

    public NotionDatabase RetrieveDatabase(string databaseId) =>
        Get($"databases/{databaseId}", NotionJsonContext.Default.NotionDatabase);

    public NotionDataSource RetrieveDataSource(string dataSourceId) =>
        Get($"data_sources/{dataSourceId}", NotionJsonContext.Default.NotionDataSource);

    public NotionDatabase CreateDatabase(NotionDatabaseCreateRequest request) =>
        Post("databases", request, NotionJsonContext.Default.NotionDatabaseCreateRequest,
            NotionJsonContext.Default.NotionDatabase);

    public void UpdateDataSource(string dataSourceId, NotionDataSourceUpdateRequest request) =>
        Send(HttpMethod.Patch, $"data_sources/{dataSourceId}", request,
            NotionJsonContext.Default.NotionDataSourceUpdateRequest, NotionJsonContext.Default.NotionDatabase);

    // The created-view response is discarded (deserialized into the shared NotionDatabase shape, whose
    // unknown-field tolerance ignores the view payload) — the provisioner needs only that it succeeded.
    public void CreateView(NotionViewCreateRequest request) =>
        Post("views", request, NotionJsonContext.Default.NotionViewCreateRequest, NotionJsonContext.Default.NotionDatabase);

    public IReadOnlyList<string> ListViewIds(string databaseId) =>
        Get($"views?database_id={databaseId}", NotionJsonContext.Default.NotionViewList).Results.Select(v => v.Id).ToList();

    public void DeleteView(string viewId)
    {
        Throttle();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"views/{viewId}");
        using var resp = _http.SendAsync(request).GetAwaiter().GetResult();
        if (!resp.IsSuccessStatusCode)
        {
            var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new NotionApiException((int)resp.StatusCode, json);
        }
    }

    public IReadOnlyList<NotionPage> QueryDataSource(string dataSourceId)
    {
        var pages = new List<NotionPage>();
        string? cursor = null;
        do
        {
            var body = new NotionQueryRequest { StartCursor = cursor };
            var page = Post(
                $"data_sources/{dataSourceId}/query",
                body, NotionJsonContext.Default.NotionQueryRequest,
                NotionJsonContext.Default.NotionPageList);
            pages.AddRange(page.Results);
            cursor = page.HasMore ? page.NextCursor : null;
        } while (cursor != null);
        return pages;
    }

    public NotionPage CreatePage(NotionPageCreateRequest request) =>
        Post("pages", request, NotionJsonContext.Default.NotionPageCreateRequest,
            NotionJsonContext.Default.NotionPage);

    public NotionPage UpdatePage(string pageId, NotionPageUpdateRequest request) =>
        Send(HttpMethod.Patch, $"pages/{pageId}", request,
            NotionJsonContext.Default.NotionPageUpdateRequest, NotionJsonContext.Default.NotionPage);

    public IReadOnlyList<NotionBlock> GetBlockChildren(string blockId)
    {
        var blocks = new List<NotionBlock>();
        string? cursor = null;
        do
        {
            var path = $"blocks/{blockId}/children" + (cursor == null ? "" : $"?start_cursor={cursor}");
            var page = Get(path, NotionJsonContext.Default.NotionBlockList);
            blocks.AddRange(page.Results);
            cursor = page.HasMore ? page.NextCursor : null;
        } while (cursor != null);
        return blocks;
    }

    public IReadOnlyList<NotionChildPage> GetChildPages(string parentPageId) =>
        GetBlockChildren(parentPageId)
            .Where(b => b.Type == "child_page")
            .Select(b => new NotionChildPage { Id = b.Id ?? "", Title = b.ChildPage?.Title ?? "" })
            .ToList();

    // Notion rejects an append of more than 100 children with a 400, so a large doc's body is chunked
    // across successive PATCHes (DR 033). Zero children issues no request — Notion 400s an empty append.
    public void AppendBlockChildren(string blockId, NotionAppendChildrenRequest request)
    {
        const int maxPerRequest = 100;
        for (var i = 0; i < request.Children.Count; i += maxPerRequest)
        {
            var chunk = request.Children.GetRange(i, Math.Min(maxPerRequest, request.Children.Count - i));
            Send(HttpMethod.Patch, $"blocks/{blockId}/children", new NotionAppendChildrenRequest { Children = chunk },
                NotionJsonContext.Default.NotionAppendChildrenRequest, NotionJsonContext.Default.NotionBlockList);
        }
    }

    public void DeleteBlock(string blockId)
    {
        Throttle();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"blocks/{blockId}");
        using var resp = _http.SendAsync(request).GetAwaiter().GetResult();
        if (!resp.IsSuccessStatusCode)
        {
            var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new NotionApiException((int)resp.StatusCode, json);
        }
    }

    public IReadOnlyList<string> SearchDataSources()
    {
        var response = Post("search", new NotionSearchRequest(),
            NotionJsonContext.Default.NotionSearchRequest, NotionJsonContext.Default.NotionSearchResponse);
        return response.Results
            .Where(r => r.Object == "data_source")
            .Select(r => r.Id)
            .ToList();
    }

    private TResp Get<TResp>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResp> respInfo)
    {
        Throttle();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var resp = _http.SendAsync(request).GetAwaiter().GetResult();
        return ReadResponse(resp, respInfo);
    }

    private TResp Post<TReq, TResp>(
        string path, TReq body,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TReq> reqInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResp> respInfo) =>
        Send(HttpMethod.Post, path, body, reqInfo, respInfo);

    private TResp Send<TReq, TResp>(
        HttpMethod method, string path, TReq body,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TReq> reqInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResp> respInfo)
    {
        Throttle();
        using var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, reqInfo),
        };
        using var resp = _http.SendAsync(request).GetAwaiter().GetResult();
        return ReadResponse(resp, respInfo);
    }

    private static TResp ReadResponse<TResp>(
        HttpResponseMessage resp, System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResp> respInfo)
    {
        var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!resp.IsSuccessStatusCode)
            throw new NotionApiException((int)resp.StatusCode, json);
        return JsonSerializer.Deserialize(json, respInfo)
            ?? throw new NotionApiException((int)resp.StatusCode, "empty response body");
    }

    /// <summary>Block until at least <see cref="_minInterval"/> has elapsed since the last request.</summary>
    private void Throttle()
    {
        if (_minInterval <= TimeSpan.Zero)
            return;
        var wait = _minInterval - (DateTime.UtcNow - _lastRequestUtc);
        if (wait > TimeSpan.Zero)
            Thread.Sleep(wait);
        _lastRequestUtc = DateTime.UtcNow;
    }
}
