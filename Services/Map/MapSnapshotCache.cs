namespace DynaDocs.Services.Map;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DynaDocs.Serialization;
using DynaDocs.Services.Map.Contract;

/// <summary>Disposable, account-scoped snapshots. Cache failures are misses; only complete files are committed.</summary>
internal sealed class MapSnapshotCache(Uri endpoint, string apiKey, string? root = null)
{
    private const int Version = 1;
    private readonly string directory = Path.Combine(
        root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dydo", "map"),
        Hash(JsonSerializer.Serialize(new[] { endpoint.AbsoluteUri, apiKey }, MapJsonContext.Default.StringArray)));

    public async Task<MapSnapshot?> ReadAsync(string projectId, CancellationToken ct)
    {
        try
        {
            await using var stream = File.OpenRead(FileName(projectId));
            var envelope = await JsonSerializer.DeserializeAsync(stream, MapJsonContext.Default.MapSnapshotEnvelope, ct);
            return Valid(envelope, projectId) ? envelope!.Snapshot : null;
        }
        catch (Exception ex) when (CacheFailure(ex)) { return null; }
    }

    public async Task WriteAsync(MapSnapshot snapshot, CancellationToken ct)
    {
        var envelope = new MapSnapshotEnvelope(Version, Canonical(snapshot.Graph.Project.Id), snapshot);
        if (!Valid(envelope, envelope.ProjectId)) return;
        var destination = FileName(envelope.ProjectId);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(directory);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, envelope, MapJsonContext.Default.MapSnapshotEnvelope, ct);
                await stream.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        catch (Exception ex) when (CacheFailure(ex)) { }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (CacheFailure(ex)) { }
        }
    }

    private string FileName(string projectId) => Path.Combine(directory, Hash(Canonical(projectId)) + ".json");
    private static string Canonical(string id) => Guid.TryParse(id, out var guid) ? guid.ToString("D") : id;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool CacheFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException;

    private static bool Valid(MapSnapshotEnvelope? envelope, string projectId)
    {
        if (envelope is not { Version: Version, Snapshot: { Graph: { } graph } snapshot }) return false;
        if (envelope.ProjectId != Canonical(projectId) || snapshot.FetchedAt == default || snapshot.FetchedAt.Offset != TimeSpan.Zero) return false;
        if (graph.Project is not { Id: { } id, Name: not null, Url: not null } || Canonical(id) != envelope.ProjectId) return false;
        return ValidGraph(graph);
    }

    private static bool ValidGraph(MapGraph graph)
    {
        if (graph.Issues is null || graph.External is null || graph.Relations is null) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!graph.Issues.Concat(graph.External).All(issue => ValidIssue(issue) && ids.Add(issue.Id))) return false;
        var relations = new HashSet<string>(StringComparer.Ordinal);
        return graph.Relations.All(relation => relation is { Id: not null, Type: "blocks" or "related", From: not null, To: not null }
            && relations.Add(relation.Id) && ids.Contains(relation.From) && ids.Contains(relation.To));
    }

    private static bool ValidIssue(MapIssue? issue) =>
        issue is { Id: not null, Identifier: not null, Title: not null, Url: not null,
            State: { Name: not null, Type: not null, Color: not null }, Team: { Id: not null, Key: not null }, Labels: not null }
        && (issue.Project is null or { Id: not null, Name: not null })
        && issue.Labels.All(label => label is { Name: not null, Color: not null });
}
