namespace DynaDocs.Sync.Notion;

using System.Text;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// Best-effort, intentionally lossy markdown ⇄ Notion block conversion (Decision 025 §6, slice
/// brief §4). Write: each non-blank line becomes one block — headings (#/##/###), bulleted list
/// items (-/*), fenced code, else a paragraph; blank lines separate but produce no block. Read:
/// each block renders back to one markdown line. Rich inline formatting and nesting are dropped —
/// the structured frontmatter↔property path carries the reliable data; bodies are a 3-way *text*
/// merge over this approximation.
/// </summary>
public static class NotionBlockConverter
{
    public static List<NotionBlock> ToBlocks(string markdown)
    {
        var blocks = new List<NotionBlock>();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        var inFence = false;
        var fenceLang = "";
        var fenceLines = new List<string>();

        foreach (var line in lines)
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (inFence)
                {
                    blocks.Add(CodeBlock(string.Join("\n", fenceLines), fenceLang));
                    fenceLines.Clear();
                    inFence = false;
                }
                else
                {
                    inFence = true;
                    fenceLang = line[3..].Trim();
                }
                continue;
            }

            if (inFence)
            {
                fenceLines.Add(line);
                continue;
            }

            if (line.Trim().Length == 0)
                continue;

            blocks.Add(LineToBlock(line));
        }

        // An unterminated fence still yields a code block rather than losing the content.
        if (inFence)
            blocks.Add(CodeBlock(string.Join("\n", fenceLines), fenceLang));

        return blocks;
    }

    public static string FromBlocks(IReadOnlyList<NotionBlock> blocks)
    {
        var sb = new StringBuilder();
        foreach (var block in blocks)
        {
            // A child_page block is a nested sub-page (DR 033), not body content — the docs mirror keeps
            // structure repo-owned, so it never renders into or round-trips through a page's body text.
            if (block.Type == "child_page")
                continue;
            sb.Append(BlockToLine(block)).Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static NotionBlock LineToBlock(string line)
    {
        if (line.StartsWith("### ", StringComparison.Ordinal))
            return new NotionBlock { Type = "heading_3", Heading3 = Body(line[4..]) };
        if (line.StartsWith("## ", StringComparison.Ordinal))
            return new NotionBlock { Type = "heading_2", Heading2 = Body(line[3..]) };
        if (line.StartsWith("# ", StringComparison.Ordinal))
            return new NotionBlock { Type = "heading_1", Heading1 = Body(line[2..]) };
        if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            return new NotionBlock { Type = "bulleted_list_item", BulletedListItem = Body(line[2..]) };
        return new NotionBlock { Type = "paragraph", Paragraph = Body(line) };
    }

    private static string BlockToLine(NotionBlock block) => block.Type switch
    {
        "heading_1" => "# " + Text(block.Heading1),
        "heading_2" => "## " + Text(block.Heading2),
        "heading_3" => "### " + Text(block.Heading3),
        "bulleted_list_item" => "- " + Text(block.BulletedListItem),
        "code" => Fence(block.Code),
        _ => Text(block.Paragraph),
    };

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
