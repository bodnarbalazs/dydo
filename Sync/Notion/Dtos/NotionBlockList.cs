namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A paginated list of blocks (GET /v1/blocks/{id}/children response).</summary>
public sealed class NotionBlockList
{
    [JsonPropertyName("results")]
    public List<NotionBlock> Results { get; set; } = [];

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }

    [JsonPropertyName("next_cursor")]
    public string? NextCursor { get; set; }
}
