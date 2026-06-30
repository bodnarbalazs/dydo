namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A page returned by a data-source query or page create/update.</summary>
public sealed class NotionPage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertyValue> Properties { get; set; } = new();
}
