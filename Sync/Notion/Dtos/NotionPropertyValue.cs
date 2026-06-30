namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// One Notion page property value, in a single shape that covers the MVP property types
/// (Decision 025 §6). On read, <see cref="Type"/> tells which field is populated; on write we set
/// only the field matching the target property's type and rely on <c>WhenWritingNull</c> to omit
/// the rest. Unknown types are read best-effort and skipped on write — see <c>NotionPropertyMapper</c>.
/// </summary>
public sealed class NotionPropertyValue
{
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NotionRichText>? Title { get; set; }

    [JsonPropertyName("rich_text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NotionRichText>? RichText { get; set; }

    [JsonPropertyName("select")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionSelectOption? Select { get; set; }

    [JsonPropertyName("multi_select")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NotionSelectOption>? MultiSelect { get; set; }

    [JsonPropertyName("number")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Number { get; set; }

    [JsonPropertyName("checkbox")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Checkbox { get; set; }

    [JsonPropertyName("date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionDate? Date { get; set; }

    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }

    [JsonPropertyName("relation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NotionRelationRef>? Relation { get; set; }
}
