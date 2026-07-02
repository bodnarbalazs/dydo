namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// A page or database emoji icon: <c>{ "type": "emoji", "emoji": "🚀" }</c>. Purely presentational —
/// it gives Notion rows and databases a glanceable icon instead of the blank default.
/// </summary>
public sealed class NotionIcon
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "emoji";

    [JsonPropertyName("emoji")]
    public string Emoji { get; set; } = "";

    /// <summary>An icon for the emoji, or null (omitted on write) when the model declares none.</summary>
    public static NotionIcon? Of(string? emoji) =>
        string.IsNullOrEmpty(emoji) ? null : new NotionIcon { Emoji = emoji };
}
