namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// A reference to a base snapshot (baseline or another session's delta) plus a delta.
/// Replaces the inline snapshot in compacted session files.
/// </summary>
public class SnapshotRef
{
    [JsonPropertyName("type")]
    public string Type => "delta";

    /// <summary>
    /// The ID of the base — either a baseline ID or another session ID.
    /// </summary>
    [JsonPropertyName("base")]
    public required string BaseId { get; set; }

    /// <summary>
    /// How deep this chain is. 1 = references a baseline directly.
    /// Max allowed: 5.
    /// </summary>
    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    /// <summary>
    /// What changed relative to the base. Null if identical to base.
    /// </summary>
    [JsonPropertyName("delta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SnapshotDelta? Delta { get; set; }
}
