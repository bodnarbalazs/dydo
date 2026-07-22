namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// Subset of GET /v1/data_sources/{id} we read: the data source's live property schema by name (the
/// schema-drift check, DR 029 §6) and its live title (<see cref="Title"/>/<see cref="Name"/>, the additive
/// pass's rename seed). Each schema entry deserializes into a <see cref="NotionPropertySchema"/> — the same
/// shape used on write — so a select's live options round-trip through <c>select.options[].name</c>.
/// </summary>
public sealed class NotionDataSource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>The data source's live title. A database's <c>data_sources[]</c> ref carries it as a flat
    /// <c>name</c> string, but a SEARCH hit carries a rich-text <c>title</c> array (ns-12 live: a search hit
    /// carries no <c>name</c> key — see <see cref="NotionSearchResult"/>), and the FULL retrieve (GET
    /// /v1/data_sources/{id}) likewise carries it as a rich-text array under <c>title</c> — confirmed live
    /// (ns-12, 2026-07-21). A wrong key here is why the F1 seed was dormant.</summary>
    [JsonPropertyName("title")]
    public List<NotionRichText>? Title { get; set; }

    /// <summary>The flattened live title, or null when the retrieve carries none. Read by the additive pass to
    /// seed a pre-ns-11 record's title from what the board ACTUALLY shows, so a model title that changed before
    /// the first post-upgrade sync is still detected as a rename (F1). Null (not empty) so the seed's
    /// <c>live.Name ?? type.NotionTitle</c> degrades to the model when the wire carries no title.</summary>
    [JsonIgnore]
    public string? Name => Title is { Count: > 0 } ? NotionRichText.Flatten(Title) : null;

    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertySchema> Properties { get; set; } = new();
}
