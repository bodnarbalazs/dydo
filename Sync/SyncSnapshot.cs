namespace DynaDocs.Sync;

using System.Text.Json.Serialization;

/// <summary>
/// The persisted last-synced state for one object — the "base" of the 3-way merge. Held in a
/// gitignored shadow store (never part of the canonical synced tree) so the shadow itself
/// never syncs or gets committed.
/// </summary>
public sealed class SyncSnapshot
{
    [JsonPropertyName("localId")]
    public required string LocalId { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("fields")]
    public required List<SyncFieldEntry> Fields { get; set; }

    /// <summary>Missing or zero identifies the legacy single-body snapshot shape.</summary>
    [JsonPropertyName("bodyVersion")]
    public int BodyVersion { get; set; }

    /// <summary>The legacy shared body base. Kept nullable so v2 snapshots do not fabricate v1 state.</summary>
    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("localBody")]
    public string? LocalBody { get; set; }

    [JsonPropertyName("externalBody")]
    public string? ExternalBody { get; set; }

    [JsonPropertyName("pendingBodyWrite")]
    public BodyWriteIntent? PendingBodyWrite { get; set; }
}
