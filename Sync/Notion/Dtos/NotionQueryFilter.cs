namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>The timestamp filter for POST /v1/data_sources/{id}/query (ns-13): restrict a query to pages whose
/// <c>last_edited_time</c> is on or after a cursor, so the sync daemon's cheap tick fetches only the pages that
/// changed since it last looked — O(changes), never O(corpus). Live-verified 2026-07-22.</summary>
public sealed class NotionQueryFilter
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "last_edited_time";

    [JsonPropertyName("last_edited_time")]
    public NotionTimestampBound LastEditedTime { get; set; } = new();
}
