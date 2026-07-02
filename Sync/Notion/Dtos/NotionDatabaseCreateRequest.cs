namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// Body for POST /v1/databases (data-source model, 2025-09-03+): a page parent, a rich-text title, and
/// the property schema nested under <c>initial_data_source.properties</c> — NOT a top-level
/// <c>properties</c> map, which the data-source API silently ignores (yielding a default "Name"-only
/// data source). The response is a <see cref="NotionDatabase"/> whose <c>data_sources[0].id</c> is the
/// data source id used thereafter for query / create-page / relations.
/// </summary>
public sealed class NotionDatabaseCreateRequest
{
    [JsonPropertyName("parent")]
    public NotionDatabaseParent Parent { get; set; } = new();

    [JsonPropertyName("title")]
    public List<NotionRichText> Title { get; set; } = [];

    [JsonPropertyName("icon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionIcon? Icon { get; set; }

    [JsonPropertyName("initial_data_source")]
    public NotionInitialDataSource InitialDataSource { get; set; } = new();
}

/// <summary>The initial data source created alongside a database — carries the property schema map.</summary>
public sealed class NotionInitialDataSource
{
    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertySchema> Properties { get; set; } = new();
}
