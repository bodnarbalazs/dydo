namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A page returned by a data-source query or page create/update.</summary>
public sealed class NotionPage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    /// <summary>Notion's server-side last-edit timestamp (ISO-8601, minute-granular). Present on every page a
    /// data-source query returns; the sync daemon's cheap-tick pre-filter keys off it (ns-13) — a page whose
    /// stamp has not advanced past the stored cursor is unchanged and never has its body re-read.</summary>
    [JsonPropertyName("last_edited_time")]
    public string? LastEditedTime { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertyValue> Properties { get; set; } = new();
}
