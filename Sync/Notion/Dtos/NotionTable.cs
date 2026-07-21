namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// The payload of a <c>table</c> block: its fixed column count and header flags, plus the row blocks.
/// On write the rows nest here (Notion carries <c>table_row</c> children inside the table object, not at
/// the block level); on read Notion returns them via GetBlockChildren, so the reader parks them on the
/// block's generic <see cref="NotionBlock.Children"/> and the renderer reads whichever is populated.
/// </summary>
public sealed class NotionTable
{
    [JsonPropertyName("table_width")]
    public int TableWidth { get; set; }

    [JsonPropertyName("has_column_header")]
    public bool HasColumnHeader { get; set; }

    [JsonPropertyName("has_row_header")]
    public bool HasRowHeader { get; set; }

    [JsonPropertyName("children")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NotionBlock>? Children { get; set; }
}
