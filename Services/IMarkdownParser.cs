namespace DynaDocs.Services;

using DynaDocs.Models;

public interface IMarkdownParser
{
    DocFile Parse(string filePath, string basePath);
    List<LinkInfo> ExtractLinks(string content);
    Frontmatter? ExtractFrontmatter(string content);
    string? ExtractTitle(string content);
    string? ExtractSummaryParagraph(string content);
    List<string> ExtractAnchors(string content);
}
