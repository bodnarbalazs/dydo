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

    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionBlockBody? Code { get; set; }
}
