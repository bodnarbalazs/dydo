namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The payload of a <c>child_page</c> block: a sub-page's title (a plain string, not rich
/// text). The block's own id is the child page's id, so enumerating a page's <c>child_page</c> blocks
/// is how the docs mirror walks the nested-page tree (DR 033 §3).</summary>
public sealed class NotionChildPageBody
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
}
