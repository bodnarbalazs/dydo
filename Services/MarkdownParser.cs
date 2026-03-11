namespace DynaDocs.Services;

using System.Text.RegularExpressions;
using Markdig;
using DynaDocs.Models;

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
            FilePath = Utils.PathUtils.NormalizePath(filePath),
            RelativePath = Utils.PathUtils.NormalizePath(relativePath),
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

    public List<LinkInfo> ExtractLinks(string content) => LinkExtractor.Extract(content);

    public Frontmatter? ExtractFrontmatter(string content) => FrontmatterExtractor.Extract(content);

    public string? ExtractTitle(string content)
    {
        var contentAfterFrontmatter = FrontmatterExtractor.RemoveFrontmatter(content);
        var match = TitleRegex().Match(contentAfterFrontmatter);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public string? ExtractSummaryParagraph(string content)
    {
        var contentAfterFrontmatter = FrontmatterExtractor.RemoveFrontmatter(content);
        var lines = contentAfterFrontmatter.Split('\n');

        int i = 0;
        for (; i < lines.Length; i++)
        {
            if (lines[i].Trim().StartsWith("# ")) { i++; break; }
        }
        if (i >= lines.Length) return null;

        for (; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i])) break;
        }
        if (i >= lines.Length) return null;

        var summaryLines = new List<string>();
        for (; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#') || trimmed.StartsWith("---"))
                break;
            summaryLines.Add(trimmed);
        }

        return summaryLines.Count > 0 ? string.Join(" ", summaryLines) : null;
    }

    public List<string> ExtractAnchors(string content) => AnchorExtractor.Extract(content, _pipeline);

    [GeneratedRegex(@"^#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex TitleRegex();
}
