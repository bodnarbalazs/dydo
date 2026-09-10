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
        var source = CreateDoc("guide.md", links: [LinkTestFactory.Create("./reference.md", LinkType.Markdown)]);
        var target = CreateDoc("reference.md");
        var allDocs = new List<DocFile> { source, target };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ReportsBrokenLink()
    {
        var source = CreateDoc("guide.md", links: [LinkTestFactory.Create("./nonexistent.md", LinkType.Markdown)]);
        var allDocs = new List<DocFile> { source };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Single(violations);
        Assert.Contains("Broken link", violations[0].Message);
        Assert.Contains("nonexistent.md", violations[0].Message);
    }

    [Fact]
    public void Validate_SkipsExternalLinks()
    {
        var doc = CreateDoc("guide.md", links: [LinkTestFactory.Create("https://example.com", LinkType.External)]);

        var violations = _rule.Validate(doc, [doc], BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsWikilinks()
    {
        var doc = CreateDoc("guide.md", links: [LinkTestFactory.Create("SomePage", LinkType.Wikilink)]);

        var violations = _rule.Validate(doc, [doc], BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ReportsMissingNonMarkdownAssetAndKeepsExistingAssetValid()
    {
        var root = Path.Combine(Path.GetTempPath(), "dydo-broken-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        File.WriteAllText(Path.Combine(root, "assets", "logo.png"), "fixture");
        var source = CreateDoc("guide.md", links:
        [
            LinkTestFactory.Create("assets/logo.png", LinkType.Markdown),
            LinkTestFactory.Create("assets/missing.png", LinkType.Markdown)
        ]);

        try
        {
            var violation = Assert.Single(_rule.Validate(source, [source], root));

            Assert.Equal("Broken link: assets/missing.png", violation.Message);
            Assert.Equal("All internal links must point to existing files and anchors", _rule.Description);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Validate_AcceptsValidAnchor()
    {
        var source = CreateDoc("guide.md", links: [LinkTestFactory.CreateWithAnchor("./reference.md", "section-1", LinkType.Markdown)]);
        var target = CreateDoc("reference.md", anchors: ["section-1", "section-2"]);
        var allDocs = new List<DocFile> { source, target };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ReportsInvalidAnchor()
    {
        var source = CreateDoc("guide.md", links: [LinkTestFactory.CreateWithAnchor("./reference.md", "nonexistent-section", LinkType.Markdown)]);
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
        var source = CreateDoc("index.md", links: [LinkTestFactory.Create("./guides/how-to.md", LinkType.Markdown)]);
        var target = CreateDoc("guides/how-to.md");
        var allDocs = new List<DocFile> { source, target };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsParentDirectoryLink()
    {
        var source = CreateDoc("guides/how-to.md", links: [LinkTestFactory.Create("../index.md", LinkType.Markdown)]);
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
            LinkTestFactory.Create("./missing1.md", LinkType.Markdown),
            LinkTestFactory.Create("./missing2.md", LinkType.Markdown)
        };
        var source = CreateDoc("guide.md", links: links);

        var violations = _rule.Validate(source, [source], BasePath).ToList();

        Assert.Equal(2, violations.Count);
    }

    #region Exclusions

    [Fact]
    public void Validate_SkipsTemplateFiles()
    {
        var source = CreateDoc("_system/template-additions/skill-implementer.template.md",
            links: [LinkTestFactory.Create("../../../understand/about.md", LinkType.Markdown)]);

        var violations = _rule.Validate(source, [source], BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsAutoGeneratedFiles()
    {
        var source = CreateDoc("project/generated/_index.md",
            content: "<!-- Auto-generated by 'dydo fix'. Do not edit - changes will be overwritten. -->\n[x](#missing)",
            links: [LinkTestFactory.CreateWithAnchor("", "missing", LinkType.Markdown)]);

        var violations = _rule.Validate(source, [source], BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ReportsBrokenLinksInNonGeneratedFiles()
    {
        var source = CreateDoc("project/generated/_index.md",
            content: "[x](#missing)",
            links: [LinkTestFactory.CreateWithAnchor("", "missing", LinkType.Markdown)]);

        var violations = _rule.Validate(source, [source], BasePath).ToList();

        Assert.Single(violations);
    }

    [Fact]
    public void Validate_ReportsBrokenLinksWhenProseQuotesAutoGeneratedMarker()
    {
        var source = CreateDoc("project/generated/guide.md",
            content: "The generated-file marker is `<!-- Auto-generated by 'dydo fix'. Do not edit - changes will be overwritten. -->`.\n[x](#missing)",
            links: [LinkTestFactory.CreateWithAnchor("", "missing", LinkType.Markdown)]);

        var violations = _rule.Validate(source, [source], BasePath).ToList();

        Assert.Single(violations);
    }

    #endregion

    #region Cross-folder (#0185)

    [Fact]
    public void Validate_AcceptsCrossFolderLink_WhenAllDocsContainsTarget()
    {
        var source = CreateDoc("project/decisions/0001-foo.md",
            links: [LinkTestFactory.Create("../../understand/architecture.md", LinkType.Markdown)]);
        var target = CreateDoc("understand/architecture.md");
        var allDocs = new List<DocFile> { source, target };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsTwoLevelParentLink_AcrossFolders()
    {
        var source = CreateDoc("a/b/c/source.md",
            links: [LinkTestFactory.Create("../../foo/bar.md", LinkType.Markdown)]);
        var target = CreateDoc("a/foo/bar.md");
        var allDocs = new List<DocFile> { source, target };

        var violations = _rule.Validate(source, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    #endregion

    #region Anchor-only (#0186)

    [Fact]
    public void Validate_AcceptsAnchorOnlyLink_WhenAnchorExistsOnSamePage()
    {
        var source = CreateDoc("guide.md",
            anchors: ["section"],
            links: [LinkTestFactory.CreateWithAnchor("", "section", LinkType.Markdown)]);

        var violations = _rule.Validate(source, [source], BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ReportsAnchorOnlyLink_WhenAnchorDoesNotExist()
    {
        var source = CreateDoc("guide.md",
            anchors: ["section"],
            links: [LinkTestFactory.CreateWithAnchor("", "missing", LinkType.Markdown)]);

        var violations = _rule.Validate(source, [source], BasePath).ToList();

        Assert.Single(violations);
        Assert.Contains("#missing", violations[0].Message);
    }

    [Fact]
    public void Validate_DoesNotEmitEmptyTargetError_ForAnchorOnlyLink()
    {
        var source = CreateDoc("guide.md",
            anchors: ["section"],
            links: [LinkTestFactory.CreateWithAnchor("", "missing", LinkType.Markdown)]);

        var violations = _rule.Validate(source, [source], BasePath).ToList();

        Assert.Single(violations);
        Assert.DoesNotMatch(@"Broken link:\s*$", violations[0].Message);
    }

    #endregion

    private static DocFile CreateDoc(string relativePath, string content = "# Test", List<LinkInfo>? links = null, List<string>? anchors = null)
    {
        var fullPath = Path.GetFullPath(Path.Combine(BasePath, relativePath));
        return new DocFile
        {
            FilePath = fullPath.Replace('\\', '/'),
            RelativePath = relativePath,
            FileName = Path.GetFileName(relativePath),
            Content = content,
            Links = links ?? [],
            Anchors = anchors ?? []
        };
    }

}
