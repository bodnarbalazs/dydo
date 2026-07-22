namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A sort clause for a data-source query (ns-13): the daemon's cheap tick sorts filter hits by
/// <c>last_edited_time</c> ascending, so paginated results arrive oldest-first and the cursor advances to the
/// newest stamp seen. Live-verified 2026-07-22.</summary>
public sealed class NotionQuerySort
{
    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = "last_edited_time";

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "ascending";
}
