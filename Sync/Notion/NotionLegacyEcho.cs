namespace DynaDocs.Sync.Notion;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Markdig;
using Markdig.Parsers;
using Markdig.Syntax;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// A FROZEN copy of the ns-6-era block converter (<c>FromBlocks∘ToBlocks</c>), kept only to recognise the board's
/// pre-ns-7 content during the one-time migration. Boards synced before ns-7 hold blocks the OLD converter pushed;
/// reading them back and normalizing under the NEW converter diverges (tables/quotes were flat, blank lines were
/// dropped), which the reconcile would otherwise misread as an external edit and use to overwrite the canonical
/// file. <see cref="NotionSyncAdapter.IsStaleConverterEcho"/> compares an external body against
/// <see cref="Render"/> of the base to detect exactly that case and force a repo→board upgrade instead.
/// <para>Do NOT extend or "fix" this — it must reproduce the old behaviour byte-for-byte. REMOVAL DEFERRED (issue
/// 0299 F13): remove the whole file and the shim once every DOWNSTREAM install has synced at least once post-2.2.
/// ns-10's live run only confirmed THIS repo's board is re-rendered under the new converter; the frozen echo is the
/// ns-6-era Markdig converter (never shipped in a release — ns-6/ns-7 are both inside v2.1.0..HEAD), so it targets
/// dev-window boards, but whether a v2.1.0-era board (pushed by the older LINE-based converter) still needs it is
/// undetermined — hence the deferral rather than deletion. Tracked in dydo/project/backlog/notion-board-followups.md.</para>
/// Excluded from coverage: frozen dead-on-arrival code, exercised behaviourally by the migration standing test but
/// not maintained to the active tier.
/// </summary>
[ExcludeFromCodeCoverage]
public static class NotionLegacyEcho
{
    /// <summary>The body the ns-6 converter would echo for <paramref name="body"/> — its old block round-trip.</summary>
    public static string Render(string body) => FromBlocks(ToBlocks(body));

    private static List<NotionBlock> ToBlocks(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n");
        var document = Markdown.Parse(text, Pipeline);
        var blocks = new List<NotionBlock>();
        foreach (var node in document)
            blocks.AddRange(Convert(node, text, nested: false));
        return blocks;
    }

    private static readonly MarkdownPipeline Pipeline = BuildPipeline();

    private static MarkdownPipeline BuildPipeline()
    {
        var builder = new MarkdownPipelineBuilder();
        builder.BlockParsers.Find<ParagraphBlockParser>()!.ParseSetexHeadings = false;
        return builder.Build();
    }

    private static string FromBlocks(IReadOnlyList<NotionBlock> blocks)
    {
        var sb = new StringBuilder();
        Render(sb, blocks, "");
        return sb.ToString().TrimEnd('\n');
    }

    private static List<NotionBlock> Convert(Block node, string src, bool nested)
    {
        switch (node)
        {
            case HeadingBlock heading when heading.Level is >= 1 and <= 3:
            {
                var body = Body(StripHeadingMarker(Slice(heading, src)));
                return heading.Level switch
                {
                    1 => [new NotionBlock { Type = "heading_1", Heading1 = body }],
                    2 => [new NotionBlock { Type = "heading_2", Heading2 = body }],
                    _ => [new NotionBlock { Type = "heading_3", Heading3 = body }],
                };
            }
            case FencedCodeBlock fenced:
                return [CodeBlock(TrimTrailingNewline(fenced.Lines.ToString()), fenced.Info ?? "")];
            case CodeBlock code:
                return [new NotionBlock { Type = "paragraph", Paragraph = Body(Slice(code, src).TrimEnd('\n')) }];
            case ListBlock list:
            {
                if (list.IsOrdered && !IsSequentialFromOne(list))
                    return VerbatimOrderedItems(list, src);
                var items = new List<NotionBlock>();
                foreach (var child in list)
                    if (child is ListItemBlock item)
                        items.Add(ConvertListItem(item, list.IsOrdered, src));
                return items;
            }
            default:
                var span = Slice(node, src);
                return [new NotionBlock { Type = "paragraph", Paragraph = Body(nested ? CleanText(span) : span) }];
        }
    }

    private static NotionBlock ConvertListItem(ListItemBlock item, bool ordered, string src)
    {
        string? text = null;
        var children = new List<NotionBlock>();
        foreach (var child in item)
        {
            if (text == null && child is ParagraphBlock paragraph)
                text = CleanText(Slice(paragraph, src));
            else
                children.AddRange(Convert(child, src, nested: true));
        }
        var body = Body(text ?? "");
        return new NotionBlock
        {
            Type = ordered ? "numbered_list_item" : "bulleted_list_item",
            BulletedListItem = ordered ? null : body,
            NumberedListItem = ordered ? body : null,
            Children = children.Count > 0 ? children : null,
        };
    }

    private static bool IsSequentialFromOne(ListBlock list)
    {
        var expected = 1;
        foreach (var child in list)
        {
            if (child is not ListItemBlock item || item.Order != expected)
                return false;
            expected++;
        }
        return true;
    }

    private static List<NotionBlock> VerbatimOrderedItems(ListBlock list, string src)
    {
        var result = new List<NotionBlock>();
        foreach (var child in list)
        {
            if (child is not ListItemBlock item)
                continue;
            string? text = null;
            var children = new List<NotionBlock>();
            foreach (var sub in item)
            {
                if (text == null && sub is ParagraphBlock paragraph)
                    text = CleanText(Slice(paragraph, src));
                else
                    children.AddRange(Convert(sub, src, nested: true));
            }
            var marker = item.Order + list.OrderedDelimiter.ToString() + " ";
            result.Add(new NotionBlock
            {
                Type = "paragraph",
                Paragraph = Body(marker + (text ?? "")),
                Children = children.Count > 0 ? children : null,
            });
        }
        return result;
    }

