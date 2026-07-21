namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// Body for PATCH /v1/data_sources/{id}: adds or updates a data source's property schema after the
/// database exists. Used for the self-relation second pass — a relation whose target is its own type
/// cannot be declared at create time because the type's data source id is not known until creation
/// returns — and for the additive model-evolution pass (ns-11), which also renames the data source via
/// <see cref="Title"/> when a type's <c>notionTitle</c> changed. Only the <c>properties</c>/<c>title</c>
/// sent are touched; Notion leaves the rest untouched.
/// </summary>
public sealed class NotionDataSourceUpdateRequest
{
    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertySchema> Properties { get; set; } = new();

    /// <summary>The new data source title, sent only for a rename (ns-11) — omitted otherwise so a
    /// schema-only PATCH never touches the title. Same rich-text shape as the create-database title.</summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NotionRichText>? Title { get; set; }
}
