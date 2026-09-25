namespace DynaDocs.Services.Map;

using System.Collections.Specialized;
using System.Text.Json;
using DynaDocs.Serialization;

/// <summary>
/// The `/api/*` routes of plan §3's HTTP API contract. Each call reads Linear afresh; nothing is
/// cached, so a reload of the page shows Linear's current state.
/// </summary>
internal sealed class MapApi(LinearReader linear)
{
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
                    await linear.GetGraphAsync(Required(query, "project"), ct),
                    MapJsonContext.Default.MapGraph),
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

    private static string Required(NameValueCollection query, string name) =>
        query[name] is { Length: > 0 } value ? value : throw MapApiException.MissingParameter(name);
}
