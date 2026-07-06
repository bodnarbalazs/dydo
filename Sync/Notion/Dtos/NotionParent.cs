namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The <c>parent</c> of a created page: a data source (a DB row, 2025-09-03 model) or another
/// page (a nested page, DR 033). <see cref="Type"/> selects which id field is serialized; the other is
/// null and omitted (<c>WhenWritingNull</c>). Use the factories rather than setting both by hand.</summary>
public sealed class NotionParent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "data_source_id";

    [JsonPropertyName("data_source_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataSourceId { get; set; }

    [JsonPropertyName("page_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PageId { get; set; }

    /// <summary>A page created as a row of a data source (the PM spine).</summary>
    public static NotionParent DataSource(string dataSourceId) =>
        new() { Type = "data_source_id", DataSourceId = dataSourceId };

    /// <summary>A page nested under another page (the docs mirror, DR 033).</summary>
    public static NotionParent Page(string pageId) =>
        new() { Type = "page_id", PageId = pageId };
}
