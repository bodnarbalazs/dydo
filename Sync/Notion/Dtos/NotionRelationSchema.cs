namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>relation</c> config in a create-database property schema. Confirmed empirically: the
/// relation references the target's <c>data_source_id</c> (not its database id). <c>single_property</c>
/// marks a one-directional relation (no synced back-reference property on the target);
/// <c>dual_property</c> synchronises a reverse property onto the target (DR 029 §5) — required wherever
/// a rollup or a derived "Blocks"/"Sprints" column needs to read the reverse. Exactly one of
/// <see cref="SingleProperty"/> / <see cref="DualProperty"/> is set, matching <see cref="Type"/>.
/// </summary>
public sealed class NotionRelationSchema
{
    [JsonPropertyName("data_source_id")]
    public string DataSourceId { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "single_property";

    [JsonPropertyName("single_property")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionEmptyConfig? SingleProperty { get; set; } = new();

    [JsonPropertyName("dual_property")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionDualPropertyConfig? DualProperty { get; set; }
}
