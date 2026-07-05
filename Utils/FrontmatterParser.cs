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
            // First-wins on a duplicate key (finding 7): UpsertField rewrites the FIRST duplicate line, so the
            // reader must resolve the first occurrence too — else an upserted value reads back invisible on a
            // duplicate-key file. Matches SyncDoc.GetField / FieldMerge.ToMap / the reconcile FirstWins overlay.
            fields.TryAdd(key, value);
        }

        return fields;
    }

    /// <summary>
    /// Removes the frontmatter block (including delimiters) and returns the body.
    /// Returns the original content unchanged if no frontmatter is present.
    /// </summary>
    public static string StripFrontmatter(string content)
    {
        var bounds = Bounds(content);
        return bounds == null ? content : content[bounds.Value.BodyStart..].TrimStart();
    }

    /// <summary>
    /// Extracts the raw YAML string between the --- delimiters.
    /// Returns null if no valid frontmatter block exists.
    /// </summary>
    internal static string? ExtractYamlBlock(string content)
    {
        var bounds = Bounds(content);
        return bounds == null ? null : content[bounds.Value.YamlStart..bounds.Value.CloserStart];
    }

    /// <summary>
    /// Sets a single top-level frontmatter key to <paramref name="value"/>, preserving every other
    /// line, the body, and delimiters. Rewrites the line in place when the key exists; otherwise
    /// appends it as the last frontmatter line. Content with no frontmatter block is returned
    /// unchanged — the caller decides whether an un-fronted file is an error worth surfacing.
    /// </summary>
    public static string UpsertField(string content, string key, string value)
    {
        var bounds = Bounds(content);
        if (bounds == null) return content;

        // The opener line (content[..openerLen]) and the closing delimiter both come from the shared Bounds
        // helper, so opener tolerance and the empty-block case never diverge from the other frontmatter readers
        // (finding 8). The yaml region begins at the opener's newline, so lines[0] is that line's remainder.
        var openerLen = bounds.Value.YamlStart - 1;
        var closerStart = bounds.Value.CloserStart;
        var lines = content[openerLen..closerStart].Split('\n').ToList();
        SetKeyLine(lines, key, value);
        return content[..openerLen] + string.Join('\n', lines) + content[closerStart..];
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

    /// <summary>The single shared boundary of a leading frontmatter block, resolved identically for every
    /// frontmatter reader — this parser, <c>SyncDocFile</c>, and <c>SyncCommand</c> — so their opener,
    /// empty-block, and closer semantics can never diverge (finding 8). The opener is <c>---</c> on the first
    /// line (trailing whitespace tolerated); the closer is the first LATER line that is <c>---</c> with only
    /// trailing whitespace, so a <c>---</c> inside a value is never the terminator and an EMPTY block
    /// (<c>---</c> immediately followed by a <c>---</c> line) is valid. Returns null when the content has no
    /// such block. <c>YamlStart</c> is the index after the opener line's newline; <c>CloserStart</c> is the
    /// index of the closing <c>---</c> (== <c>YamlStart</c> for an empty block); <c>BodyStart</c> is the index
    /// after the closing line's newline. Operates on either <c>\n</c> or <c>\r\n</c> line endings.</summary>
    internal static (int YamlStart, int CloserStart, int BodyStart)? Bounds(string content)
    {
        var openerNewline = content.IndexOf('\n');
        if (openerNewline < 0) return null;
        // Validate the OPENER with the same strictness as the closer, matching the XML contract above (review
        // R2-3): exactly `---` on the first line, trailing whitespace tolerated. A bare StartsWith("---") also
        // opened on `----` (a 4-dash horizontal rule) or `--- title`, so a body that happens to begin with such a
        // line — plus any later `---` — was mis-parsed as bogus frontmatter, mangling body content into fields on
        // the sync path. IsDelimiterLine keeps the trailing-whitespace tolerance the doc promises.
        if (!IsDelimiterLine(content, 0, openerNewline)) return null;

        var lineStart = openerNewline + 1;
        while (true)
        {
            var newline = content.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? content.Length : newline;
            if (IsDelimiterLine(content, lineStart, lineEnd))
                return (openerNewline + 1, lineStart, newline < 0 ? content.Length : newline + 1);
            if (newline < 0) return null;
            lineStart = newline + 1;
        }
    }

    /// <summary>Whether <c>content[start..end)</c> is a delimiter line: exactly <c>---</c> followed only by
    /// trailing whitespace (spaces, tabs, or a <c>\r</c>). A longer run of dashes or any other trailing content
    /// is not a delimiter, so a <c>---</c> inside a value or a horizontal rule is never mistaken for one.</summary>
    private static bool IsDelimiterLine(string content, int start, int end)
    {
        while (end > start && char.IsWhiteSpace(content[end - 1]))
            end--;
        return content.AsSpan(start, end - start).SequenceEqual("---");
    }
}
