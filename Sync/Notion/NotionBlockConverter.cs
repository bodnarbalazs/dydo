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
            sb.Append(BlockToLine(block)).Append('\n');
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

    /// <summary>Notion requires a known language; default unspecified fences to "plain text".</summary>
    private static string NormalizeLanguage(string language) =>
        language.Length == 0 ? "plain text" : language;
}
