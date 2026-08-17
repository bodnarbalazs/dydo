namespace DynaDocs.Sync;

using System.Text;
using DynaDocs.Models;
using DynaDocs.Utils;

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
        // Atomic write (ns-4 finding 4): a torn WriteAllText could leave a conflict-shadow file with no surviving
        // merge sentinel, which PromoteResolvedShadows would then mistake for a human's resolution and promote over
        // the canonical doc. Render to a temp sibling, then move-with-overwrite (a same-directory rename), so any
        // reader — this process or the next tick — ever sees only the complete content, never a half-written body.
        var temp = filePath + ".tmp" + Guid.NewGuid().ToString("N")[..8];
        File.WriteAllText(temp, Render(doc));
        File.Move(temp, filePath, overwrite: true);
    }

    /// <summary>Patch an existing canonical file without re-rendering its untouched frontmatter or body.</summary>
    public static void PatchExisting(string filePath, SyncDoc current, SyncDoc desired, bool patchFields, bool patchBody)
    {
        var bytes = File.ReadAllBytes(filePath);
        var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var content = File.ReadAllText(filePath);
        var patched = content;
        if (patchFields)
            patched = PatchFields(patched, current.Fields, desired.Fields);
        if (patchBody)
            patched = PatchBody(patched, desired.Body);
        if (patched == content)
            return;
        var temp = filePath + ".tmp" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            File.WriteAllText(temp, patched, new UTF8Encoding(bom));
            File.Move(temp, filePath, overwrite: true);
        }
        finally
        {
            // File.Delete is intentionally idempotent, so cleanup does not need a racy existence
            // probe after a successful same-directory move.
            File.Delete(temp);
        }
    }

    public static string Render(SyncDoc doc)
    {
        var lines = new List<string> { "---" };
        foreach (var field in doc.Fields)
            lines.Add($"{Encode(field.Key, isKey: true)}: {Encode(field.Value, isKey: false)}");
        lines.Add("---");
        // One blank line between frontmatter and body, matching dydo's house style.
        return string.Join('\n', lines) + "\n\n" + doc.Body.TrimStart('\n');
    }

    /// <summary>
    /// Frontmatter is the reliable-data channel — these files are committed and trusted by agents and
    /// tooling — but field keys and values can be externally authored (a colleague's Notion property name
    /// or value; coding-standards §6 boundary validation). A raw newline or a leading <c>---</c> in such a
    /// value would forge sibling frontmatter keys or terminate the block and inject a markdown body, and a
    /// KEY bearing a <c>:</c> would mis-split on read. So a value carrying a newline, carriage return, or a
    /// leading quote — and a KEY additionally carrying a colon — is emitted as a double-quoted,
    /// backslash-escaped scalar that can never escape its single line. <see cref="Decode"/> is the exact
    /// inverse: it unquotes ONLY a token <see cref="Encode"/> would itself emit (unescapes cleanly and
    /// re-encodes to the same token), so an encoded value round-trips byte-for-byte (Decision 025 §3) while
    /// a hand-authored value that merely sits in quotes (<c>status: "active"</c>) passes through verbatim,
    /// never mutated. Ordinary values pass through unchanged.
    /// </summary>
    private static string Encode(string s, bool isKey) =>
        NeedsQuoting(s, isKey)
            ? "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\""
            : s;

    private static bool NeedsQuoting(string s, bool isKey) =>
        s.Length > 0 && (s[0] == '"' || s.AsSpan().IndexOfAny('\n', '\r') >= 0 || (isKey && s.Contains(':')));

    /// <summary>The index of the <c>key: value</c> separator colon. When the key is a quoted scalar (see
    /// <see cref="Encode"/>), a colon inside the quotes belongs to the escaped key, so the separator is the
    /// first colon past the closing quote — otherwise it is simply the first colon.</summary>
    private static int SeparatorColon(string line)
    {
        if (line.Length == 0 || line[0] != '"')
            return line.IndexOf(':');

        for (var i = 1; i < line.Length; i++)
        {
            if (line[i] == '\\') { i++; continue; }
            if (line[i] == '"') return line.IndexOf(':', i + 1);
        }
        return -1;
    }

    /// <summary>Reverse of <see cref="Encode"/>. A token is unescaped only when it is EXACTLY what Encode
    /// would emit — it unescapes cleanly AND re-encodes to the same token (so Encode would in fact have
    /// quoted it). Anything else is returned verbatim: a hand-authored value that merely sits in quotes
    /// (<c>status: "active"</c>) or one Encode would never quote is passed through untouched, never mangled.</summary>
    private static string Decode(string token, bool isKey) =>
        TryUnescape(token, out var decoded) && Encode(decoded, isKey) == token
            ? decoded
            : token;

    /// <summary>Unescape a double-quoted token whose body uses only the escape sequences <see cref="Encode"/>
    /// emits (<c>\\</c>, <c>\"</c>, <c>\r</c>, <c>\n</c>) and holds no raw inner quote. Returns false — leaving
    /// the token to pass through verbatim — for any token that is not well-formed Encode output.</summary>
    private static bool TryUnescape(string token, out string decoded)
    {
        decoded = "";
        if (!IsQuotedToken(token))
            return false;

        var inner = token[1..^1];
        var sb = new StringBuilder(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (c == '"')
                return false; // a raw inner quote — Encode escapes these, so this is not our output
            if (c != '\\')
            {
                sb.Append(c);
                continue;
            }
            // A backslash must open one of Encode's escape sequences; anything else (a trailing lone
            // backslash, or an escape Encode never emits) means the token is not our output.
            if (i + 1 >= inner.Length || !TryUnescapeChar(inner[++i], out var unescaped))
                return false;
            sb.Append(unescaped);
        }
        decoded = sb.ToString();
        return true;
    }

    private static bool IsQuotedToken(string token) =>
        token.Length >= 2 && token[0] == '"' && token[^1] == '"';

    private static bool TryUnescapeChar(char escaped, out char result)
    {
        switch (escaped)
        {
            case '\\': result = '\\'; return true;
            case '"': result = '"'; return true;
            case 'r': result = '\r'; return true;
            case 'n': result = '\n'; return true;
            default: result = '\0'; return false;
        }
    }

    private static string PatchBody(string content, string body)
    {
        var bounds = FrontmatterParser.Bounds(content);
        if (bounds == null)
            return body;
        var bodyStart = bounds.Value.BodyStart;
        while (bodyStart < content.Length && (content[bodyStart] == '\r' || content[bodyStart] == '\n'))
            bodyStart++;
        return content[..bodyStart] + body;
    }

    private static string PatchFields(string content, IReadOnlyList<SyncField> current, IReadOnlyList<SyncField> desired)
    {
        var bounds = FrontmatterParser.Bounds(content);
        if (bounds == null)
            return content;

        var desiredByKey = FirstWins(desired);
        var currentByKey = FirstWins(current);
        var edits = new List<(int Start, int Length, string Text)>();
        var firstLineByKey = FrontmatterFieldLines(content, bounds.Value);
        AddChangedFieldEdits(edits, desiredByKey, currentByKey, firstLineByKey, bounds.Value.CloserStart, Newline(content));
        AddRemovedFieldEdits(edits, desiredByKey, currentByKey, firstLineByKey);

        foreach (var edit in edits.OrderByDescending(e => e.Start))
            content = content[..edit.Start] + edit.Text + content[(edit.Start + edit.Length)..];
        return content;
    }

    private static Dictionary<string, (int Start, int Length, string Text)> FrontmatterFieldLines(string content,
        (int YamlStart, int CloserStart, int BodyStart) bounds)
    {
        var byKey = new Dictionary<string, (int Start, int Length, string Text)>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in FrontmatterLines(content, bounds.YamlStart, bounds.CloserStart))
            AddFieldLine(byKey, line);
        return byKey;
    }

    private static void AddFieldLine(Dictionary<string, (int Start, int Length, string Text)> byKey,
        (int Start, int Length, string Text) line)
    {
        var colon = SeparatorColon(line.Text);
        if (colon < 0)
            return;
        var key = Decode(line.Text[..colon].Trim(), isKey: true);
        if (key.Length > 0)
            byKey.TryAdd(key, line);
    }

    private static void AddChangedFieldEdits(List<(int Start, int Length, string Text)> edits,
        IReadOnlyDictionary<string, string> desired, IReadOnlyDictionary<string, string> current,
        IReadOnlyDictionary<string, (int Start, int Length, string Text)> lines, int closerStart, string newline)
    {
        foreach (var (key, value) in desired)
        {
            if (current.TryGetValue(key, out var old) && old == value)
                continue;
            if (lines.TryGetValue(key, out var line))
                edits.Add((line.Start, line.Text.Length, $"{Encode(key, true)}: {Encode(value, false)}"));
            else
                edits.Add((closerStart, 0, $"{Encode(key, true)}: {Encode(value, false)}{newline}"));
        }
    }

    private static void AddRemovedFieldEdits(List<(int Start, int Length, string Text)> edits,
        IReadOnlyDictionary<string, string> desired, IReadOnlyDictionary<string, string> current,
        IReadOnlyDictionary<string, (int Start, int Length, string Text)> lines)
    {
        foreach (var (key, _) in current)
            if (!desired.ContainsKey(key) && lines.TryGetValue(key, out var line))
                edits.Add((line.Start, line.Length, ""));
    }

    private static string Newline(string content) => content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static Dictionary<string, string> FirstWins(IEnumerable<SyncField> fields)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
            values.TryAdd(field.Key, field.Value);
        return values;
    }

    private static List<(int Start, int Length, string Text)> FrontmatterLines(string content, int start, int end)
    {
        var lines = new List<(int Start, int Length, string Text)>();
        for (var position = start; position < end;)
        {
            var newline = content.IndexOf('\n', position);
            var lineEnd = newline < 0 || newline >= end ? end : newline;
            var textEnd = lineEnd > position && content[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;
            var length = (newline < 0 || newline >= end) ? end - position : newline - position + 1;
            lines.Add((position, length, content[position..textEnd]));
            position += length;
        }
        return lines;
    }


    /// <summary>
    /// Splits content into ordered frontmatter fields and the trailing body. A file without a
    /// leading frontmatter block yields empty fields and the whole content as body. Frontmatter
    /// boundaries come from the shared <see cref="FrontmatterParser.Bounds"/> helper, so the opener
    /// tolerance (trailing whitespace) and the empty-block case match every other reader (finding 8).
    /// </summary>
    private static (List<SyncField> Fields, string Body) SplitFrontmatter(string content)
    {
        var normalized = content.Replace("\r\n", "\n");
        var bounds = FrontmatterParser.Bounds(normalized);
        if (bounds == null)
            return ([], normalized.TrimStart('\n'));

        var (yamlStart, closerStart, bodyStart) = bounds.Value;
        var yaml = normalized[yamlStart..closerStart];
        var body = normalized[bodyStart..].TrimStart('\n');
        return (ParseYamlFields(yaml), body);
    }

    /// <summary>Parse the frontmatter YAML block's lines into ordered fields, applying <see cref="Decode"/> to
    /// each key and value. Lines without a separator colon, or with an empty key, are skipped.</summary>
    private static List<SyncField> ParseYamlFields(string yaml)
    {
        var fields = new List<SyncField>();
        foreach (var line in yaml.Split('\n'))
        {
            var colon = SeparatorColon(line);
            if (colon < 0) continue;
            var key = Decode(line[..colon].Trim(), isKey: true);
            if (key.Length == 0) continue;
            fields.Add(new SyncField { Key = key, Value = Decode(line[(colon + 1)..].Trim(), isKey: false) });
        }
        return fields;
    }

}
