namespace DynaDocs.Services.Map;

using System.Collections.Specialized;
using System.Collections.Concurrent;
using DynaDocs.Services.Map.Contract;
using System.Text.Json;
using DynaDocs.Serialization;

/// <summary>
/// Fresh reads and independent saved-snapshot lookups for the map viewer.
/// </summary>
internal sealed class MapApi(LinearReader linear, MapSnapshotCache? cache = null)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> projectReads = new(StringComparer.Ordinal);

    public async Task<(int Status, byte[] Body)> HandleAsync(string path, NameValueCollection query, CancellationToken ct)
    {
        try
        {
            return (200, path switch
            {
                "/api/teams" => JsonSerializer.SerializeToUtf8Bytes(
                    new() { ["teams"] = await linear.GetTeamsAsync(ct) },
                    MapJsonContext.Default.DictionaryStringListMapTeam),
                "/api/projects" => JsonSerializer.SerializeToUtf8Bytes(
                    new() { ["projects"] = await linear.GetProjectsAsync(Required(query, "team"), ct) },
                    MapJsonContext.Default.DictionaryStringListMapProject),
                "/api/graph" => JsonSerializer.SerializeToUtf8Bytes(
                    await FreshAsync(Required(query, "project"), ct),
                    MapJsonContext.Default.MapGraph),
                "/api/saved" => JsonSerializer.SerializeToUtf8Bytes(
                    new MapSavedResponse(await SavedAsync(Required(query, "project"), ct)),
                    MapJsonContext.Default.MapSavedResponse),
                _ => throw MapApiException.NotFound($"No API route {path}.")
            });
        }
        catch (MapApiException ex)
        {
            return (ex.Status, ex.Envelope());
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            // Linear answered, but not in the shape the queries ask for.
            return (502, MapApiException.LinearError($"Linear sent an unexpected response: {ex.Message}").Envelope());
        }
    }

    private async Task<MapGraph> FreshAsync(string project, CancellationToken ct)
    {
        var key = Guid.TryParse(project, out var id) ? id.ToString("D") : project;
        var gate = projectReads.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var graph = await linear.GetGraphAsync(project, ct);
            var graphId = Guid.TryParse(graph.Project.Id, out var parsed) ? parsed.ToString("D") : graph.Project.Id;
            if (graphId != key) throw new InvalidOperationException("Linear returned a different project.");
            if (cache is not null) await cache.WriteAsync(new(graph, DateTimeOffset.UtcNow), ct);
            return graph;
        }
        finally { gate.Release(); }
    }

    private Task<MapSnapshot?> SavedAsync(string project, CancellationToken ct) =>
        cache?.ReadAsync(project, ct) ?? Task.FromResult<MapSnapshot?>(null);

    private static string Required(NameValueCollection query, string name) =>
        query[name] is { Length: > 0 } value ? value : throw MapApiException.MissingParameter(name);
}
