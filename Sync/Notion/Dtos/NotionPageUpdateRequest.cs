namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Body for PATCH /v1/pages/{id}: update properties and/or archive the page.</summary>
public sealed class NotionPageUpdateRequest
{
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, NotionPropertyValue>? Properties { get; set; }

    [JsonPropertyName("archived")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Archived { get; set; }
}
