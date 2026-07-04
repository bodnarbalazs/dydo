namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>dual_property</c> config of a relation schema (DR 029 §5). A dual-property relation
/// synchronises a back-reference property onto the target data source — the reverse a rollup or a
/// derived "Blocks"/"Sprints" column reads. <see cref="SyncedPropertyName"/> names that synced
/// back-reference so the model can reference it deterministically (rollups by name).
/// </summary>
public sealed class NotionDualPropertyConfig
{
    [JsonPropertyName("synced_property_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SyncedPropertyName { get; set; }
}
