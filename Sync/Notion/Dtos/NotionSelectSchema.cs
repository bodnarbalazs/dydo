namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The <c>select</c> config in a create-database property schema: its options by name.</summary>
public sealed class NotionSelectSchema
{
    [JsonPropertyName("options")]
    public List<NotionSelectOption> Options { get; set; } = [];
}
