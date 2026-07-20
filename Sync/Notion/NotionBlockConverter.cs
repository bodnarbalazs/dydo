namespace DynaDocs.Sync.Notion;

using System.Text;
using Markdig;
using Markdig.Parsers;
using Markdig.Syntax;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// Structure-aware, best-effort markdown ⇄ Notion block conversion (Decision 025 §6, ns-6). Write: parse the
/// Markdig AST and map each block — headings (# / ## / ###), paragraphs, fenced/indented code (with language
/// normalization), and bulleted/numbered lists including nested sub-lists carried as block <c>children</c>.
/// Inline text stays a plain, unannotated run: the raw source span is kept verbatim (no bold/italic/link
/// parsing this sprint), so a marker like <c>**x**</c> survives unchanged. Read: each block renders back to a
/// markdown line, nested children indented under their parent. The structured frontmatter↔property path carries
/// the reliable data; bodies are a 3-way *text* merge over this approximation.
/// </summary>
public static class NotionBlockConverter
{
    public static List<NotionBlock> ToBlocks(string markdown)
    {
        var text = markdown.Replace("\r\n", "\n");
        var document = Markdown.Parse(text, Pipeline);
        var blocks = new List<NotionBlock>();
        foreach (var node in document)
            blocks.AddRange(Convert(node, text, nested: false));
        return blocks;
    }

    /// <summary>Setext headings are disabled: a paragraph directly above a <c>---</c>/<c>===</c> line must stay a
    /// paragraph, never be promoted to a heading. Body normalization strips blank lines, which brings a
    /// blank-separated thematic-break <c>---</c> adjacent to the paragraph above it; setext promotion there makes
    /// <c>FromBlocks∘ToBlocks</c> non-idempotent and would silently rewrite the canonical doc on the next sync.</summary>
    private static readonly MarkdownPipeline Pipeline = BuildPipeline();

    private static MarkdownPipeline BuildPipeline()
    {
        var builder = new MarkdownPipelineBuilder();
        builder.BlockParsers.Find<ParagraphBlockParser>()!.ParseSetexHeadings = false;
        return builder.Build();
    }

    public static string FromBlocks(IReadOnlyList<NotionBlock> blocks)
    {
        var sb = new StringBuilder();
        Render(sb, blocks, "");
        return sb.ToString().TrimEnd('\n');
    }

