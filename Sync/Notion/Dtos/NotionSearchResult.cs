namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>One search hit. Data-source discovery reads the id; the CreateDatabase recovery (ns-5) also
/// matches on <see cref="Name"/> and adopts the owning database from <see cref="Parent"/>.</summary>
public sealed class NotionSearchResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("object")]
    public string? Object { get; set; }

    /// <summary>The data source's title — a rich-text ARRAY on the wire (ns-12 live: a search hit carries no
    /// <c>name</c> key), flattened via <see cref="Name"/> to match against a type's Notion title and recover a
    /// database whose create response was lost to an ambiguous failure.</summary>
    [JsonPropertyName("title")]
    public List<NotionRichText>? Title { get; set; }

    /// <summary>The hit's title as plain text — the recovery's match key.</summary>
    [JsonIgnore]
    public string Name => NotionRichText.Flatten(Title);

    /// <summary>The hit's parent — for a data source, the database that owns it (carrying the database id the
    /// recovery adopts).</summary>
    [JsonPropertyName("parent")]
    public NotionParent? Parent { get; set; }
}
