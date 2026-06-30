namespace DynaDocs.Sync.Model;

using System.Text.Json.Serialization;

/// <summary>
/// One property of a sync-model object type (slice brief §1): its <see cref="Type"/>
/// (title|select|number|date|rich_text|relation), the <see cref="Options"/> a select offers, and
/// for a relation the <see cref="To"/> object type it points at. View-agnostic — how the property is
/// realised in Notion (or any other view) lives in that view's adapter, never here.
/// </summary>
public sealed class SyncPropertyDef
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("options")]
    public List<string>? Options { get; set; }

    [JsonPropertyName("to")]
    public string? To { get; set; }
}
