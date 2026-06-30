namespace DynaDocs.Sync.Notion.Dtos;

using System.Text.Json.Serialization;

/// <summary>
/// One rich-text run. On read, Notion always populates <see cref="PlainText"/>; on write we send
/// the minimal <c>{ type: "text", text: { content } }</c> form, which Notion accepts.
/// </summary>
public sealed class NotionRichText
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public NotionText? Text { get; set; }

    [JsonPropertyName("plain_text")]
    public string? PlainText { get; set; }

    /// <summary>Build a single-run rich-text list for a plain string (write side).</summary>
    public static List<NotionRichText> Of(string content) =>
        [new NotionRichText { Type = "text", Text = new NotionText { Content = content } }];

    /// <summary>Flatten a rich-text array to plain text (read side).</summary>
    public static string Flatten(List<NotionRichText>? runs) =>
        runs == null ? "" : string.Concat(runs.Select(r => r.PlainText ?? r.Text?.Content ?? ""));
}
