namespace DynaDocs.Tests.Map;

using System.Net;
using System.Text.Json;
using DynaDocs.Serialization;
using DynaDocs.Services.Map;
using DynaDocs.Services.Map.Contract;
using Reqnroll;
using static LinearJson;

[Binding]
public sealed class MapMemorySteps
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-memory-" + Guid.NewGuid());
    private readonly FakeLinear _linear = new();
    private readonly HttpClient _browser = new();
    private CancellationTokenSource _stop = new();
    private MapServer _server = null!;
    private Task _running = Task.CompletedTask;
    private Uri _url = null!;
    private string _project = "p-map";
    private string _key = "test-key";
    private string _endpoint = "https://api.linear.app/graphql";
    private MapGraph _fresh = null!;
    private int _callsBeforeSaved;
    private Task<HttpResponseMessage>? _blocked;
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private MapSnapshotCache Cache() => new(new Uri(_endpoint), _key, _root);

    [Given("an isolated map cache")]
    public void Start()
    {
        _server = new(new MapApi(_linear.Reader(), Cache()), new ViewerBundle(_ => null));
        _url = _server.Start();
        _running = _server.RunAsync(_stop.Token);
    }

    [When("I open project {string} with Linear title {string}")]
    public async Task Open(string project, string title)
    {
        _project = project;
        _linear.Serve(_ => IssuesPage(Node(Issue("1"))).Replace("Issue 1", title, StringComparison.Ordinal));
        using var response = await _browser.GetAsync(new Uri(_url, $"api/graph?project={project}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _fresh = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), MapJsonContext.Default.MapGraph)!;
    }

    [Then("the fresh map title is {string}")]
    public void FreshTitle(string title) => Assert.Equal(title, _fresh.Issues.Single().Title);

    private async Task<MapSnapshot?> Saved()
    {
        _callsBeforeSaved = _linear.Calls.Count;
        using var response = await _browser.GetAsync(new Uri(_url, $"api/saved?project={_project}"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), MapJsonContext.Default.MapSavedResponse)!.Snapshot;
    }

    [Then("the saved map title is {string}")]
    public async Task SavedTitle(string title)
    {
        var saved = await Saved();
        Assert.NotNull(saved);
        Assert.Equal(title, saved.Graph.Issues.Single().Title);
        Assert.Equal(TimeSpan.Zero, saved.FetchedAt.Offset);
        Assert.NotEqual(default, saved.FetchedAt);
    }

    [Then("reading the saved map made no Linear request")]
    public void NoLinearRead() => Assert.Equal(_callsBeforeSaved, _linear.Calls.Count);

    [When("I restart the map server")]
    public async Task Restart()
    {
        var port = _url.Port;
        await Stop();
        _stop.Dispose();
        _stop = new();
        Start();
        Assert.NotEqual(port, _url.Port);
    }

    [When("Linear fails on Refresh")]
    public async Task Fail()
    {
        _linear.Answer = _ => (HttpStatusCode.BadGateway, "{\"errors\":[{\"message\":\"offline\"}]}");
        using var response = await _browser.GetAsync(new Uri(_url, $"api/graph?project={_project}"));
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [When("I use project {string} and key {string} and endpoint {string}")]
    public async Task Scope(string project, string key, string endpoint)
    {
        _project = project; _key = key; _endpoint = endpoint;
        await Restart();
    }

    [Then("the saved map is missing")]
    public async Task Missing() => Assert.Null(await Saved());

    [When("the saved map becomes {string}")]
    public async Task Damage(string damage)
    {
        var file = Directory.GetFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        switch (damage)
        {
            case "corrupt": await File.WriteAllTextAsync(file, "broken"); break;
            case "incompatible":
                var json = await File.ReadAllTextAsync(file);
                await File.WriteAllTextAsync(file, json.Replace("\"version\":1", "\"version\":999", StringComparison.Ordinal));
                break;
            case "unreadable": File.Delete(file); Directory.CreateDirectory(file); break;
            case "unwritable": Directory.Delete(_root, true); await File.WriteAllTextAsync(_root, "file"); break;
            default: throw new ArgumentException(damage);
        }
    }

    [When("simultaneous writers save different maps")]
    public async Task Writers()
    {
        await Task.WhenAll(Enumerable.Range(1, 10).Select(i => Cache().WriteAsync(
            new(new(new("p-map", $"Map {i}", "url"), [], [], []), DateTimeOffset.UnixEpoch.AddSeconds(i)), default)));
    }

    [Then("one complete saved map remains")]
    public async Task Complete()
    {
        var saved = await Saved();
        Assert.NotNull(saved);
        Assert.Equal($"Map {(saved.FetchedAt - DateTimeOffset.UnixEpoch).TotalSeconds}", saved.Graph.Project.Name);
        Assert.Single(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [When("another project has a blocked Linear fetch")]
    public async Task Blocked()
    {
        _linear.AsyncAnswer = async (_, ct) =>
        {
            _entered.SetResult();
            try { await Task.Delay(Timeout.Infinite, ct); }
            finally { _cancelled.SetResult(); }
            return (HttpStatusCode.OK, "{}");
        };
        _blocked = _browser.GetAsync(new Uri(_url, "api/graph?project=other"));
        await _entered.Task;
    }

    [Then("the blocked fetch ends when the server stops")]
    public async Task Drained()
    {
        Assert.False(_blocked!.IsCompleted);
        await Stop();
        Assert.True(_cancelled.Task.IsCompletedSuccessfully);
        try { using var response = await _blocked; }
        catch (HttpRequestException) { }
    }

    private async Task Stop()
    {
        await _stop.CancelAsync();
        await _running;
        _server.Dispose();
    }

    [AfterScenario("map-memory")]
    public async Task Cleanup()
    {
        await Stop();
        _stop.Dispose(); _browser.Dispose(); _linear.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
        if (File.Exists(_root)) File.Delete(_root);
    }
}
