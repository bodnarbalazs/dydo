namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Body for PATCH /v1/pages/{id}/markdown (Notion's native Markdown Content API, DR 035): a single
/// markdown string replacing the page body. <see cref="AllowDeletingContent"/> is required for a destructive
/// update — a body replace removes the page's existing blocks — so the adapter always sends it true. Newlines
/// must be real <c>\n</c> in the serialized JSON string (never rendered blocks), which source-generated
/// System.Text.Json emits by default.</summary>
public sealed class NotionMarkdownUpdateRequest
{
    [JsonPropertyName("markdown")]
    public string Markdown { get; set; } = "";

    [JsonPropertyName("allow_deleting_content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AllowDeletingContent { get; set; }
}