    private static void Render(StringBuilder sb, IReadOnlyList<NotionBlock> blocks, string prefix)
    {
        var number = 0;
        foreach (var block in blocks)
        {
            if (block.Type == "child_page")
                continue;
            number = block.Type == "numbered_list_item" ? number + 1 : 0;
            foreach (var physical in BlockToLine(block, number).Split('\n'))
                sb.Append(prefix).Append(physical).Append('\n');
            if (block.Children is { Count: > 0 } children)
                Render(sb, children, prefix + new string(' ', MarkerWidth(block, number)));
        }
    }

    private static string BlockToLine(NotionBlock block, int number) => block.Type switch
    {
        "heading_1" => "# " + Text(block.Heading1),
        "heading_2" => "## " + Text(block.Heading2),
        "heading_3" => "### " + Text(block.Heading3),
        "bulleted_list_item" => "- " + Text(block.BulletedListItem),
        "numbered_list_item" => number + ". " + Text(block.NumberedListItem),
        "code" => Fence(block.Code),
        _ => Text(block.Paragraph),
    };

    private static int MarkerWidth(NotionBlock block, int number) =>
        block.Type == "numbered_list_item" ? number.ToString().Length + 2 : 2;

    private static string Slice(Block block, string src)
    {
        var span = block.Span;
        if (span.Start < 0 || span.Length <= 0 || span.Start >= src.Length)
            return "";
        return src.Substring(span.Start, Math.Min(span.Length, src.Length - span.Start));
    }

    private static string CleanText(string raw)
    {
        var newline = raw.IndexOf('\n');
        if (newline < 0)
            return raw;
        var lines = raw.Split('\n');
        for (var i = 1; i < lines.Length; i++)
            lines[i] = lines[i].TrimStart(' ', '\t');
        return string.Join('\n', lines);
    }

    private static string StripHeadingMarker(string raw)
    {
        var i = 0;
        while (i < raw.Length && raw[i] == '#')
            i++;
        while (i < raw.Length && (raw[i] == ' ' || raw[i] == '\t'))
            i++;
        return raw[i..];
    }

    private static string TrimTrailingNewline(string s) => s.EndsWith('\n') ? s[..^1] : s;

    private static NotionBlockBody Body(string text) => new()
    {
        RichText = NotionRichText.Of(text),
    };

    private static NotionBlock CodeBlock(string code, string language) => new()
    {
        Type = "code",
        Code = new NotionBlockBody { RichText = NotionRichText.Of(code), Language = NormalizeLanguage(language) },
    };

    private static string Text(NotionBlockBody? body) => NotionRichText.Flatten(body?.RichText);

    private static string Fence(NotionBlockBody? body)
    {
        var lang = body?.Language is { Length: > 0 } and not "plain text" ? body.Language : "";
        return "```" + lang + "\n" + Text(body) + "\n```";
    }

    private static string NormalizeLanguage(string language)
    {
        var lang = language.Trim().ToLowerInvariant();
        if (lang.Length == 0)
            return "plain text";
        if (LanguageAliases.TryGetValue(lang, out var canonical))
            lang = canonical;
        return NotionLanguages.Contains(lang) ? lang : "plain text";
    }

    private static readonly Dictionary<string, string> LanguageAliases = new(StringComparer.Ordinal)
    {
        ["csharp"] = "c#", ["cs"] = "c#",
        ["cpp"] = "c++",
        ["fsharp"] = "f#",
        ["py"] = "python",
        ["js"] = "javascript",
        ["ts"] = "typescript",
        ["yml"] = "yaml",
        ["sh"] = "shell", ["zsh"] = "shell", ["console"] = "shell", ["shell-session"] = "shell",
        ["pwsh"] = "powershell", ["ps1"] = "powershell",
        ["md"] = "markdown",
        ["text"] = "plain text", ["plaintext"] = "plain text", ["plain"] = "plain text", ["txt"] = "plain text",
        ["dockerfile"] = "docker",
        ["objc"] = "objective-c",
        ["vb"] = "visual basic",
        ["asm"] = "assembly",
        ["tex"] = "latex",
        ["golang"] = "go", ["rs"] = "rust", ["kt"] = "kotlin", ["rb"] = "ruby",
    };

    private static readonly HashSet<string> NotionLanguages = new(StringComparer.Ordinal)
    {
        "abap", "abc", "agda", "arduino", "ascii art", "assembly", "bash", "basic", "bnf", "c", "c#", "c++",
        "clojure", "coffeescript", "coq", "css", "dart", "dhall", "diff", "docker", "ebnf", "elixir", "elm",
        "erlang", "f#", "flow", "fortran", "gherkin", "glsl", "go", "graphql", "groovy", "haskell", "hcl",
        "html", "idris", "java", "javascript", "json", "julia", "kotlin", "latex", "less", "lisp", "livescript",
        "llvm ir", "lua", "makefile", "markdown", "markup", "matlab", "mathematica", "mermaid", "nix",
        "notion formula", "objective-c", "ocaml", "pascal", "perl", "php", "plain text", "powershell", "prolog",
        "protobuf", "purescript", "python", "r", "racket", "reason", "ruby", "rust", "sass", "scala", "scheme",
        "scss", "shell", "smalltalk", "solidity", "sql", "swift", "toml", "typescript", "vb.net", "verilog",
        "vhdl", "visual basic", "webassembly", "xml", "yaml", "java/c/c++/c#",
    };
}
