namespace DynaDocs.Tests.Map;

using System.Net;
using System.Text;
using System.Text.Json;
using DynaDocs.Services.Map;
using static LinearJson;

/// <summary>The map server over real HTTP on localhost, with a fake Linear behind it.</summary>
public sealed class MapServerTests : IAsyncLifetime
{
    private const string JsonType = "application/json; charset=utf-8";

    private readonly FakeLinear _linear = new();
    private readonly HttpClient _browser = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, byte[]> _bundle = [];
    private MapServer _server = null!;
    private Task _running = Task.CompletedTask;
    private Uri _url = null!;

    public Task InitializeAsync()
    {
        _server = new MapServer(new MapApi(_linear.Reader()), new ViewerBundle(name => _bundle.GetValueOrDefault(name)));
        _url = _server.Start();
        _running = _server.RunAsync(_stop.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _stop.CancelAsync();
        await _running;
        _server.Dispose();
        _browser.Dispose();
        _stop.Dispose();
    }

    private async Task<(HttpStatusCode Status, string? ContentType, string Body)> Get(string path, HttpMethod? method = null)
    {
        using var response = await _browser.SendAsync(new HttpRequestMessage(method ?? HttpMethod.Get, new Uri(_url, path)));
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, response.Content.Headers.ContentType?.ToString(), body);
    }

    private static (string Code, string Message) Error(string body)
    {
        var error = JsonDocument.Parse(body).RootElement.GetProperty("error");
        return (error.GetProperty("code").GetString()!, error.GetProperty("message").GetString()!);
    }

    [Fact]
    public void Start_BindsLocalhostOnAFreePort()
    {
        Assert.Equal("localhost", _url.Host);
        Assert.Equal("/", _url.AbsolutePath);
        Assert.NotEqual(0, _url.Port);
    }

    [Fact]
    public void Start_RetriesWithAFreshListener_WhenTheFirstProbedPortIsTaken()
    {
        var occupied = FreeTcpPort();
        using var blocker = new HttpListener();
        blocker.Prefixes.Add($"http://localhost:{occupied}/");
        blocker.Start();
        var free = FreeTcpPort();
        var ports = new Queue<int>([occupied, free]);

        using var server = new MapServer(new MapApi(_linear.Reader()), new ViewerBundle(_ => null), () => ports.Dequeue());
        var url = server.Start();

        Assert.Equal(free, url.Port);
        Assert.Empty(ports);
    }

    private static int FreeTcpPort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [Fact]
    public async Task Teams_AnswerTheContractShape()
    {
        _linear.Serve(_ => """{"data":{"teams":{"nodes":[{"id":"t1","key":"DYD","name":"Dydo"}],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}""");

        var (status, type, body) = await Get("/api/teams");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(JsonType, type);
        Assert.Equal("""{"teams":[{"id":"t1","key":"DYD","name":"Dydo"}]}""", body);
    }

    [Fact]
    public async Task Projects_AnswerTheContractShape()
    {
        _linear.Serve(_ => """{"data":{"team":{"projects":{"nodes":[{"id":"p1","name":"Map","url":"https://l/p1","status":{"name":"Planned","type":"planned"}}],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}""");

        var (status, _, body) = await Get("/api/projects?team=t1");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("""{"projects":[{"id":"p1","name":"Map","url":"https://l/p1","status":{"name":"Planned","type":"planned"}}]}""", body);
        Assert.Equal("t1", _linear.Calls.Single().Var("teamId"));
    }

