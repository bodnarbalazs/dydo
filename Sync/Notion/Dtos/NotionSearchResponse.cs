namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>POST /v1/search response (paginated, but discovery only reads the first page).</summary>
public sealed class NotionSearchResponse
{
    [JsonPropertyName("results")]
    public List<NotionSearchResult> Results { get; set; } = [];
}
