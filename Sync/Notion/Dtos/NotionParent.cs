namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The <c>parent</c> of a created page: a data source (2025-09-03 model).</summary>
public sealed class NotionParent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "data_source_id";

    [JsonPropertyName("data_source_id")]
    public string DataSourceId { get; set; } = "";
}
