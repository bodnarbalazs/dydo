namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// Subset of GET /v1/data_sources/{id} we read: the data source's live property schema by name (the
/// schema-drift check, DR 029 §6) and its live title (<see cref="Name"/>, the additive pass's rename
/// seed). Each schema entry deserializes into a <see cref="NotionPropertySchema"/> — the same shape used
/// on write — so a select's live options round-trip through <c>select.options[].name</c>.
/// </summary>
public sealed class NotionDataSource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    /// <summary>The data source's live title, as the retrieve response carries it (the same <c>name</c> key the
    /// database's <c>data_sources[]</c> refs and search hits use). Read by the additive pass to seed a pre-ns-11
    /// record's title from what the board ACTUALLY shows, so a model title that changed before the first
    /// post-upgrade sync is still detected as a rename (F1). Null when absent on the wire — an ns-10 wire-shape
    /// check — in which case the pass degrades to seeding from the model.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, NotionPropertySchema> Properties { get; set; } = new();
}
