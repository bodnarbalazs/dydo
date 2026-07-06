namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A single-property view filter on the wire: the <see cref="Property"/> (by name) plus exactly one
/// type-matched condition — <see cref="Select"/> for a select column, <see cref="Checkbox"/> for a checkbox,
/// <see cref="Rollup"/> for a rollup count. (Formula columns are not filterable via Notion's API — "formula of
/// unknown type" — so no formula condition exists.) The provisioner picks which to set from the live schema's
/// property type; the unset ones are omitted.</summary>
public sealed class NotionViewFilterBody
{
    [JsonPropertyName("property")]
    public string Property { get; set; } = "";

    [JsonPropertyName("select")]
    public NotionViewTextCondition? Select { get; set; }

    [JsonPropertyName("checkbox")]
    public NotionViewCheckboxCondition? Checkbox { get; set; }

    [JsonPropertyName("rollup")]
    public NotionViewRollupCondition? Rollup { get; set; }
}
