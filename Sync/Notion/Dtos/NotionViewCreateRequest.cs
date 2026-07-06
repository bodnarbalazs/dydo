namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The <c>POST /v1/views</c> body (board-views feature). Creates one view on an existing database:
/// <see cref="DatabaseId"/> locates the database, <see cref="DataSourceId"/> the collection it scopes to,
/// and <see cref="Type"/> is the view kind (<c>table</c>|<c>board</c>|<c>timeline</c>). The optional
/// <see cref="Filter"/>/<see cref="Sorts"/> shape the rows; <see cref="Configuration"/> carries the
/// type-specific layout (column order + visibility, board grouping, timeline date axis).</summary>
public sealed class NotionViewCreateRequest
{
    [JsonPropertyName("database_id")]
    public string DatabaseId { get; set; } = "";

    [JsonPropertyName("data_source_id")]
    public string DataSourceId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "table";

    [JsonPropertyName("filter")]
    public NotionViewFilterBody? Filter { get; set; }

    [JsonPropertyName("sorts")]
    public List<NotionViewSortBody>? Sorts { get; set; }

    [JsonPropertyName("configuration")]
    public NotionViewConfiguration? Configuration { get; set; }

    [JsonPropertyName("position")]
    public NotionViewPosition Position { get; set; } = new();
}
