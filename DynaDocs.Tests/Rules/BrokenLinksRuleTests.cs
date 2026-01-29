namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using DynaDocs.Services;
using Xunit;

public class BrokenLinksRuleTests
{
    private readonly BrokenLinksRule _rule;
    private static readonly string BasePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "test-docs"));

    public BrokenLinksRuleTests()
    {
        _rule = new BrokenLinksRule(new LinkResolver());
    }

    [Fact]
    public void Validate_AcceptsValidRelativeLink()
    {
        var source = CreateDoc("guide.md", links: [CreateLink("./reference.md", LinkType.Markdown)]);
        var target = CreateDoc("reference.md");
        var allDocs = new List<DocFile> { source, target };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ReportsBrokenLink()
    {
        var source = CreateDoc("guide.md", links: [CreateLink("./nonexistent.md", LinkType.Markdown)]);
        var allDocs = new List<DocFile> { source };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Single(violations);
        Assert.Contains("Broken link", violations[0].Message);
        Assert.Contains("nonexistent.md", violations[0].Message);
    }

    [Fact]
    public void Validate_SkipsExternalLinks()
    {
        var doc = CreateDoc("guide.md", links: [CreateLink("https://example.com", LinkType.External)]);

        var violations = _rule.Validate(doc, [doc], BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsWikilinks()
    {
        var doc = CreateDoc("guide.md", links: [CreateLink("SomePage", LinkType.Wikilink)]);

        var violations = _rule.Validate(doc, [doc], BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsValidAnchor()
    {
        var source = CreateDoc("guide.md", links: [CreateLinkWithAnchor("./reference.md", "section-1", LinkType.Markdown)]);
        var target = CreateDoc("reference.md", anchors: ["section-1", "section-2"]);
        var allDocs = new List<DocFile> { source, target };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ReportsInvalidAnchor()
    {
        var source = CreateDoc("guide.md", links: [CreateLinkWithAnchor("./reference.md", "nonexistent-section", LinkType.Markdown)]);
        var target = CreateDoc("reference.md", anchors: ["section-1"]);
        var allDocs = new List<DocFile> { source, target };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Single(violations);
        Assert.Contains("Broken link", violations[0].Message);
        Assert.Contains("#nonexistent-section", violations[0].Message);
    }

    [Fact]
    public void Validate_AcceptsLinkToNestedFile()
    {
        var source = CreateDoc("index.md", links: [CreateLink("./guides/how-to.md", LinkType.Markdown)]);
        var target = CreateDoc("guides/how-to.md");
        var allDocs = new List<DocFile> { source, target };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsParentDirectoryLink()
    {
        var source = CreateDoc("guides/how-to.md", links: [CreateLink("../index.md", LinkType.Markdown)]);
        var target = CreateDoc("index.md");
        var allDocs = new List<DocFile> { source, target };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ReportsMultipleBrokenLinks()
    {
        var links = new List<LinkInfo>
        {
            CreateLink("./missing1.md", LinkType.Markdown),
            CreateLink("./missing2.md", LinkType.Markdown)
        };
        var source = CreateDoc("guide.md", links: links);

        var violations = _rule.Validate(source, [source], BasePath).ToList();

        Assert.Equal(2, violations.Count);
    }

    private static DocFile CreateDoc(string relativePath, List<LinkInfo>? links = null, List<string>? anchors = null)
    {
        var fullPath = Path.GetFullPath(Path.Combine(BasePath, relativePath));
        return new DocFile
        {
            FilePath = fullPath.Replace('\\', '/'),
            RelativePath = relativePath,
            FileName = Path.GetFileName(relativePath),
            Content = "# Test",
            Links = links ?? [],
            Anchors = anchors ?? []
        };
    }

    private static LinkInfo CreateLink(string target, LinkType type)
    {
        return new LinkInfo(
            RawText: $"[link]({target})",
            DisplayText: "link",
            Target: target,
            Anchor: null,
            Type: type,
            LineNumber: 1
        );
    }

    private static LinkInfo CreateLinkWithAnchor(string target, string anchor, LinkType type)
    {
        return new LinkInfo(
            RawText: $"[link]({target}#{anchor})",
            DisplayText: "link",
            Target: target,
            Anchor: anchor,
            Type: type,
            LineNumber: 1
        );
    }
}
