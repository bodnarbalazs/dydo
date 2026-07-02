namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The initial data source created alongside a database — carries the property schema map.</summary>
public sealed class NotionInitialDataSource
{
    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertySchema> Properties { get; set; } = new();
}
