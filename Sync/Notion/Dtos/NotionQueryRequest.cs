namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Body for POST /v1/data_sources/{id}/query. Pagination for the full sweep; an optional
/// <see cref="Filter"/> + <see cref="Sorts"/> for the daemon's cheap tick, which asks only for pages edited on or
/// after a cursor (ns-13). Both are null on a full query, so its serialized body is unchanged.</summary>
public sealed class NotionQueryRequest
{
    [JsonPropertyName("start_cursor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartCursor { get; set; }

    [JsonPropertyName("filter")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NotionQueryFilter? Filter { get; set; }

    [JsonPropertyName("sorts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NotionQuerySort>? Sorts { get; set; }
}
