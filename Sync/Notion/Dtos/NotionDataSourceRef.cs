namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>One entry of a database's <c>data_sources[]</c> (the 2025-09-03 data-source model).</summary>
public sealed class NotionDataSourceRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
