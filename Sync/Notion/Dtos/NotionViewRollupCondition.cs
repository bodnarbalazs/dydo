namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A condition on a rollup column, keyed by the aggregated value's type. Only a <see cref="Number"/>
/// aggregation is needed so far (the checked-count rollup on the parent types).</summary>
public sealed class NotionViewRollupCondition
{
    [JsonPropertyName("number")]
    public NotionViewNumberCondition? Number { get; set; }
}
