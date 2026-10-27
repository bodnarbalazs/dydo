namespace DynaDocs.Serialization;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Writes a JSON file a user owns as an edit of its original text, not a fresh serialization:
/// every value the edit left alone keeps its bytes (spacing, escapes, key order, unknown keys),
/// and the BOM, line endings and trailing newline stay the file's own. Only new or changed values
/// are serialized, with relaxed escaping and the file's indent.
/// </summary>
internal static class JsonText
{
    private const char Bom = '﻿';

    /// <summary>The file's text exactly as stored (a BOM stays as U+FEFF), or null when absent.</summary>
    public static string? Read(string path) =>
        File.Exists(path) ? Encoding.UTF8.GetString(File.ReadAllBytes(path)) : null;

    /// <summary>Parses text <see cref="Read"/> returned; throws <see cref="JsonException"/> when malformed.</summary>
    public static JsonNode? Parse(string text) => JsonNode.Parse(text.TrimStart(Bom));

    /// <summary>Writes <paramref name="target"/> over <paramref name="original"/> only when the text changes.</summary>
    public static void Write(string path, string? original, JsonNode target)
    {
        var text = Render(original, target);
        if (text != original)
            File.WriteAllText(path, text);
    }

    /// <summary>
    /// The text of <paramref name="target"/> as an edit of <paramref name="original"/>. Without a
    /// parseable original it is a fresh, LF, newline-terminated document.
    /// </summary>
    public static string Render(string? original, JsonNode target)
    {
        if (original == null)
            return Fresh(target, "", new Style("\n", ' ', 2)) + "\n";

        var bom = original.StartsWith(Bom) ? Bom.ToString() : "";
        var source = new Source(Encoding.UTF8.GetBytes(original[bom.Length..]));
        Span root;
        try
        {
            root = source.ReadRoot();
        }
        catch (JsonException)
        {
            return Fresh(target, "", new Style("\n", ' ', 2)) + "\n";
        }

        return bom + source.Slice(0, root.Start) + new Renderer(source).Render(root, target)
            + source.Slice(root.End, source.Bytes.Length);
    }

    /// <summary>
    /// Applies the change from <paramref name="baseline"/> to <paramref name="updated"/> onto
    /// <paramref name="original"/>, in place where it can. A caller that models only part of a
    /// file passes the model of the original as the baseline, so members the model adds or drops
    /// on its own (defaults, unknown keys) leave the original alone. Returns the merged node.
    /// </summary>
    public static JsonNode? Merge(JsonNode? original, JsonNode? baseline, JsonNode? updated)
    {
        if (JsonNode.DeepEquals(baseline, updated))
            return original;

        switch (original, baseline, updated)
        {
            case (JsonObject o, JsonObject b, JsonObject u):
                foreach (var (key, value) in u.ToList())
                {
                    var current = o[key];
                    var merged = Merge(current, b[key], value);
                    if (!ReferenceEquals(merged, current))
                        o[key] = merged;
                }
                foreach (var key in b.Select(member => member.Key).Where(key => !u.ContainsKey(key)).ToList())
                    o.Remove(key);
                return o;

            case (JsonArray o, JsonArray b, JsonArray u) when o.Count == b.Count:
                for (var i = 0; i < u.Count; i++)
                {
                    if (i >= o.Count)
                    {
                        o.Add(u[i]?.DeepClone());
                        continue;
                    }
                    var merged = Merge(o[i], b[i], u[i]);
                    if (!ReferenceEquals(merged, o[i]))
                        o[i] = merged;
                }
                while (o.Count > u.Count)
                    o.RemoveAt(o.Count - 1);
                return o;

            default:
                return updated?.DeepClone();
        }
    }

