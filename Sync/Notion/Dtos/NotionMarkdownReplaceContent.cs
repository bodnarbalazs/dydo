namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The <c>replace_content</c> command body nested inside a PATCH /v1/pages/{id}/markdown request
/// (Notion's native Markdown Content API, DR 035). That endpoint is a DISCRIMINATED command, not a flat markdown
/// string: the new body is <c>new_str</c> (NOT <c>markdown</c>) and <see cref="AllowDeletingContent"/> nests HERE,
/// inside the command object — not at the top level. <c>allow_deleting_content</c> gates a destructive full
/// overwrite (a body replace removes the page's existing blocks); the docs mirror sends it <c>false</c> for a page
/// that still carries child pages so replacing the body never trashes the nested docs
/// (makenotion/notion-mcp-server#171), <c>true</c> only for a leaf page. Newlines must be real <c>\n</c> in the
/// serialized JSON string, which source-generated System.Text.Json emits by default.</summary>
public sealed class NotionMarkdownReplaceContent
{
    [JsonPropertyName("new_str")]
    public string NewStr { get; set; } = "";

    [JsonPropertyName("allow_deleting_content")]
    public bool AllowDeletingContent { get; set; }
}
