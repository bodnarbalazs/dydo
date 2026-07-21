namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// The inner payload shared by the text-bearing block types we handle (paragraph, headings,
/// bulleted list item, code, quote): a rich-text array, plus an optional <c>language</c> for code and
/// optional nested <c>children</c>.
/// </summary>
public sealed class NotionBlockBody
{
    [JsonPropertyName("rich_text")]
    public List<NotionRichText> RichText { get; set; } = [];

    [JsonPropertyName("language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }

    /// <summary>Nested block children. Notion requires a block's children INSIDE its type payload
    /// (<c>{"type":"bulleted_list_item","bulleted_list_item":{"rich_text":[…],"children":[…]}}</c>) — never as a
    /// top-level block field, which it rejects with a 400 (ns-12 live). List items, paragraphs and quotes carry
    /// them here; a flat body omits the array (<c>WhenWritingNull</c>). Reached uniformly via
    /// <see cref="NotionBlock.Children"/>. Never populated on read — Notion returns children one level at a time
    /// via GetBlockChildren, flagged by <c>has_children</c>, never inlined.</summary>
    [JsonPropertyName("children")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NotionBlock>? Children { get; set; }
}