    /// <param name="nested">True when this block is a child inside a list item, so its rendered lines will carry an
    /// indent prefix. A nested multi-line paragraph must have its continuation-line indentation stripped or the
    /// prefix would double it every normalization (runaway indent); a top-level paragraph keeps its lines verbatim,
    /// matching the old converter and staying idempotent (an indented line there is stable prose, not re-indented).</param>
    private static List<NotionBlock> Convert(Block node, string src, bool nested)
    {
        switch (node)
        {
            // H1–H3 map to the matching Notion heading; H4+ has no Notion equivalent this sprint (the clamp to
            // heading_3 is ns-7), so it degrades to a verbatim paragraph, exactly as the prior line converter did.
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
            // Only a FENCED code block becomes a Notion code block. An INDENTED code block (a plain CodeBlock —
            // FencedCodeBlock, its subtype, is matched here first) has no Notion equivalent and the old line
            // converter kept those lines verbatim, so it falls through to a verbatim paragraph — which also keeps
            // normalization idempotent, since a rendered indented block re-parses as a paragraph continuation.
            case FencedCodeBlock fenced:
                return [CodeBlock(TrimTrailingNewline(fenced.Lines.ToString()), fenced.Info ?? "")];
            // An indented code block keeps its leading whitespace verbatim (it IS the content), so it is emitted as
            // a raw-span paragraph without continuation-line cleaning — a rendered indented block re-parses to the
            // same paragraph.
            case CodeBlock code:
                return [new NotionBlock { Type = "paragraph", Paragraph = Body(Slice(code, src).TrimEnd('\n')) }];
            case ListBlock list:
            {
                // An ordered list whose item numbers are not exactly 1..n (a run starting ≠1, or with gaps) cannot
                // round-trip as numbered_list_item — Notion stores no ordinal, so FromBlocks re-sequences from 1 and
                // would rewrite the original numbers. Such a run stays verbatim paragraph lines, matching the old
                // converter's echo; only a clean 1..n run becomes real numbered items.
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

    /// <summary>A list item's own text is its first paragraph; every following child block — chiefly a nested
    /// sub-list — becomes the item's <c>children</c>, so an indented list lands as a real hierarchy.</summary>
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

    /// <summary>Whether an ordered list's items are exactly 1, 2, 3, …, n — the only shape that round-trips as
    /// numbered items, since render re-sequences from 1.</summary>
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

    /// <summary>Render a non-1..n ordered list as verbatim paragraph lines (old-converter behavior): each item is a
    /// paragraph carrying its original marker (<c>3. </c>) and text, with any nested blocks kept as children.</summary>
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
        // Numbered items are re-sequenced from 1 within each contiguous run at this level: Notion stores no
        // ordinal on a numbered_list_item, so on read we can only count, and on write a 1,2,3… list round-trips.
        var number = 0;
        foreach (var block in blocks)
        {
            // A child_page block is a nested sub-page (DR 033), not body content — never rendered into a body.
            if (block.Type == "child_page")
                continue;
            number = block.Type == "numbered_list_item" ? number + 1 : 0;
            // Prefix EVERY physical line, not just the first: a multi-line block (a code fence, a soft-wrapped
            // paragraph) nested under a list item must carry the indent on all its lines, or the un-indented
            // continuation lines break out of the item and the body stops round-tripping.
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

    /// <summary>The indentation a child list needs to nest under its parent item — the width of the parent's list
    /// marker (<c>"- "</c> = 2, <c>"1. "</c> = 3), the CommonMark minimum, so the rendered indent re-parses as a
    /// child rather than a sibling.
    /// <para>KNOWN EDGE: a non-1..n ordered list is emitted verbatim as <c>paragraph</c> blocks (see
    /// <see cref="VerbatimOrderedItems"/>), not numbered_list_item, so a child under such an item uses the default
    /// width 2 even though its rendered <c>"N. "</c> marker is 3+ wide. The child is then under-indented and
    /// un-nests one level on re-parse, so a shape like <c>"3. text\n   - sub"</c> is NOT a pass-1 fixed point — it
    /// converges after a second normalization (the sub-item becomes a top-level sibling). No synced record contains
    /// a non-1 ordered run with a nested child, so this never fires in practice; pinned by a converter test.</para></summary>
    private static int MarkerWidth(NotionBlock block, int number) =>
        block.Type == "numbered_list_item" ? number.ToString().Length + 2 : 2;

    /// <summary>The verbatim source text a Markdig block spans — kept unparsed so inline markers survive a
    /// round-trip (structure-only sprint). An empty or unset span yields the empty string.</summary>
    private static string Slice(Block block, string src)
    {
        var span = block.Span;
        if (span.Start < 0 || span.Length <= 0 || span.Start >= src.Length)
            return "";
        return src.Substring(span.Start, Math.Min(span.Length, src.Length - span.Start));
    }

    /// <summary>Strip the leading indentation off a multi-line text block's continuation lines. A paragraph or
    /// list-item text nested in a list carries the source's continuation indentation inside its span; without this
    /// the per-line render prefix would double it, and each normalization would add another level (runaway indent).
    /// The first line already starts at the content column, so only lines after it are trimmed; prose continuation
    /// whitespace is never semantic.</summary>
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

    /// <summary>Notion's code block accepts only a fixed language vocabulary and rejects anything else with a
    /// 400 that aborts the whole reconcile (e.g. the ubiquitous "csharp" fence — Notion spells it "c#"). Map
    /// the common markdown aliases to Notion's spelling, then fall back to "plain text" for any language Notion
    /// does not accept, so an unrecognised fence degrades to an un-highlighted block instead of wedging the
    /// sync. An empty fence defaults to "plain text". A repo body's original fence tag is not rewritten — the
    /// adapter's NormalizeBody re-runs this mapping so the round-trip comparison matches Notion's echo.</summary>
    private static string NormalizeLanguage(string language)
    {
        var lang = language.Trim().ToLowerInvariant();
        if (lang.Length == 0)
            return "plain text";
        if (LanguageAliases.TryGetValue(lang, out var canonical))
            lang = canonical;
        return NotionLanguages.Contains(lang) ? lang : "plain text";
    }

    /// <summary>Common markdown / highlighter fence tags that differ from Notion's spelling.</summary>
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

    /// <summary>Notion's accepted code-block languages (Notion-Version 2026-03-11). A language outside this set
    /// is rejected with a validation_error, so NormalizeLanguage maps to it or falls back to "plain text".</summary>
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
