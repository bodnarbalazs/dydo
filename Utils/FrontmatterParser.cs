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
}
