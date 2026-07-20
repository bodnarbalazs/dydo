namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A view from the list-views response (GET /v1/views?database_id=…): its id, and its name so the
/// CreateView recovery can match an already-created view by name before re-creating (ns-5).</summary>
public sealed class NotionViewRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
