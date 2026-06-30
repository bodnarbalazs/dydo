namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>A date property value. The MVP maps only the <c>start</c> bound to/from a string.</summary>
public sealed class NotionDate
{
    [JsonPropertyName("start")]
    public string? Start { get; set; }
}
