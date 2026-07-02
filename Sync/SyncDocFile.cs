namespace DynaDocs.Sync;

using System.Text;
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
            var colon = SeparatorColon(line);
            if (colon < 0) continue;
            var key = Decode(line[..colon].Trim(), isKey: true);
            if (key.Length == 0) continue;
            fields.Add(new SyncField { Key = key, Value = Decode(line[(colon + 1)..].Trim(), isKey: false) });
        }

        return (fields, body);
    }
}
