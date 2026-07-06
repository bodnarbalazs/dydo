namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// One entry of a create-database <c>properties</c> map — the property's <em>schema</em> (its type
/// definition), distinct from a page's property <em>value</em>. Exactly one field is set per entry;
/// <c>WhenWritingNull</c> omits the rest so the serialized shape is e.g. <c>{ "select": { "options": […] } }</c>.
/// </summary>
public sealed class NotionPropertySchema
{
    /// <summary>The property's Notion id, populated on read (GET /v1/data_sources) and omitted on write. A
    /// view's column/group/timeline config references properties by id, so provisioning views resolves each
    /// model property name to this id from the live schema.</summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionEmptyConfig? Title { get; set; }

    [JsonPropertyName("rich_text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionEmptyConfig? RichText { get; set; }

    [JsonPropertyName("select")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionSelectSchema? Select { get; set; }

    [JsonPropertyName("number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionEmptyConfig? Number { get; set; }

    [JsonPropertyName("date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionEmptyConfig? Date { get; set; }

    [JsonPropertyName("checkbox")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionEmptyConfig? Checkbox { get; set; }

    [JsonPropertyName("relation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionRelationSchema? Relation { get; set; }

    [JsonPropertyName("formula")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionFormulaSchema? Formula { get; set; }

    [JsonPropertyName("rollup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionRollupSchema? Rollup { get; set; }
}
