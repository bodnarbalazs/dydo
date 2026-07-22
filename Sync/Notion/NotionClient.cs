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

    /// <summary>Total tries (initial + retries) for a transient failure before the error is surfaced.</summary>
    public const int MaxAttempts = 5;

    private static readonly TimeSpan DefaultMinInterval = TimeSpan.FromMilliseconds(334);
    private static readonly TimeSpan BaseBackoff = TimeSpan.FromMilliseconds(250);

    private readonly HttpClient _http;
    private readonly TimeSpan _minInterval;
    private readonly Action<TimeSpan> _sleep;
    private DateTime _lastRequestUtc = DateTime.MinValue;

    /// <param name="http">The transport. Tests pass one built over a fake handler.</param>
    /// <param name="token">Notion integration token — set as the bearer header, never logged.</param>
    /// <param name="minInterval">Minimum gap between requests; pass <see cref="TimeSpan.Zero"/> in tests.</param>
    /// <param name="sleep">Backoff wait hook; defaults to <see cref="Thread.Sleep(TimeSpan)"/>. Tests inject
    /// a capture to assert the computed delay without real sleeping.</param>
    public NotionClient(HttpClient http, string token, TimeSpan? minInterval = null, Action<TimeSpan>? sleep = null)
    {
        _http = http;
        _minInterval = minInterval ?? DefaultMinInterval;
        _sleep = sleep ?? Thread.Sleep;
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

    // Non-idempotent: a create that dies on a 5xx/transport-throw may have succeeded server-side, so it must
    // not blind-retry (ns-5). The provisioner recovers by re-searching before re-creating.
    public NotionDatabase CreateDatabase(NotionDatabaseCreateRequest request) =>
        Post("databases", request, NotionJsonContext.Default.NotionDatabaseCreateRequest,
            NotionJsonContext.Default.NotionDatabase, idempotent: false);

    public void UpdateDataSource(string dataSourceId, NotionDataSourceUpdateRequest request) =>
        Send(HttpMethod.Patch, $"data_sources/{dataSourceId}", request,
            NotionJsonContext.Default.NotionDataSourceUpdateRequest, NotionJsonContext.Default.NotionDatabase);

    public void ArchiveDatabase(string databaseId) =>
        Send(HttpMethod.Patch, $"databases/{databaseId}", new NotionDatabaseUpdateRequest { InTrash = true },
            NotionJsonContext.Default.NotionDatabaseUpdateRequest, NotionJsonContext.Default.NotionDatabase);

    // The created-view response is discarded (deserialized into the shared NotionDatabase shape, whose
    // unknown-field tolerance ignores the view payload) — the provisioner needs only that it succeeded.
    // Non-idempotent (ns-5): a 5xx/transport-throw is recovered by listing views and matching by name.
    public void CreateView(NotionViewCreateRequest request) =>
        Post("views", request, NotionJsonContext.Default.NotionViewCreateRequest, NotionJsonContext.Default.NotionDatabase,
            idempotent: false);

    public IReadOnlyList<NotionViewRef> ListViews(string databaseId) =>
        Get($"views?database_id={databaseId}", NotionJsonContext.Default.NotionViewList).Results;

    public NotionView RetrieveView(string viewId) =>
        Get($"views/{viewId}", NotionJsonContext.Default.NotionView);

    public void DeleteView(string viewId)
    {
        using var resp = SendWithRetry(() => new HttpRequestMessage(HttpMethod.Delete, $"views/{viewId}"));
        if (!resp.IsSuccessStatusCode)
        {
            var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new NotionApiException((int)resp.StatusCode, json);
        }
    }

    public IReadOnlyList<NotionPage> QueryDataSource(string dataSourceId) =>
        QueryPaged(dataSourceId, filter: null, sorts: null);

    public IReadOnlyList<NotionPage> QueryDataSourceSince(string dataSourceId, string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
            return QueryDataSource(dataSourceId); // no stamp cursor yet — the first tick reads the whole board once
        return QueryPaged(
            dataSourceId,
            new NotionQueryFilter { LastEditedTime = new NotionTimestampBound { OnOrAfter = cursor } },
            [new NotionQuerySort()]);
    }

    private IReadOnlyList<NotionPage> QueryPaged(string dataSourceId, NotionQueryFilter? filter, List<NotionQuerySort>? sorts)
    {
        var pages = new List<NotionPage>();
        string? cursor = null;
        do
        {
            var body = new NotionQueryRequest { StartCursor = cursor, Filter = filter, Sorts = sorts };
            var page = Post(
                $"data_sources/{dataSourceId}/query",
                body, NotionJsonContext.Default.NotionQueryRequest,
                NotionJsonContext.Default.NotionPageList);
            pages.AddRange(page.Results);
            cursor = page.HasMore ? page.NextCursor : null;
        } while (cursor != null);
        return pages;
    }

    // Non-idempotent (ns-5): re-creating a page that a lost 5xx/transport-throw already created duplicates a
    // row, so the adapter recovers by re-querying the data source for the record's title before re-creating.
    public NotionPage CreatePage(NotionPageCreateRequest request) =>
        Post("pages", request, NotionJsonContext.Default.NotionPageCreateRequest,
            NotionJsonContext.Default.NotionPage, idempotent: false);

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

    public NotionMarkdownResponse GetPageMarkdown(string pageId) =>
        Get($"pages/{pageId}/markdown", NotionJsonContext.Default.NotionMarkdownResponse);

    // PATCH /v1/pages/{id}/markdown is a discriminated command, not a flat markdown object: the replace_content
    // variant carries the new body as new_str with allow_deleting_content nested inside it. The response echoes
    // the stored markdown; the caller does not need it, so it is discarded.
    public void UpdatePageMarkdown(string pageId, string markdown, bool allowDeletingContent) =>
        Send(HttpMethod.Patch, $"pages/{pageId}/markdown",
            new NotionMarkdownUpdateRequest
            {
                ReplaceContent = new NotionMarkdownReplaceContent { NewStr = markdown, AllowDeletingContent = allowDeletingContent },
            },
            NotionJsonContext.Default.NotionMarkdownUpdateRequest, NotionJsonContext.Default.NotionMarkdownResponse);

    public IReadOnlyList<NotionChildPage> GetChildPages(string parentPageId) =>
        GetBlockChildren(parentPageId)
            .Where(b => b.Type == "child_page")
            .Select(b => new NotionChildPage { Id = b.Id ?? "", Title = b.ChildPage?.Title ?? "" })
            .ToList();

    // Notion rejects an append of more than 100 children with a 400, so a large doc's body is chunked
    // across successive PATCHes (DR 033). Zero children issues no request — Notion 400s an empty append.
    // Returns the created blocks' ids in payload order (across chunks), so the depth-limited append driver can
    // resolve a deferred deeper level against the id of the block it belongs under (ns-6).
    public IReadOnlyList<string> AppendBlockChildren(string blockId, NotionAppendChildrenRequest request)
    {
        var ids = new List<string>();
        // Chunk by both caps (≤100 top-level blocks AND ≤1000 total elements including inlined children) — a
        // depth-2 chunk of 100 parent blocks can otherwise breach Notion's 1000-element payload limit with a 400.
        foreach (var chunk in NotionBlockAppender.Chunk(request.Children))
        {
            // Non-idempotent (ns-5): re-appending duplicates blocks, so a 5xx/transport-throw is surfaced as a
            // body-sync error rather than retried — the record's snapshot does not advance and the next tick retries.
            var response = Send(HttpMethod.Patch, $"blocks/{blockId}/children",
                new NotionAppendChildrenRequest { Children = chunk },
                NotionJsonContext.Default.NotionAppendChildrenRequest, NotionJsonContext.Default.NotionBlockList,
                idempotent: false);
            // Every appended top-level block comes back with an id; a missing one means an unexpected response
            // shape the depth driver cannot resolve a deferred level against, so fail loudly rather than defer to "".
            ids.AddRange(response.Results.Select(b => b.Id
                ?? throw new NotionApiException(0, "append response missing a created block id")));
        }
        return ids;
    }

    public void DeleteBlock(string blockId)
    {
        using var resp = SendWithRetry(() => new HttpRequestMessage(HttpMethod.Delete, $"blocks/{blockId}"));
        if (!resp.IsSuccessStatusCode)
        {
            var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new NotionApiException((int)resp.StatusCode, json);
        }
    }

    public IReadOnlyList<NotionSearchResult> SearchDataSources()
    {
        var response = Post("search", new NotionSearchRequest(),
            NotionJsonContext.Default.NotionSearchRequest, NotionJsonContext.Default.NotionSearchResponse);
        return response.Results.Where(r => r.Object == "data_source").ToList();
    }

    private TResp Get<TResp>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResp> respInfo)
    {
        using var resp = SendWithRetry(() => new HttpRequestMessage(HttpMethod.Get, path));
        return ReadResponse(resp, respInfo);
    }

    private TResp Post<TReq, TResp>(
        string path, TReq body,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TReq> reqInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResp> respInfo, bool idempotent = true) =>
        Send(HttpMethod.Post, path, body, reqInfo, respInfo, idempotent);

    private TResp Send<TReq, TResp>(
        HttpMethod method, string path, TReq body,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TReq> reqInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResp> respInfo, bool idempotent = true)
    {
        using var resp = SendWithRetry(() => new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body, reqInfo),
        }, idempotent);
        return ReadResponse(resp, respInfo);
    }

    /// <summary>
    /// The single send path every request helper routes through, so spine and docs syncs share one
    /// resiliency policy. Rate responses (429 + 529 <c>service_overload</c>) are retried for EVERY request —
    /// they are unambiguous rejections, so nothing was created — honouring a <c>Retry-After</c> header when
    /// present, else backing off exponentially with jitter. The 5xx server errors (500/502/503/504) and
    /// transport-level throws (a forcibly-closed socket surfaces as an <see cref="HttpRequestException"/>
    /// before any response exists; a client timeout as a <see cref="TaskCanceledException"/>) are retried only
    /// when <paramref name="idempotent"/> is true: a non-idempotent create (page/database/view/append) may have
    /// succeeded server-side before the error surfaced, so re-sending would duplicate — the create sender opts
    /// out and recovers by re-query instead (ns-5). Retries run up to <see cref="MaxAttempts"/> total tries.
    /// Every other status — success or a hard 4xx like the archived-ancestor 400 — returns on the first try.
    /// On the final try (or an opted-out non-idempotent throw) a transport throw is wrapped in a
    /// <see cref="NotionApiException"/> (status 0) so the caller's catch surfaces a clean error instead of an
    /// unhandled crash. The request is rebuilt per attempt because an <see cref="HttpRequestMessage"/> (and its
    /// content stream) cannot be resent. <see cref="Throttle"/> still gates each attempt; the backoff is in
    /// addition to it.
    /// </summary>
    private HttpResponseMessage SendWithRetry(Func<HttpRequestMessage> requestFactory, bool idempotent = true)
    {
        for (var attempt = 1; ; attempt++)
        {
            Throttle();
            using var request = requestFactory();
            HttpResponseMessage resp;
            try
            {
                resp = _http.SendAsync(request).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (IsTransientTransport(ex))
            {
                if (!idempotent || attempt >= MaxAttempts)
                    throw new NotionApiException(0, ex.Message);
                _sleep(Backoff(attempt));
                continue;
            }

            if (resp.IsSuccessStatusCode || attempt >= MaxAttempts || !ShouldRetry((int)resp.StatusCode, idempotent))
                return resp;

            var delay = RetryDelay(resp, attempt);
            resp.Dispose();
            _sleep(delay);
        }
    }

    // A rate response (429/529) is retried for every request; a 5xx server error only for idempotent ones.
    private static bool ShouldRetry(int status, bool idempotent) =>
        IsRateResponse(status) || (idempotent && IsServerError(status));

    private static bool IsRateResponse(int status) => status is 429 or 529;

    // NotionApiException.AmbiguousOutcome mirrors this set (plus status 0 for a transport throw) — the statuses a
    // non-idempotent create suppresses here are exactly the ones its recovery re-queries on. Keep the two in sync.
    private static bool IsServerError(int status) => status is 500 or 502 or 503 or 504;

    // A transport failure throws before any HTTP response: a forcibly-closed socket / IO error surfaces
    // as HttpRequestException, and a client-side timeout as TaskCanceledException (OperationCanceledException).
    // No external CancellationToken is ever passed in, so a cancellation here can only be a timeout —
    // transient, and safe to retry.
    private static bool IsTransientTransport(Exception ex) =>
        ex is HttpRequestException or OperationCanceledException;

    private static TimeSpan RetryDelay(HttpResponseMessage resp, int attempt)
    {
        // Both rate responses (429 + 529) carry a Retry-After the server wants honoured; a 5xx does not.
        if (IsRateResponse((int)resp.StatusCode) && resp.Headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta && delta > TimeSpan.Zero)
                return delta;
            if (retryAfter.Date is { } date && date - DateTimeOffset.UtcNow is { Ticks: > 0 } until)
                return until;
        }
        return Backoff(attempt);
    }

    // Exponential backoff (250ms, 500ms, 1s, …) plus jitter up to one base interval to de-correlate
    // concurrent retriers — a transport throw has no Retry-After, so this is its only delay source.
    private static TimeSpan Backoff(int attempt) =>
        BaseBackoff * Math.Pow(2, attempt - 1)
        + TimeSpan.FromMilliseconds(Random.Shared.Next((int)BaseBackoff.TotalMilliseconds));

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
