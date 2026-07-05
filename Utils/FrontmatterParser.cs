namespace DynaDocs.Utils;

/// <summary>
/// Shared YAML frontmatter extraction. Replaces duplicate parsing logic
/// across FrontmatterExtractor, InboxItemParser, InboxMetadataReader,
/// MessageFinder, AgentStateStore, WatchdogService, DispatchService,
/// GuardCommand, and AgentRegistry.
/// </summary>
public static class FrontmatterParser
{
    /// <summary>
    /// Extracts YAML frontmatter key-value pairs from Markdown content.
    /// Returns null if the content has no valid frontmatter block.
    /// Keys are returned as-is (not lowercased) — callers handle casing.
    /// </summary>
    public static Dictionary<string, string>? ParseFields(string content)
    {
        var yaml = ExtractYamlBlock(content);
        if (yaml == null) return null;

        var fields = new Dictionary<string, string>();

        foreach (var line in yaml.Split('\n'))
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0) continue;

            var key = line[..colonIndex].Trim();
            if (key.Length == 0) continue;

            var value = line[(colonIndex + 1)..].Trim();
            fields[key] = value;
        }

        return fields;
    }

    /// <summary>
    /// Removes the frontmatter block (including delimiters) and returns the body.
    /// Returns the original content unchanged if no frontmatter is present.
    /// </summary>
    public static string StripFrontmatter(string content)
    {
        if (!content.StartsWith("---")) return content;
        // Anchor the closing delimiter to a line start (same discipline as UpsertField): a "---" SUBSTRING
        // inside a value must not be mistaken for the block's end, or the body would start mid-value.
        var close = ClosingDelimiterIndex(content);
        return close < 0 ? content : content[(close + 3)..].TrimStart();
    }

    /// <summary>
    /// Extracts the raw YAML string between the --- delimiters.
    /// Returns null if no valid frontmatter block exists.
    /// </summary>
    internal static string? ExtractYamlBlock(string content)
    {
        if (!content.StartsWith("---")) return null;
        // Line-anchored close: a "---" inside a value is not the terminator, so the returned block is never
        // truncated mid-value (a file UpsertField wrote correctly is read back correctly).
        var close = ClosingDelimiterIndex(content);
        return close < 0 ? null : content[3..close];
    }

    /// <summary>
    /// Sets a single top-level frontmatter key to <paramref name="value"/>, preserving every other
    /// line, the body, and delimiters. Rewrites the line in place when the key exists; otherwise
    /// appends it as the last frontmatter line. Content with no frontmatter block is returned
    /// unchanged — the caller decides whether an un-fronted file is an error worth surfacing.
    /// </summary>
    public static string UpsertField(string content, string key, string value)
    {
        var yaml = ExtractYamlBlock(content);
        if (yaml == null) return content;

        // Anchor the closing delimiter to a line start: a "---" INSIDE a frontmatter value must not be mistaken
        // for the end of the block, or the write would truncate the file mid-value and corrupt it.
        var closeDelimiter = ClosingDelimiterIndex(content);
        if (closeDelimiter < 0) return content;
        var lines = content[3..closeDelimiter].Split('\n').ToList();
        SetKeyLine(lines, key, value);
        return content[..3] + string.Join('\n', lines) + content[closeDelimiter..];
    }

    /// <summary>Rewrite the frontmatter line for <paramref name="key"/> in place, or append it inside the block
    /// (before the trailing blank line that precedes the closing delimiter) when the key is absent.</summary>
    private static void SetKeyLine(List<string> lines, string key, string value)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var colon = lines[i].IndexOf(':');
            if (colon < 0 || !lines[i][..colon].Trim().Equals(key, StringComparison.Ordinal))
                continue;
            lines[i] = $"{key}: {value}";
            return;
        }

        // lines[0] is the remainder of the OPENING delimiter line (empty for a normal "---\n" open), never a
        // key line, so the back-scan must stop at index 1 — inserting before it would glue the new key onto
        // the opening "---" and corrupt a file whose frontmatter block is empty.
        var insertAt = lines.Count;
        while (insertAt > 1 && lines[insertAt - 1].Trim().Length == 0)
            insertAt--;
        lines.Insert(insertAt, $"{key}: {value}");
    }

    /// <summary>The index at which the closing <c>---</c> line begins — the first <c>---</c> that starts a line
    /// after the opening and is immediately followed by end-of-line or end-of-content. Returns -1 when no such
    /// line exists, so a <c>---</c> inside a value or body is never mistaken for the block's end.</summary>
    private static int ClosingDelimiterIndex(string content)
    {
        var at = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        while (at >= 0)
        {
            if (IsDelimiterLineEnd(content, at + 4))
                return at + 1;
            at = content.IndexOf("\n---", at + 4, StringComparison.Ordinal);
        }
        return -1;
    }

    /// <summary>Whether the closing <c>---</c> is the whole line: only trailing whitespace ("---  \n") may follow
    /// before the line end or end-of-content, so a previously-parseable file is not silently degraded to
    /// frontmatter-less (finding 7), while a "---" inside a value or a longer run is not a terminator.</summary>
    private static bool IsDelimiterLineEnd(string content, int index)
    {
        while (index < content.Length && content[index] is ' ' or '\t')
            index++;
        return index >= content.Length || content[index] is '\n' or '\r';
    }
}
