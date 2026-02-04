namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using DynaDocs.Models;
using DynaDocs.Utils;

public partial class MarkdownParser : IMarkdownParser
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownParser()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAutoIdentifiers()
            .Build();
    }

    public DocFile Parse(string filePath, string basePath)
    {
        var content = File.ReadAllText(filePath);
        var relativePath = Path.GetRelativePath(basePath, filePath);

        var docFile = new DocFile
        {
            FilePath = PathUtils.NormalizePath(filePath),
            RelativePath = PathUtils.NormalizePath(relativePath),
            FileName = Path.GetFileName(filePath),
            Content = content
        };

        docFile.Frontmatter = ExtractFrontmatter(content);
        docFile.HasFrontmatter = docFile.Frontmatter != null;
        docFile.Title = ExtractTitle(content);
        docFile.SummaryParagraph = ExtractSummaryParagraph(content);
        docFile.Links = ExtractLinks(content);
        docFile.Anchors = ExtractAnchors(content);

        return docFile;
    }

    public List<LinkInfo> ExtractLinks(string content)
    {
        var links = new List<LinkInfo>();
        var lines = content.Split('\n');
        var inCodeBlock = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            // Track fenced code block state
            if (line.TrimStart().StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            // Skip links inside code blocks
            if (inCodeBlock)
                continue;

            foreach (Match match in MarkdownLinkRegex().Matches(line))
            {
                // Skip links inside inline code (backticks)
                if (IsInsideInlineCode(line, match.Index))
                    continue;

                var target = match.Groups[2].Value;
                var (path, anchor) = SplitAnchor(target);

                var linkType = target.StartsWith("http://") || target.StartsWith("https://")
                    ? LinkType.External
                    : LinkType.Markdown;

                links.Add(new LinkInfo(
                    RawText: match.Value,
                    DisplayText: match.Groups[1].Value,
                    Target: path,
                    Anchor: anchor,
                    Type: linkType,
                    LineNumber: lineNumber
                ));
            }

            foreach (Match match in WikilinkRegex().Matches(line))
            {
                var path = match.Groups[1].Value;
                var (targetPath, anchor) = SplitAnchor(path);

                links.Add(new LinkInfo(
                    RawText: match.Value,
                    DisplayText: match.Groups[2].Success ? match.Groups[2].Value : path,
                    Target: targetPath,
                    Anchor: anchor,
                    Type: LinkType.Wikilink,
                    LineNumber: lineNumber
                ));
            }
        }

        return links;
    }

    public Frontmatter? ExtractFrontmatter(string content)
    {
        if (!content.StartsWith("---")) return null;

        var endIndex = content.IndexOf("---", 3);
        if (endIndex == -1) return null;

        var yaml = content.Substring(3, endIndex - 3);
        var frontmatter = new Frontmatter();

        foreach (var line in yaml.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIndex = line.IndexOf(':');
            if (colonIndex == -1) continue;

            var key = line[..colonIndex].Trim().ToLowerInvariant();
            var value = line[(colonIndex + 1)..].Trim();

            switch (key)
            {
                case "area": frontmatter.Area = value; break;
                case "type": frontmatter.Type = value; break;
                case "status": frontmatter.Status = value; break;
                case "date": frontmatter.Date = value; break;
            }
        }

        return frontmatter;
    }

    public string? ExtractTitle(string content)
    {
        var contentAfterFrontmatter = RemoveFrontmatter(content);
        var match = TitleRegex().Match(contentAfterFrontmatter);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public string? ExtractSummaryParagraph(string content)
    {
        var contentAfterFrontmatter = RemoveFrontmatter(content);
        var lines = contentAfterFrontmatter.Split('\n');

        bool foundTitle = false;
        var summaryLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (!foundTitle)
            {
                if (trimmed.StartsWith("# "))
                {
                    foundTitle = true;
                }
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed) && summaryLines.Count == 0)
                continue;

            if (trimmed.StartsWith('#') || trimmed.StartsWith("---"))
                break;

            if (string.IsNullOrWhiteSpace(trimmed) && summaryLines.Count > 0)
                break;

            summaryLines.Add(trimmed);
        }

        return summaryLines.Count > 0 ? string.Join(" ", summaryLines) : null;
    }

    public List<string> ExtractAnchors(string content)
    {
        var anchors = new List<string>();
        var document = Markdown.Parse(content, _pipeline);

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

    private static string RemoveFrontmatter(string content)
    {
        if (!content.StartsWith("---")) return content;
        var endIndex = content.IndexOf("---", 3);
        return endIndex == -1 ? content : content[(endIndex + 3)..].TrimStart();
    }

    private static (string path, string? anchor) SplitAnchor(string target)
    {
        var anchorIndex = target.IndexOf('#');
        if (anchorIndex == -1) return (target, null);
        return (target[..anchorIndex], target[(anchorIndex + 1)..]);
    }

    /// <summary>
    /// Check if a position in a line is inside inline code (backticks).
    /// Counts backticks before the position - odd count means inside code.
    /// </summary>
    private static bool IsInsideInlineCode(string line, int position)
    {
        var backtickCount = 0;
        for (int i = 0; i < position && i < line.Length; i++)
        {
            if (line[i] == '`')
                backtickCount++;
        }
        return backtickCount % 2 == 1;
    }

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]")]
    private static partial Regex WikilinkRegex();

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"[^\w\-]")]
    private static partial Regex AnchorCleanupRegex();
}
