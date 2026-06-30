namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// The inner payload shared by the text-bearing block types we handle (paragraph, headings,
/// bulleted list item, code): a rich-text array, plus an optional <c>language</c> for code.
/// </summary>
public sealed class NotionBlockBody
{
    [JsonPropertyName("rich_text")]
    public List<NotionRichText> RichText { get; set; } = [];

    [JsonPropertyName("language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }
}
