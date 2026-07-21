namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A view from the list-views response (GET /v1/views?database_id=…), which returns ONLY <c>{object, id}</c>
/// per view — no name (ns-12 live). To match a view by name the CreateView recovery must retrieve each id via
/// <see cref="NotionView"/> (GET /v1/views/{id}).</summary>
public sealed class NotionViewRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}
