namespace DynaDocs.Sync;

using DynaDocs.Models;

/// <summary>
/// Maps a dydo doc file (YAML frontmatter + markdown body) to/from a <see cref="SyncDoc"/>,
/// preserving frontmatter key order and the body verbatim. Generic over object type: the
/// caller supplies the localId and source path, so Task/Campaign/Sprint all use the same path.
/// </summary>
public static class SyncDocFile
{
    public static SyncDoc Read(string filePath, string localId, string sourcePath)
    {
        var content = File.ReadAllText(filePath);
        return Parse(content, localId, sourcePath);
    }

    public static SyncDoc Parse(string content, string localId, string sourcePath)
    {
        var (fields, body) = SplitFrontmatter(content);
        return new SyncDoc
        {
            LocalId = localId,
            Fields = fields,
            Body = body,
            SourcePath = sourcePath,
        };
    }

    public static void Write(string filePath, SyncDoc doc)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, Render(doc));
    }

    public static string Render(SyncDoc doc)
    {
        var lines = new List<string> { "---" };
        foreach (var field in doc.Fields)
            lines.Add($"{field.Key}: {field.Value}");
        lines.Add("---");
        // One blank line between frontmatter and body, matching dydo's house style.
        return string.Join('\n', lines) + "\n\n" + doc.Body.TrimStart('\n');
    }

    /// <summary>
    /// Splits content into ordered frontmatter fields and the trailing body. A file without a
    /// leading frontmatter block yields empty fields and the whole content as body.
    /// </summary>
    private static (List<SyncField> Fields, string Body) SplitFrontmatter(string content)
    {
        var normalized = content.Replace("\r\n", "\n");
        if (!normalized.StartsWith("---\n"))
            return ([], normalized.TrimStart('\n'));

        var end = normalized.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (end < 0)
            return ([], normalized.TrimStart('\n'));

        var yaml = normalized[4..end];
        var afterDelimiter = end + "\n---".Length;
        var body = normalized[afterDelimiter..].TrimStart('\n');

        var fields = new List<SyncField>();
        foreach (var line in yaml.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key = line[..colon].Trim();
            if (key.Length == 0) continue;
            fields.Add(new SyncField { Key = key, Value = line[(colon + 1)..].Trim() });
        }

        return (fields, body);
    }
}
