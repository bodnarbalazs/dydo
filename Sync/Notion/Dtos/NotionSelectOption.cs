namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A select / multi-select option. Writing by <c>name</c> is sufficient; Notion resolves
/// it against the existing options (and rejects unknown ones — acceptable for the MVP). In a
/// create/update <em>schema</em> the option also carries its palette <see cref="Color"/> (DR 029's
/// color language); a page's property <em>value</em> writes only the name, so the color is omitted
/// when null.</summary>
public sealed class NotionSelectOption
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; set; }
}
