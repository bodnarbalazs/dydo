namespace DynaDocs.Tests.Map;

using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using DynaDocs.Services.Map;
using static LinearJson;

public sealed class MapApiTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-api-" + Guid.NewGuid());
    private readonly FakeLinear _linear = new();
    private MapApi Api() => new(_linear.Reader(), new(new Uri("http://fake-linear.test/graphql"), FakeLinear.ApiKey, _root));
    private static NameValueCollection Project(string id = "p-map") => new() { ["project"] = id };
    public void Dispose() { _linear.Dispose(); if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task FreshAlwaysReadsLinear_AndSavesOnlyCompleteSuccesses()
    {
        var api = Api();
        _linear.Serve(_ => IssuesPage(Node(Issue("1"))));
        Assert.Equal(200, (await api.HandleAsync("/api/graph", Project(), default)).Status);
        _linear.Answer = _ => (HttpStatusCode.BadGateway, "{\"errors\":[{\"message\":\"offline\"}]}");
        Assert.Equal(502, (await api.HandleAsync("/api/graph", Project(), default)).Status);
        var (_, saved) = await api.HandleAsync("/api/saved", Project(), default);
        Assert.Equal("Issue 1", JsonDocument.Parse(saved).RootElement.GetProperty("snapshot").GetProperty("graph").GetProperty("issues")[0].GetProperty("title").GetString());
        Assert.Equal(2, _linear.CallsTo("ProjectIssues"));
    }

    [Fact]
    public async Task AResponseForAnotherProject_IsNeverSaved()
    {
        _linear.Serve(_ => IssuesPage(Node(Issue("1"))));
        var api = Api();
        Assert.Equal(502, (await api.HandleAsync("/api/graph", Project("other"), default)).Status);
        var (_, saved) = await api.HandleAsync("/api/saved", Project(), default);
        Assert.Equal("{\"snapshot\":null}", System.Text.Encoding.UTF8.GetString(saved));
    }

    [Fact]
    public async Task SameProjectFreshReads_AreSerializedThroughCommit_WhileSavedReadsRemainResponsive()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        _linear.AsyncAnswer = async (_, ct) =>
        {
            var index = Interlocked.Increment(ref calls);
            if (index == 1) { entered.SetResult(); await release.Task.WaitAsync(ct); }
            return (HttpStatusCode.OK, IssuesPage(Node(Issue(index.ToString()))));
        };
        var api = Api();
        var first = api.HandleAsync("/api/graph", Project(), default);
        await entered.Task;
        var second = api.HandleAsync("/api/graph", Project(), default);
        var miss = await api.HandleAsync("/api/saved", Project(), default);
        Assert.Equal(200, miss.Status);
        Assert.Equal(1, calls);
        release.SetResult();
        await Task.WhenAll(first, second);
        var (_, saved) = await api.HandleAsync("/api/saved", Project(), default);
        Assert.Equal("Issue 2", JsonDocument.Parse(saved).RootElement.GetProperty("snapshot").GetProperty("graph").GetProperty("issues")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task SavedRoute_RequiresAProjectWithoutCallingLinear()
    {
        Assert.Equal(400, (await Api().HandleAsync("/api/saved", new(), default)).Status);
        Assert.Empty(_linear.Calls);
    }
}
