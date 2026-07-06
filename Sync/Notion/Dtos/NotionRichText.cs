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

    /// <summary>Notion rejects a rich-text run whose content exceeds this many characters with a 400. A
    /// block (or a title/rich_text property) may carry many runs, and <see cref="Flatten"/> concatenates
    /// them, so splitting a long string across runs is transparent to the round-trip.</summary>
    private const int MaxRunLength = 2000;

    /// <summary>Build a rich-text list for a plain string (write side), split into runs no longer than
    /// Notion's per-run cap so a long paragraph or code block is not rejected. A surrogate pair is never
    /// split across runs.</summary>
    public static List<NotionRichText> Of(string content)
    {
        if (content.Length <= MaxRunLength)
            return [new NotionRichText { Type = "text", Text = new NotionText { Content = content } }];

        var runs = new List<NotionRichText>();
        for (var i = 0; i < content.Length;)
        {
            var len = Math.Min(MaxRunLength, content.Length - i);
            if (i + len < content.Length && char.IsHighSurrogate(content[i + len - 1]))
                len--; // keep the surrogate pair together in the next run
            runs.Add(new NotionRichText { Type = "text", Text = new NotionText { Content = content.Substring(i, len) } });
            i += len;
        }
        return runs;
    }

    /// <summary>Flatten a rich-text array to plain text (read side).</summary>
    public static string Flatten(List<NotionRichText>? runs) =>
        runs == null ? "" : string.Concat(runs.Select(r => r.PlainText ?? r.Text?.Content ?? ""));
}
