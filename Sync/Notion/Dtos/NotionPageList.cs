namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A paginated list of pages (data-source query response).</summary>
public sealed class NotionPageList
{
    [JsonPropertyName("results")]
    public List<NotionPage> Results { get; set; } = [];

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }
}
