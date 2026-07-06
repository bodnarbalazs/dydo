namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>One entry in a view's <c>sorts</c> array: the property (by name — Notion accepts the name here)
/// and a direction.</summary>
public sealed class NotionViewSortBody
{
    [JsonPropertyName("property")]
    public string Property { get; set; } = "";

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "ascending";
}
