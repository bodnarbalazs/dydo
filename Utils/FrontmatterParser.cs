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
        var endIndex = content.IndexOf("---", 3);
        return endIndex < 0 ? content : content[(endIndex + 3)..].TrimStart();
    }

    /// <summary>
    /// Extracts the raw YAML string between the --- delimiters.
    /// Returns null if no valid frontmatter block exists.
    /// </summary>
    internal static string? ExtractYamlBlock(string content)
    {
        if (!content.StartsWith("---")) return null;
        var endIndex = content.IndexOf("---", 3);
        return endIndex < 0 ? null : content[3..endIndex];
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

        var closeDelimiter = content.IndexOf("---", 3);
        var block = content[3..closeDelimiter];
        var lines = block.Split('\n').ToList();

        var replaced = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var colon = lines[i].IndexOf(':');
            if (colon < 0) continue;
            if (!lines[i][..colon].Trim().Equals(key, StringComparison.Ordinal)) continue;
            lines[i] = $"{key}: {value}";
            replaced = true;
            break;
        }

        if (!replaced)
        {
            // Insert before the trailing blank line that precedes the closing delimiter, so the new
            // key stays inside the key block rather than after a gap.
            var insertAt = lines.Count;
            while (insertAt > 0 && lines[insertAt - 1].Trim().Length == 0)
                insertAt--;
            lines.Insert(insertAt, $"{key}: {value}");
        }

        return content[..3] + string.Join('\n', lines) + content[closeDelimiter..];
    }
}
