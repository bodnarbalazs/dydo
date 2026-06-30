namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Body for POST /v1/data_sources/{id}/query — only pagination is needed for the MVP.</summary>
public sealed class NotionQueryRequest
{
    [JsonPropertyName("start_cursor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartCursor { get; set; }
}
