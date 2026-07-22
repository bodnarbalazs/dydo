namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The <c>on_or_after</c> bound of a <see cref="NotionQueryFilter"/> (ns-13). Inclusive by design:
/// Notion stamps are minute-granular, so an edit landing in the cursor's own minute shares its stamp — re-checking
/// a boundary page is harmless, missing one is not.</summary>
public sealed class NotionTimestampBound
{
    [JsonPropertyName("on_or_after")]
    public string OnOrAfter { get; set; } = "";
}
