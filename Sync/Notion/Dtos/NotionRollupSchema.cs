namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>rollup</c> config in a create/update property schema (DR 029 §5 — progress bars). A rollup
/// aggregates a property of related rows, so it references the <see cref="RelationPropertyName"/> on
/// this data source (the reverse relation a child's dual-property relation creates), the
/// <see cref="RollupPropertyName"/> to aggregate on the related rows, and the aggregation
/// <see cref="Function"/> (e.g. <c>percent_checked</c>). View-only: computed in Notion, never stored.
/// </summary>
public sealed class NotionRollupSchema
{
    [JsonPropertyName("relation_property_name")]
    public string RelationPropertyName { get; set; } = "";

    [JsonPropertyName("rollup_property_name")]
    public string RollupPropertyName { get; set; } = "";

    [JsonPropertyName("function")]
    public string Function { get; set; } = "";
}
