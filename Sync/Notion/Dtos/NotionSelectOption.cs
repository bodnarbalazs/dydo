namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A select / multi-select option. Writing by <c>name</c> is sufficient; Notion resolves
/// it against the existing options (and rejects unknown ones — acceptable for the MVP).</summary>
public sealed class NotionSelectOption
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}
