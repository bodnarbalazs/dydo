namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The payload of a <c>table_row</c> block: one rich-text array per cell.</summary>
public sealed class NotionTableRow
{
    [JsonPropertyName("cells")]
    public List<List<NotionRichText>> Cells { get; set; } = [];
}
