namespace DynaDocs.Tests.Map;

using DynaDocs.Services.Map;
using DynaDocs.Services.Map.Contract;

public sealed class MapSnapshotCacheTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-map-" + Guid.NewGuid());
    private static readonly Uri Endpoint = new("https://api.linear.app/graphql");
    private static readonly DateTimeOffset Fetched = DateTimeOffset.Parse("2026-10-04T12:00:00Z");
    private MapSnapshotCache Cache(string key = "test-key", string? endpoint = null) => new(new Uri(endpoint ?? Endpoint.AbsoluteUri), key, _root);
    private static MapGraph Graph(string id = "p1") => new(new(id, "Map", "https://linear.app/p1"), [], [], []);

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task FirstVisit_IsAMiss() => Assert.Null(await Cache().ReadAsync("p1", default));

    [Fact]
    public async Task Restart_RetainsCompleteGraphAndUtcTimestamp()
    {
        await Cache().WriteAsync(new(Graph(), Fetched), default);
        var saved = await Cache().ReadAsync("p1", default);
        Assert.NotNull(saved);
        Assert.Equal("Map", saved.Graph.Project.Name);
        Assert.Equal(Fetched, saved.FetchedAt);
        Assert.Equal(TimeSpan.Zero, saved.FetchedAt.Offset);
    }

    [Theory]
    [InlineData("p2", "test-key", "https://api.linear.app/graphql")]
    [InlineData("p1", "rotated-key", "https://api.linear.app/graphql")]
    [InlineData("p1", "test-key ", "https://api.linear.app/graphql")]
    [InlineData("p1", "test-key", "https://other.test/graphql")]
    public async Task ProjectsExactKeysAndEndpoints_AreIsolated(string project, string key, string endpoint)
    {
        await Cache().WriteAsync(new(Graph(), Fetched), default);
        Assert.Null(await Cache(key, endpoint).ReadAsync(project, default));
    }

    [Fact]
    public async Task EndpointAndProjectCanonicalization_IsStableAndSecretsAreAbsent()
    {
        const string id = "ABCD1234-1234-1234-1234-123456789ABC";
        await Cache().WriteAsync(new(Graph(id), Fetched), default);
        Assert.NotNull(await Cache(endpoint: "https://API.LINEAR.APP:443/graphql").ReadAsync(id.ToLowerInvariant(), default));
        foreach (var file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("test-key", file);
            Assert.DoesNotContain("test-key", await File.ReadAllTextAsync(file));
            Assert.Matches("^[A-F0-9]{64}\\.json$", Path.GetFileName(file));
        }
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"version\":99}")]
    public async Task CorruptOrIncompatibleSnapshot_IsAMiss(string bytes)
    {
        await Cache().WriteAsync(new(Graph(), Fetched), default);
        await File.WriteAllTextAsync(Directory.GetFiles(_root, "*.json", SearchOption.AllDirectories).Single(), bytes);
        Assert.Null(await Cache().ReadAsync("p1", default));
    }

    [Fact]
    public async Task UnwritableRoot_DoesNotFailSuccessfulGraph()
    {
        await File.WriteAllTextAsync(_root, "not a directory");
        try
        {
            await Cache().WriteAsync(new(Graph(), Fetched), default);
            Assert.Null(await Cache().ReadAsync("p1", default));
        }
        finally { File.Delete(_root); }
    }

    [Fact]
    public async Task SimultaneousWriters_LeaveOneCompleteSnapshotAndNoTemporaryFiles()
    {
        await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Cache().WriteAsync(new(Graph() with { Project = new("p1", $"Map {i}", "url") }, Fetched.AddSeconds(i)), default)));
        var saved = await Cache().ReadAsync("p1", default);
        Assert.NotNull(saved);
        Assert.Equal($"Map {(saved.FetchedAt - Fetched).TotalSeconds}", saved.Graph.Project.Name);
        Assert.Single(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("projectId", "\"wrong\"")]
    [InlineData("snapshot.fetchedAt", "\"0001-01-01T00:00:00+00:00\"")]
    [InlineData("snapshot.fetchedAt", "\"2026-10-04T12:00:00+02:00\"")]
    [InlineData("snapshot.graph.project", "null")]
    [InlineData("snapshot.graph.issues", "null")]
    [InlineData("snapshot.graph.external", "null")]
    [InlineData("snapshot.graph.relations", "null")]
    [InlineData("snapshot.graph.project.name", "null")]
    [InlineData("snapshot.graph.project.id", "\"wrong\"")]
    public async Task InvalidEnvelope_IsAMiss(string property, string json)
    {
        await Cache().WriteAsync(new(Graph(), Fetched), default);
        var file = Directory.GetFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        var document = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllTextAsync(file))!;
        var parts = property.Split('.');
        var parent = document;
        foreach (var part in parts[..^1]) parent = parent[part]!;
        parent[parts[^1]] = System.Text.Json.Nodes.JsonNode.Parse(json);
        await File.WriteAllTextAsync(file, document.ToJsonString());
        Assert.Null(await Cache().ReadAsync("p1", default));
    }

    private static MapIssue Issue(string id = "1") => new(id, "DYD-1", "Title", "url", new("Todo", "unstarted", "#fff"), null, null, new("t", "T"), new("p1", "Map"), [new("AFK", "#fff")]);

    [Fact]
    public async Task CompleteGraph_RoundTripsNestedFieldsExternalIssuesAndRelations()
    {
        var graph = Graph() with { Issues = [Issue()], External = [Issue("2") with { Project = null }], Relations = [new("r", "blocks", "2", "1")] };
        await Cache().WriteAsync(new(graph, Fetched), default);
        var saved = await Cache().ReadAsync("p1", default);
        Assert.NotNull(saved);
        Assert.Equal("AFK", saved.Graph.Issues.Single().Labels.Single().Name);
        Assert.Null(saved.Graph.External.Single().Project);
        Assert.Equal(graph.Relations, saved.Graph.Relations);
    }

    [Fact]
    public async Task InvalidGraphs_DoNotReplaceAValidSnapshot()
    {
        var graph = Graph() with { Issues = [Issue()] };
        var invalid = new MapGraph[]
        {
            graph with { External = [Issue()] },
            graph with { Issues = [null!] },
            graph with { Issues = [Issue() with { State = null! }] },
            graph with { Issues = [Issue() with { Team = null! }] },
            graph with { Issues = [Issue() with { Labels = null! }] },
            graph with { Issues = [Issue() with { Labels = [null!] }] },
            graph with { Issues = [Issue() with { Project = new("p1", null!) }] },
            graph with { Relations = [new("r", "blocks", "1", "missing")] },
            graph with { Relations = [new("r", "unknown", "1", "1")] },
            graph with { Relations = [new("r", "blocks", "1", "1"), new("r", "related", "1", "1")] },
            graph with { Relations = [null!] }
        };
        await Cache().WriteAsync(new(graph, Fetched), default);
        foreach (var broken in invalid)
        {
            await Cache().WriteAsync(new(broken, Fetched.AddMinutes(1)), default);
            Assert.Equal(Fetched, (await Cache().ReadAsync("p1", default))!.FetchedAt);
        }
    }

    [Fact]
    public async Task CancelledWrite_PreservesTheLastSnapshotAndCleansTemporaryFiles()
    {
        await Cache().WriteAsync(new(Graph(), Fetched), default);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Cache().WriteAsync(new(Graph(), Fetched.AddMinutes(1)), cancelled.Token));
        Assert.Equal(Fetched, (await Cache().ReadAsync("p1", default))!.FetchedAt);
        Assert.Single(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }
}
