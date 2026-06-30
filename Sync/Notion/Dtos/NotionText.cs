namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The <c>{ "content": "..." }</c> payload of a rich-text node's <c>text</c> field.</summary>
public sealed class NotionText
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}
