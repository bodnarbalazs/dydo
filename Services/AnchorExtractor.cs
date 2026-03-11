namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

internal static partial class AnchorExtractor
{
    public static List<string> Extract(string content, MarkdownPipeline pipeline)
    {
        var anchors = new List<string>();
        var document = Markdown.Parse(content, pipeline);

        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            var text = GetHeadingText(heading);
            var anchor = GenerateAnchor(text);
            if (!string.IsNullOrEmpty(anchor))
            {
                anchors.Add(anchor);
            }
        }

        return anchors;
    }

    private static string GetHeadingText(HeadingBlock heading)
    {
        var text = "";
        if (heading.Inline != null)
        {
            foreach (var inline in heading.Inline)
            {
                if (inline is LiteralInline literal)
                    text += literal.Content.ToString();
                else if (inline is LinkInline link && link.FirstChild is LiteralInline linkText)
                    text += linkText.Content.ToString();
            }
        }
        return text;
    }

    private static string GenerateAnchor(string text)
    {
        return AnchorCleanupRegex()
            .Replace(text.ToLowerInvariant().Replace(' ', '-'), "");
    }

    [GeneratedRegex(@"[^\w\-]")]
    private static partial Regex AnchorCleanupRegex();
}
