namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Body for POST /v1/search used to discover the accessible data source(s).</summary>
public sealed class NotionSearchRequest
{
    [JsonPropertyName("filter")]
    public NotionSearchFilter Filter { get; set; } = new();
}
