namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A view id from the list-views response (GET /v1/views?database_id=…).</summary>
public sealed class NotionViewRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}
