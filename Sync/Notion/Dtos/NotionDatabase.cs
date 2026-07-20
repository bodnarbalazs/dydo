namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Subset of GET /v1/databases/{id} we use: the database's data sources and its soft-delete
/// state. A database moved to Notion trash still 200s on retrieval carrying <c>in_trash: true</c> (it does
/// not 404), so the provisioner reads these to detect a trashed board and re-mint (ns-3).</summary>
public sealed class NotionDatabase
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("data_sources")]
    public List<NotionDataSourceRef> DataSources { get; set; } = [];

    [JsonPropertyName("in_trash")]
    public bool InTrash { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }
}
