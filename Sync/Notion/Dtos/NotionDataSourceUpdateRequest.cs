namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// Body for PATCH /v1/data_sources/{id}: adds or updates a data source's property schema after the
/// database exists. Used for the self-relation second pass — a relation whose target is its own type
/// cannot be declared at create time because the type's data source id is not known until creation
/// returns. Only the <c>properties</c> to add/change are sent; Notion leaves the rest untouched.
/// </summary>
public sealed class NotionDataSourceUpdateRequest
{
    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertySchema> Properties { get; set; } = new();
}
