namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>How a board's groups are ordered. <c>manual</c> keeps the property's own option order.</summary>
public sealed class NotionViewGroupSort
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "manual";
}
