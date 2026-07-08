namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Response envelope for GET/PATCH /v1/pages/{id}/markdown (Notion's native Markdown Content API,
/// DR 035): Notion maps the page's blocks to a single markdown string server-side. Only the <c>markdown</c>
/// field is read; any surrounding envelope fields are ignored by the deserializer, so the exact wrapper shape
/// need not be modeled. An empty body reads back as the empty string, never null.
///
/// <para><b>20k-block ceiling (DR 035 caveat / brief slice 2) — deferred to the live smoke.</b> Notion caps a
/// page at ~20k blocks and truncates the markdown export past that, signalling it with an envelope flag. The
/// flag's exact wire name is NOT documented and cannot be confirmed here without live API access, so modelling
/// it now would be a guess (the one thing DR 025 §6 practice forbids — "verified, not assumed"). This DTO is
/// deliberately <b>tolerant</b>: unknown envelope fields are ignored, so once Charlie's live smoke confirms the
/// truncation field's name, surfacing it is a one-line add here plus a warn at the <c>DocsPageAdapter</c> read
/// site — never a crash. No corpus doc approaches the ceiling, so nothing regresses in the meantime.</para></summary>
public sealed class NotionMarkdownResponse
{
    [JsonPropertyName("markdown")]
    public string Markdown { get; set; } = "";
}