    [Fact]
    public async Task Graph_AnswersTheContractShape()
    {
        _linear.Serve(_ => IssuesPage(
            Node(Issue("1"), inverseRelations: Connection(Incoming("r1", "blocks", Issue("9", project: "null"))))));

        var (status, type, body) = await Get("/api/graph?project=p-map");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(JsonType, type);
        var graph = JsonDocument.Parse(body).RootElement;
        Assert.Equal(["project", "issues", "external", "relations"], graph.EnumerateObject().Select(p => p.Name));
        Assert.Equal(
            ["id", "identifier", "title", "url", "state", "assignee", "parentId", "team", "project"],
            graph.GetProperty("issues")[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal(JsonValueKind.Null, graph.GetProperty("external")[0].GetProperty("project").ValueKind);
        Assert.Equal(JsonValueKind.Null, graph.GetProperty("issues")[0].GetProperty("assignee").ValueKind);
        Assert.Equal("""{"id":"r1","type":"blocks","from":"9","to":"1"}""", graph.GetProperty("relations")[0].GetRawText());
        Assert.DoesNotContain(FakeLinear.ApiKey, body);
    }

    [Fact]
    public async Task Graph_IsNotCached_EachRequestReadsLinearAgain()
    {
        var state = "Todo";
        _linear.Serve(_ => IssuesPage(Node(Issue("1", state: $$"""{"name":"{{state}}","type":"unstarted","color":"#fff"}"""))));

        var first = await Get("/api/graph?project=p-map");
        state = "In Progress";
        var second = await Get("/api/graph?project=p-map");

        Assert.Equal(2, _linear.CallsTo("ProjectIssues"));
        Assert.Contains("\"name\":\"Todo\"", first.Body);
        Assert.Contains("\"name\":\"In Progress\"", second.Body);
    }

    [Theory]
    [InlineData("/api/projects", "team")]
    [InlineData("/api/projects?team=", "team")]
    [InlineData("/api/graph", "project")]
    public async Task MissingParameter_Is400_WithoutCallingLinear(string path, string parameter)
    {
        var (status, type, body) = await Get(path);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(JsonType, type);
        Assert.Equal("missing_parameter", Error(body).Code);
        Assert.Contains(parameter, Error(body).Message);
        Assert.Empty(_linear.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, """{"data":{"project":null}}""", HttpStatusCode.NotFound, "not_found")]
    [InlineData(HttpStatusCode.BadRequest,
        """{"errors":[{"message":"Authentication required, not authenticated","extensions":{"type":"authentication error"}}]}""",
        HttpStatusCode.BadGateway, "linear_auth")]
    [InlineData(HttpStatusCode.BadRequest,
        """{"errors":[{"message":"Rate limited","extensions":{"code":"RATELIMITED"}}]}""",
        HttpStatusCode.ServiceUnavailable, "linear_rate_limited")]
    [InlineData(HttpStatusCode.OK, """{"errors":[{"message":"boom"}]}""", HttpStatusCode.BadGateway, "linear_error")]
    [InlineData(HttpStatusCode.OK, """{"data":{"project":{"id":"p"}}}""", HttpStatusCode.BadGateway, "linear_error")]
    [InlineData(HttpStatusCode.OK, """{"data":{"project":{"id":"p","name":"n","url":"u","issues":{"pageInfo":{"hasNextPage":false},"nodes":[{"id":null}]}}}}""",
        HttpStatusCode.BadGateway, "linear_error")]
    public async Task LinearFailures_AnswerTheErrorEnvelope(
        HttpStatusCode linearStatus, string linearBody, HttpStatusCode status, string code)
    {
        _linear.Answer = _ => (linearStatus, linearBody);

        var (actualStatus, type, body) = await Get("/api/graph?project=p-map");

        Assert.Equal(status, actualStatus);
        Assert.Equal(JsonType, type);
        Assert.Equal(code, Error(body).Code);
        Assert.DoesNotContain(FakeLinear.ApiKey, body);
    }

    [Theory]
    [InlineData("/api/nothing")]
    [InlineData("/assets/missing.js")]
    public async Task UnknownPath_Is404NotFound(string path)
    {
        var (status, type, body) = await Get(path);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal(JsonType, type);
        Assert.Equal("not_found", Error(body).Code);
        Assert.Empty(_linear.Calls);
    }

    [Theory]
    [InlineData("POST", "/api/teams")]
    [InlineData("DELETE", "/api/graph?project=p-map")]
    [InlineData("PUT", "/")]
    public async Task OnlyGetIsAnswered_OtherMethodsNeverReachLinear(string method, string path)
    {
        var (status, type, body) = await Get(path, new HttpMethod(method));

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal(JsonType, type);
        Assert.Contains("GET only", Error(body).Message);
        Assert.Empty(_linear.Calls);
    }

    [Fact]
    public async Task Root_WithoutABuiltViewer_SaysSo()
    {
        var (status, type, body) = await Get("/");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("text/html; charset=utf-8", type);
        Assert.Contains("not built", body);
    }

    [Fact]
    public async Task Viewer_IsServedWithItsContentTypes()
    {
        _bundle["index.html"] = Encoding.UTF8.GetBytes("<!doctype html><div id=root></div>");
        _bundle["assets/index-abc.js"] = Encoding.UTF8.GetBytes("console.log(1)");
        _bundle["assets/index-abc.css"] = Encoding.UTF8.GetBytes("body{}");

        Assert.Equal((HttpStatusCode.OK, "text/html; charset=utf-8", "<!doctype html><div id=root></div>"), await Get("/?team=t&project=p"));
        Assert.Equal((HttpStatusCode.OK, "text/javascript; charset=utf-8", "console.log(1)"), await Get("/assets/index-abc.js"));
        Assert.Equal((HttpStatusCode.OK, "text/css; charset=utf-8", "body{}"), await Get("/assets/index-abc.css"));
    }
}
