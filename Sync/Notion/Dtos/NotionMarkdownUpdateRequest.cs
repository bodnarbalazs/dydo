namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Body for PATCH /v1/pages/{id}/markdown (Notion's native Markdown Content API, DR 035). The endpoint is
/// a DISCRIMINATED command keyed by <c>type</c>, NOT a flat <c>{ markdown, allow_deleting_content }</c> object —
/// sending a bare markdown string is rejected with <c>body.type should be defined</c>. This models the
/// <c>replace_content</c> full-overwrite variant: the new body and its destructive-update flag live inside the
/// nested <see cref="NotionMarkdownReplaceContent"/> command (the markdown field is <c>new_str</c>, and
/// <c>allow_deleting_content</c> nests inside it, not at the top level).</summary>
public sealed class NotionMarkdownUpdateRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "replace_content";

    [JsonPropertyName("replace_content")]
    public NotionMarkdownReplaceContent ReplaceContent { get; set; } = new();
}
