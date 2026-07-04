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

    /// <summary>
    /// For a select property, the option value → repo subfolder routing (slice brief §3): a doc whose
    /// value for this property matches a key is filed under that subfolder, and an unmapped value goes to
    /// the dir root. Folder placement is derived presentation — status stays canonical in frontmatter — so
    /// the engine pools docs from every subfolder and re-files a doc when this value changes, never losing
    /// it. Absent for properties that don't route.
    /// </summary>
    [JsonPropertyName("folders")]
    public Dictionary<string, string>? Folders { get; set; }
}
