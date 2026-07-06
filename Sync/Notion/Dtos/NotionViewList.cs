namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The list-views response: the database's views (GET /v1/views?database_id=…). Used to find the
/// auto-created default view so provisioning can remove it, leaving only the declared views.</summary>
public sealed class NotionViewList
{
    [JsonPropertyName("results")]
    public List<NotionViewRef> Results { get; set; } = [];
}
