namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;
using Xunit;

public class LinkResolverTests
{
    private readonly LinkResolver _resolver = new();
    private static readonly string BasePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "test-docs"));

    #region ResolveLink Tests

    [Fact]
    public void ResolveLink_AcceptsExternalLink()
    {
        var doc = CreateDoc("test.md");
        var link = CreateLink("https://example.com", LinkType.External);

        var result = _resolver.ResolveLink(doc, link, [], BasePath);

        Assert.True(result);
    }

    [Fact]
    public void ResolveLink_AcceptsValidRelativeLink()
    {
        var source = CreateDoc("guide.md");
        var target = CreateDoc("reference.md");
        var link = CreateLink("./reference.md", LinkType.Markdown);
        var allDocs = new List<DocFile> { source, target };

        var result = _resolver.ResolveLink(source, link, allDocs, BasePath);

        Assert.True(result);
    }

    [Fact]
    public void ResolveLink_RejectsBrokenLink()
    {
        var source = CreateDoc("test.md");
        var link = CreateLink("./nonexistent.md", LinkType.Markdown);

        var result = _resolver.ResolveLink(source, link, [source], BasePath);

        Assert.False(result);
    }

    [Fact]
    public void ResolveLink_AcceptsValidAnchor()
    {
        var source = CreateDoc("guide.md");
        var target = CreateDoc("reference.md", anchors: ["section-1", "section-2"]);
        var link = CreateLinkWithAnchor("./reference.md", "section-1", LinkType.Markdown);
        var allDocs = new List<DocFile> { source, target };

        var result = _resolver.ResolveLink(source, link, allDocs, BasePath);

        Assert.True(result);
    }

    [Fact]
    public void ResolveLink_RejectsInvalidAnchor()
    {
        var source = CreateDoc("guide.md");
        var target = CreateDoc("reference.md", anchors: ["section-1"]);
        var link = CreateLinkWithAnchor("./reference.md", "nonexistent", LinkType.Markdown);
        var allDocs = new List<DocFile> { source, target };

        var result = _resolver.ResolveLink(source, link, allDocs, BasePath);

        Assert.False(result);
    }

    [Fact]
    public void ResolveLink_AcceptsParentDirectoryLink()
    {
        var source = CreateDoc("guides/how-to.md");
        var target = CreateDoc("index.md");
        var link = CreateLink("../index.md", LinkType.Markdown);
        var allDocs = new List<DocFile> { source, target };

        var result = _resolver.ResolveLink(source, link, allDocs, BasePath);

        Assert.True(result);
    }

    [Fact]
    public void ResolveLink_AcceptsNestedPathLink()
    {
        var source = CreateDoc("index.md");
        var target = CreateDoc("guides/backend/api.md");
        var link = CreateLink("./guides/backend/api.md", LinkType.Markdown);
        var allDocs = new List<DocFile> { source, target };

        var result = _resolver.ResolveLink(source, link, allDocs, BasePath);

        Assert.True(result);
    }

    #endregion

    #region FindFileByName Tests

    [Fact]
    public void FindFileByName_ReturnsUniqueMatch()
    {
        var doc = CreateDoc("guides/how-to.md");
        var allDocs = new List<DocFile> { doc };

        var result = _resolver.FindFileByName("how-to.md", allDocs);

        Assert.Equal("guides/how-to.md", result);
    }

    [Fact]
    public void FindFileByName_ReturnsNullForMultipleMatches()
    {
        var doc1 = CreateDoc("guides/how-to.md");
        var doc2 = CreateDoc("reference/how-to.md");
        var allDocs = new List<DocFile> { doc1, doc2 };

        var result = _resolver.FindFileByName("how-to.md", allDocs);

        Assert.Null(result);
    }

    [Fact]
    public void FindFileByName_ReturnsNullForNoMatch()
    {
        var doc = CreateDoc("guide.md");
        var allDocs = new List<DocFile> { doc };

        var result = _resolver.FindFileByName("nonexistent.md", allDocs);

        Assert.Null(result);
    }

    [Fact]
    public void FindFileByName_MatchesCaseInsensitive()
    {
        var doc = CreateDoc("guides/HowTo.md");
        var allDocs = new List<DocFile> { doc };

        var result = _resolver.FindFileByName("howto.md", allDocs);

        Assert.Equal("guides/HowTo.md", result);
    }

    [Fact]
    public void FindFileByName_MatchesWithoutExtension()
    {
        var doc = CreateDoc("guides/how-to.md");
        var allDocs = new List<DocFile> { doc };

        var result = _resolver.FindFileByName("how-to", allDocs);

        Assert.Equal("guides/how-to.md", result);
    }

    #endregion

    #region ValidateAnchor Tests

    [Fact]
    public void ValidateAnchor_AcceptsExistingAnchor()
    {
        var doc = CreateDoc("test.md", anchors: ["section-1", "section-2"]);

        var result = _resolver.ValidateAnchor("section-1", doc);

        Assert.True(result);
    }

    [Fact]
    public void ValidateAnchor_RejectsMissingAnchor()
    {
        var doc = CreateDoc("test.md", anchors: ["section-1"]);

        var result = _resolver.ValidateAnchor("nonexistent", doc);

        Assert.False(result);
    }

    [Fact]
    public void ValidateAnchor_AcceptsNullAnchor()
    {
        var doc = CreateDoc("test.md", anchors: []);

        var result = _resolver.ValidateAnchor(null, doc);

        Assert.True(result);
    }

    [Fact]
    public void ValidateAnchor_MatchesCaseInsensitive()
    {
        var doc = CreateDoc("test.md", anchors: ["Section-One"]);

        var result = _resolver.ValidateAnchor("section-one", doc);

        Assert.True(result);
    }

    #endregion

    #region Helpers

    private static DocFile CreateDoc(string relativePath, List<string>? anchors = null)
    {
        var fullPath = Path.GetFullPath(Path.Combine(BasePath, relativePath));
        return new DocFile
        {
            FilePath = fullPath.Replace('\\', '/'),
            RelativePath = relativePath,
            FileName = Path.GetFileName(relativePath),
            Content = "# Test",
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

    #endregion
}
