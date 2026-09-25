namespace DynaDocs.Services.Map;

using System.Net;
using System.Net.Sockets;

/// <summary>
/// The loopback HTTP server behind `dydo map`: `http://localhost:&lt;free port&gt;/`, GET only, the
/// viewer bundle at `/` and the API under `/api/`. The `localhost` prefix needs no URL reservation on
/// Windows and refuses requests whose Host header names anything else.
/// </summary>
internal sealed class MapServer(MapApi api, ViewerBundle viewer, Func<int>? portSource = null) : IDisposable
{
    private const int BindAttempts = 5;
    private const string Json = "application/json; charset=utf-8";

    // Test seam: the bind-retry test holds a port occupied so the first attempt fails, then hands
    // back a free one to prove the retry binds with a fresh listener.
    private readonly Func<int> nextPort = portSource ?? FreePort;

    private HttpListener listener = null!;

    /// <summary>Binds a free loopback port and returns the URL the browser opens.</summary>
    public Uri Start()
    {
        for (var attempt = 1; ; attempt++)
        {
            var url = $"http://localhost:{nextPort()}/";
            // A failed Start() disposes the HttpListener instance, so each attempt needs its own.
            var candidate = new HttpListener();
            candidate.Prefixes.Add(url);
            try
            {
                candidate.Start();
                listener = candidate;
                return new Uri(url);
            }
            // Another process can take the probed port before the listener binds it.
            catch (HttpListenerException) when (attempt < BindAttempts)
            {
            }
        }
    }

    /// <summary>Answers requests one at a time until <paramref name="ct"/> is cancelled.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        using var stop = ct.Register(listener.Stop);
        try
        {
            while (true)
                await RespondAsync(await listener.GetContextAsync(), ct);
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // Stopping the listener faults the pending accept; that is the way out.
        }
    }

    public void Dispose() => listener?.Close();

    private async Task RespondAsync(HttpListenerContext context, CancellationToken ct)
    {
        var request = context.Request;
        var path = request.Url!.AbsolutePath;
        var (status, contentType, body) = await AnswerAsync(request.HttpMethod, path, request, ct);

        var response = context.Response;
        try
        {
            response.StatusCode = status;
            response.ContentType = contentType;
            response.ContentLength64 = body.Length;
            await response.OutputStream.WriteAsync(body, ct);
            response.Close();
        }
        catch (HttpListenerException)
        {
            // The browser went away mid-answer; the next request is unaffected.
        }
    }

    private async Task<(int Status, string ContentType, byte[] Body)> AnswerAsync(
        string method, string path, HttpListenerRequest request, CancellationToken ct)
    {
        if (method != "GET")
            return (404, Json, MapApiException.NotFound($"dydo map answers GET only, not {method} {path}.").Envelope());

        if (path.StartsWith("/api/", StringComparison.Ordinal))
        {
            var (status, body) = await api.HandleAsync(path, request.QueryString, ct);
            return (status, Json, body);
        }

        return viewer.Find(path) is { } file
            ? (200, file.ContentType, file.Body)
            : (404, Json, MapApiException.NotFound($"No file {path}.").Envelope());
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
