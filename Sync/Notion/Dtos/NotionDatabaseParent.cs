namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The <c>parent</c> of a created database: the page it is nested under.</summary>
public sealed class NotionDatabaseParent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "page_id";

    [JsonPropertyName("page_id")]
    public string PageId { get; set; } = "";
}
