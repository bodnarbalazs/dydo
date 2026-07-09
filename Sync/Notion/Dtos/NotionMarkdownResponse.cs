namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Response envelope for GET/PATCH /v1/pages/{id}/markdown (Notion's native Markdown Content API,
/// DR 035): <c>{ object: "page_markdown", id, markdown, truncated, unknown_block_ids }</c>. Notion maps the page's
/// blocks to a single markdown string server-side. An empty body reads back as the empty string, never null.
///
/// <para><see cref="Truncated"/> is Notion's ~20k-block export ceiling signal (DR 035 caveat): past the ceiling the
/// exported markdown is CUT SHORT. A truncated read is therefore SHORTER than the real body, so the docs mirror
/// must never treat it as external state — merging it back would look like a Notion-side deletion and truncate the
/// canonical repo file. The read site (<c>DocsPageAdapter.ReadPage</c>) warns and reuses the last-synced body
/// instead of the truncated one. <see cref="UnknownBlockIds"/> lists blocks Notion could not render to markdown,
/// surfaced for diagnostics.</para></summary>
public sealed class NotionMarkdownResponse
{
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("markdown")]
    public string Markdown { get; set; } = "";

    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }

    [JsonPropertyName("unknown_block_ids")]
    public List<string>? UnknownBlockIds { get; set; }
}
