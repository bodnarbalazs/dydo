namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// One block. <see cref="Type"/> selects which payload field is set; on write only that field is
/// populated and the rest are omitted (<c>WhenWritingNull</c>). Append payloads also carry the
/// constant <c>object: "block"</c> that Notion expects.
/// </summary>
public sealed class NotionBlock
{
    [JsonPropertyName("object")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Object { get; set; }

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "paragraph";

    [JsonPropertyName("paragraph")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionBlockBody? Paragraph { get; set; }

    [JsonPropertyName("heading_1")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionBlockBody? Heading1 { get; set; }

    [JsonPropertyName("heading_2")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionBlockBody? Heading2 { get; set; }

    [JsonPropertyName("heading_3")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionBlockBody? Heading3 { get; set; }

    [JsonPropertyName("bulleted_list_item")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionBlockBody? BulletedListItem { get; set; }

    [JsonPropertyName("numbered_list_item")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionBlockBody? NumberedListItem { get; set; }

    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionBlockBody? Code { get; set; }

    [JsonPropertyName("quote")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionBlockBody? Quote { get; set; }

    [JsonPropertyName("table")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionTable? Table { get; set; }

    [JsonPropertyName("table_row")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionTableRow? TableRow { get; set; }

    /// <summary>Nested block children — a uniform accessor over wherever the payload actually carries them: a
    /// text block's own body (paragraph/list item/quote, <see cref="NotionBlockBody.Children"/>) or a table's rows
    /// (<see cref="NotionTable.Children"/>). Notion nests children INSIDE the type payload, never as a top-level
    /// block field (which it rejects with a 400, ns-12 live), so this is <c>[JsonIgnore]</c> — the children
    /// serialize through the payload, and a flat block carries no top-level array. On write, Notion accepts at most
    /// two levels of nesting per request, so a payload is cut at depth 2 and deeper levels appended iteratively
    /// against the ids the API returns (<see cref="DynaDocs.Sync.Notion.NotionBlockAppender"/>). On read the setter
    /// parks a fetched child level into the active payload (the body is already deserialized, so it routes there).</summary>
    [JsonIgnore]
    public List<NotionBlock>? Children
    {
        get => Body?.Children ?? Table?.Children;
        set
        {
            if (Body is { } body)
                body.Children = value;
            else if (Table is { } table)
                table.Children = value;
        }
    }

    /// <summary>The active text payload — exactly one is non-null on a text-bearing block, so coalescing selects it
    /// without consulting <see cref="Type"/>. Null for tables, table rows and child pages.</summary>
    private NotionBlockBody? Body =>
        Paragraph ?? Heading1 ?? Heading2 ?? Heading3 ?? BulletedListItem ?? NumberedListItem ?? Code ?? Quote;

    /// <summary>Read-only flag Notion sets on a block that has nested children. GetBlockChildren returns one level
    /// at a time (never inlining descendants), so the reader recurses into a block's children only when this is
    /// true — a flat body then costs no extra reads. Never sent on write.</summary>
    [JsonPropertyName("has_children")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasChildren { get; set; }

    /// <summary>Set on a <c>child_page</c> block Notion returns for a nested sub-page (DR 033). Read-only:
    /// child pages are created via the pages endpoint, never appended as blocks.</summary>
    [JsonPropertyName("child_page")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionChildPageBody? ChildPage { get; set; }
}
