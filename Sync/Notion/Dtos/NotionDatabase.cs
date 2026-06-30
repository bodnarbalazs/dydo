namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Subset of GET /v1/databases/{id} we use: the database's data sources.</summary>
public sealed class NotionDatabase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("data_sources")]
    public List<NotionDataSourceRef> DataSources { get; set; } = [];
}
