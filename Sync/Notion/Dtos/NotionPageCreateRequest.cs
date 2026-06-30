namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Body for POST /v1/pages: a data-source parent, properties, and optional body blocks.</summary>
public sealed class NotionPageCreateRequest
{
    [JsonPropertyName("parent")]
    public NotionParent Parent { get; set; } = new();

    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertyValue> Properties { get; set; } = new();

    [JsonPropertyName("children")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NotionBlock>? Children { get; set; }
}