    private static string Fresh(JsonNode? node, string indent, Style style)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
               {
                   Indented = true,
                   IndentCharacter = style.IndentCharacter,
                   IndentSize = style.IndentSize,
                   NewLine = "\n",
                   Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
               }))
        {
            if (node == null)
                writer.WriteNullValue();
            else
                node.WriteTo(writer);
        }
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\n", style.NewLine + indent);
    }

    private sealed record Style(string NewLine, char IndentCharacter, int IndentSize)
    {
        public string Unit => new(IndentCharacter, IndentSize);
    }

    /// <summary>A member or item: the whitespace after its comma (or opening bracket), and its value.</summary>
    private sealed record Part(string? Name, string Lead, int KeyStart, Span Value);

    /// <summary>One value's place in the original bytes; containers also hold their parts.</summary>
    private sealed record Span(JsonTokenType Kind, int Start, int End, List<Part> Parts, int TrailStart);

    private sealed class Source(byte[] bytes)
    {
        public byte[] Bytes { get; } = bytes;

        public string Slice(int start, int end) => Encoding.UTF8.GetString(Bytes, start, end - start);

        public Span ReadRoot()
        {
            var reader = new Utf8JsonReader(Bytes);
            if (!reader.Read())
                throw new JsonException("empty document");
            return ReadValue(ref reader);
        }

        private Span ReadValue(ref Utf8JsonReader reader)
        {
            var start = (int)reader.TokenStartIndex;
            var kind = reader.TokenType;
            if (kind is not (JsonTokenType.StartObject or JsonTokenType.StartArray))
                return new Span(kind, start, (int)reader.BytesConsumed, [], 0);

            var parts = new List<Part>();
            var after = (int)reader.BytesConsumed;
            while (reader.Read() && reader.TokenType is not (JsonTokenType.EndObject or JsonTokenType.EndArray))
            {
                var keyStart = (int)reader.TokenStartIndex;
                string? name = null;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    name = reader.GetString();
                    reader.Read();
                }
                var gap = Slice(after, keyStart);
                var value = ReadValue(ref reader);
                parts.Add(new Part(name, gap[(gap.IndexOf(',') + 1)..], keyStart, value));
                after = value.End;
            }
            return new Span(kind, start, (int)reader.BytesConsumed, parts, after);
        }
    }

    private sealed class Renderer(Source source)
    {
        private readonly Style _style = Infer(source.Slice(0, source.Bytes.Length));

        public string Render(Span span, JsonNode? target)
        {
            if (JsonNode.DeepEquals(Node(span), target))
                return source.Slice(span.Start, span.End);
            return (span.Kind, target) switch
            {
                (JsonTokenType.StartObject, JsonObject members) => RenderObject(span, members),
                (JsonTokenType.StartArray, JsonArray items) => RenderArray(span, items),
                _ => Fresh(target, LineIndent(span.Start), _style)
            };
        }

        private string RenderObject(Span span, JsonObject target)
        {
            var parts = target.Select(member =>
            {
                var part = span.Parts.FirstOrDefault(candidate => candidate.Name == member.Key);
                if (part != null)
                    return part.Lead + source.Slice(part.KeyStart, part.Value.Start) + Render(part.Value, member.Value);
                var lead = NewLead(span);
                return lead + Fresh(JsonValue.Create(member.Key), "", _style) + ": "
                    + Fresh(member.Value, Indent(lead), _style);
            });
            return Container(span, target.Count, parts, '{', '}');
        }

        private string RenderArray(Span span, JsonArray target)
        {
            var originals = span.Parts.Select(part => Node(part.Value)).ToList();
            var matched = Match(originals, target);
            var parts = target.Select((item, i) =>
            {
                if (matched[i] >= 0)
                {
                    var part = span.Parts[matched[i]];
                    return part.Lead + Render(part.Value, item);
                }
                var lead = NewLead(span);
                return lead + Fresh(item, Indent(lead), _style);
            });
            return Container(span, target.Count, parts, '[', ']');
        }

        private string Container(Span span, int count, IEnumerable<string> parts, char open, char close)
        {
            if (count == 0)
                return $"{open}{close}";
            var trail = span.Parts.Count > 0
                ? source.Slice(span.TrailStart, span.End - 1)
                : _style.NewLine + LineIndent(span.Start);
            return open + string.Join(",", parts) + trail + close;
        }

        // An item keeps its original text when an equal one is still ahead; an item that changed
        // takes the unused original of the same kind between its matched neighbours.
        private static int[] Match(List<JsonNode?> originals, JsonArray target)
        {
            var matched = Enumerable.Repeat(-1, target.Count).ToArray();
            var used = new bool[originals.Count];
            var next = 0;
            for (var i = 0; i < target.Count; i++)
            {
                var at = originals.FindIndex(next, original => JsonNode.DeepEquals(original, target[i]));
                if (at < 0)
                    continue;
                matched[i] = at;
                used[at] = true;
                next = at + 1;
            }

            for (var i = 0; i < target.Count; i++)
            {
                if (matched[i] >= 0)
                    continue;
                var low = matched.Take(i).Where(at => at >= 0).DefaultIfEmpty(-1).Max() + 1;
                var high = matched.Skip(i + 1).Where(at => at >= 0).DefaultIfEmpty(originals.Count).First();
                for (var at = low; at < high; at++)
                {
                    if (used[at] || originals[at]?.GetValueKind() != target[i]?.GetValueKind())
                        continue;
                    matched[i] = at;
                    used[at] = true;
                    break;
                }
            }
            return matched;
        }

        private string NewLead(Span container) => container.Parts.Count > 0
            ? container.Parts[^1].Lead
            : _style.NewLine + LineIndent(container.Start) + _style.Unit;

        private JsonNode? Node(Span span) => JsonNode.Parse(source.Slice(span.Start, span.End));

        private static string Indent(string lead) => lead[(lead.LastIndexOf('\n') + 1)..];

        private string LineIndent(int position)
        {
            var lineStart = Array.LastIndexOf(source.Bytes, (byte)'\n', Math.Max(position - 1, 0)) + 1;
            var end = lineStart;
            while (end < position && source.Bytes[end] is (byte)' ' or (byte)'\t')
                end++;
            return source.Slice(lineStart, end);
        }

        // The file's newline, and the indent of its first indented line as one level.
        private static Style Infer(string text)
        {
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var line = text.Split('\n').Skip(1).FirstOrDefault(candidate => candidate.Length > 0 && candidate[0] is ' ' or '\t');
            if (line == null)
                return new Style(newline, ' ', 2);
            var indent = line.Length - line.TrimStart(' ', '\t').Length;
            return new Style(newline, line[0], line[0] == '\t' ? 1 : indent);
        }
    }
}
