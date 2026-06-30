namespace DynaDocs.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Optional <c>notion</c> section of dydo.json (Decision 025). Names the parent page the PM spine
/// databases are provisioned under; if unset, <c>DYDO_NOTION_PARENT_PAGE</c> is used. The integration
/// token is never stored here — it comes only from <c>DYDO_NOTION_TOKEN</c> — so this file stays safe
/// to commit.
/// </summary>
public sealed class NotionConfig
{
    [JsonPropertyName("parentPageId")]
    public string? ParentPageId { get; set; }
}
