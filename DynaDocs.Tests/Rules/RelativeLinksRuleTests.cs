namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using Xunit;

public class RelativeLinksRuleTests
{
    private readonly RelativeLinksRule _rule = new();

    [Fact]
    public void Validate_AcceptsValidRelativeLinks()
    {
        var doc = CreateDocWithLinks(
            new LinkInfo("[Guide](./guide.md)", "Guide", "./guide.md", null, LinkType.Markdown, 1)
        );

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_RejectsWikilinks()
    {
        var doc = CreateDocWithLinks(
            new LinkInfo("[[my-doc]]", "my-doc", "my-doc", null, LinkType.Wikilink, 1)
        );

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("Wikilink", violations[0].Message);
    }

    [Fact]
    public void Validate_RejectsAbsolutePaths()
    {
        var doc = CreateDocWithLinks(
            new LinkInfo("[Doc](/absolute/path.md)", "Doc", "/absolute/path.md", null, LinkType.Markdown, 1)
        );

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("Absolute path", violations[0].Message);
    }

    [Fact]
    public void Validate_RejectsMissingMdExtension()
    {
        var doc = CreateDocWithLinks(
            new LinkInfo("[Doc](./guide)", "Doc", "./guide", null, LinkType.Markdown, 1)
        );

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Single(violations);
        Assert.Contains("missing .md extension", violations[0].Message);
    }

    [Fact]
    public void Validate_AcceptsExternalLinks()
    {
        var doc = CreateDocWithLinks(
            new LinkInfo("[GitHub](https://github.com)", "GitHub", "https://github.com", null, LinkType.External, 1)
        );

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsAnchorOnlyLinks()
    {
        var doc = CreateDocWithLinks(
            new LinkInfo("[Section](#section)", "Section", "#section", "section", LinkType.Markdown, 1)
        );

        var violations = _rule.Validate(doc, [], "/base").ToList();

        Assert.Empty(violations);
    }

    private static DocFile CreateDocWithLinks(params LinkInfo[] links)
    {
        return new DocFile
        {
            FilePath = "/base/test.md",
            RelativePath = "test.md",
            FileName = "test.md",
            Content = "# Test",
            Links = links.ToList()
        };
    }
}
