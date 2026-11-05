namespace DynaDocs.Tests.Survey;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using DynaDocs.Tests.Map;

/// <summary>
/// Serves a <see cref="FakeLinear"/> over loopback HTTP, so a command reaches it through the
/// <c>DYDO_LINEAR_ENDPOINT</c> seam exactly as it would reach Linear. Requests are answered one at a time.
/// </summary>
internal sealed class FakeLinearServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly HttpMessageInvoker _linear;
    private readonly Task _serving;

    public FakeLinearServer(FakeLinear linear)
    {
        _linear = new HttpMessageInvoker(linear, disposeHandler: false);
        var port = FreePort();
        Endpoint = new Uri($"http://localhost:{port}/graphql");
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();
        _serving = ServeAsync();
    }

    public Uri Endpoint { get; }

    /// <summary>A loopback port nothing listens on, for an endpoint that never answers.</summary>
    public static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    private async Task ServeAsync()
    {
        while (await NextAsync() is { } context)
        {
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), Endpoint)
            {
                Content = new StreamContent(context.Request.InputStream)
            };
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType!);
            request.Headers.TryAddWithoutValidation("Authorization", context.Request.Headers["Authorization"]);

            using var answer = await _linear.SendAsync(request, CancellationToken.None);
            context.Response.StatusCode = (int)answer.StatusCode;
            context.Response.ContentType = "application/json";
            await answer.Content.CopyToAsync(context.Response.OutputStream);
            context.Response.Close();
        }
    }

    private async Task<HttpListenerContext?> NextAsync()
    {
        try
        {
            return await _listener.GetContextAsync();
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            // Closing the listener faults the pending accept; that is the way out.
            return null;
        }
    }

    public void Dispose()
    {
        _listener.Close();
        _serving.GetAwaiter().GetResult();
        _linear.Dispose();
    }
}
