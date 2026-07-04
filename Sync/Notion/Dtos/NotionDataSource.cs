namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// Subset of GET /v1/data_sources/{id} the schema-drift check reads (DR 029 §6): the data source's
/// live property schema by name. Each entry deserializes into a <see cref="NotionPropertySchema"/> —
/// the same shape used on write — so a select's live options round-trip through
/// <c>select.options[].name</c>; unrelated read-only fields (id, name, type) are ignored.
/// </summary>
public sealed class NotionDataSource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertySchema> Properties { get; set; } = new();
}
