namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>Where the new view lands among the database's existing views. <c>end</c> appends it after the
/// auto-created default view.</summary>
public sealed class NotionViewPosition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "end";
}
