namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A view's type-specific layout. <see cref="Properties"/> is the ordered column list with per-column
/// visibility (the one place display order + hide-compute-only is expressed). <see cref="GroupBy"/> is set only
/// for a board; <see cref="DatePropertyId"/>/<see cref="EndDatePropertyId"/> only for a timeline. Null fields
/// are omitted, so a table sends just its properties.</summary>
public sealed class NotionViewConfiguration
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "table";

    [JsonPropertyName("properties")]
    public List<NotionViewPropertyRef>? Properties { get; set; }

    [JsonPropertyName("group_by")]
    public NotionViewGroupBy? GroupBy { get; set; }

    [JsonPropertyName("date_property_id")]
    public string? DatePropertyId { get; set; }

    [JsonPropertyName("end_date_property_id")]
    public string? EndDatePropertyId { get; set; }
}
