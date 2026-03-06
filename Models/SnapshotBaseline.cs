namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// A full project snapshot stored as a baseline for delta compression.
/// Lives alongside session files as _baseline-{id}.json in the audit year folder.
/// </summary>
public class SnapshotBaseline
{
    [JsonPropertyName("type")]
    public string Type => "baseline";

    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("created")]
    public DateTime Created { get; set; }

    [JsonPropertyName("snapshot")]
    public required ProjectSnapshot Snapshot { get; set; }
}
