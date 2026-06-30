namespace DynaDocs.Tests.Sync.Notion;

using System.Net;
using System.Net.Http;

/// <summary>
/// Records every request and returns canned responses queued by the test — no live network. Each
/// captured <see cref="Capture"/> exposes the method, absolute path + query, the Notion-Version and
/// Authorization headers, and the request body, so tests assert request shape exactly.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();
    public List<Capture> Requests { get; } = [];

    public FakeHttpMessageHandler Enqueue(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue((status, json));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new Capture(
            request.Method.Method,
            request.RequestUri!.AbsolutePath,
            request.RequestUri.Query,
            request.Headers.TryGetValues("Notion-Version", out var v) ? string.Join(",", v) : null,
            request.Headers.Authorization?.ToString(),
            body));

        var (status, respBody) = _responses.Count > 0 ? _responses.Dequeue() : (HttpStatusCode.OK, "{}");
        return new HttpResponseMessage(status) { Content = new StringContent(respBody) };
    }

    public sealed record Capture(
        string Method, string Path, string Query, string? NotionVersion, string? Authorization, string Body);
}
