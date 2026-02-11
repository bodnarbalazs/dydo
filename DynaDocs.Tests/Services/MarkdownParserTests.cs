namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;
using Xunit;

public class MarkdownParserTests
{
    private readonly MarkdownParser _parser = new();

    [Fact]
    public void ExtractFrontmatter_ParsesValidYaml()
    {
        var content = """
            ---
            area: backend
            type: guide
            ---
            # Title
            """;

        var frontmatter = _parser.ExtractFrontmatter(content);

        Assert.NotNull(frontmatter);
        Assert.Equal("backend", frontmatter.Area);
        Assert.Equal("guide", frontmatter.Type);
    }

    [Fact]
    public void ExtractFrontmatter_ReturnsNullForMissingFrontmatter()
    {
        var content = """
            # Title

            Some content here.
            """;

        var frontmatter = _parser.ExtractFrontmatter(content);

        Assert.Null(frontmatter);
    }

    [Fact]
    public void ExtractFrontmatter_ParsesDecisionFields()
    {
        var content = """
            ---
            area: platform
            type: decision
            status: accepted
            date: 2025-01-15
            ---
            # Decision Title
            """;

        var frontmatter = _parser.ExtractFrontmatter(content);

        Assert.NotNull(frontmatter);
        Assert.Equal("decision", frontmatter.Type);
        Assert.Equal("accepted", frontmatter.Status);
        Assert.Equal("2025-01-15", frontmatter.Date);
    }

    [Fact]
    public void ExtractLinks_FindsMarkdownLinks()
    {
        var content = """
            Check out [this guide](./guides/setup.md) and [the API](../reference/api.md).
            """;

        var links = _parser.ExtractLinks(content);

        Assert.Equal(2, links.Count);
        Assert.Equal("./guides/setup.md", links[0].Target);
        Assert.Equal("../reference/api.md", links[1].Target);
        Assert.All(links, l => Assert.Equal(LinkType.Markdown, l.Type));
    }

    [Fact]
    public void ExtractLinks_ParsesAnchors()
    {
        var content = "[Glossary term](../glossary.md#my-term)";

        var links = _parser.ExtractLinks(content);

        Assert.Single(links);
        Assert.Equal("../glossary.md", links[0].Target);
        Assert.Equal("my-term", links[0].Anchor);
    }

    [Fact]
    public void ExtractLinks_DetectsWikilinks()
    {
        var content = "See [[coding-standards]] for more info.";

        var links = _parser.ExtractLinks(content);

        Assert.Single(links);
        Assert.Equal(LinkType.Wikilink, links[0].Type);
        Assert.Equal("coding-standards", links[0].Target);
    }

    [Fact]
    public void ExtractLinks_DetectsWikilinksWithDisplayText()
    {
        var content = "See [[coding-standards|the standards]] for more.";

        var links = _parser.ExtractLinks(content);

        Assert.Single(links);
        Assert.Equal(LinkType.Wikilink, links[0].Type);
        Assert.Equal("coding-standards", links[0].Target);
        Assert.Equal("the standards", links[0].DisplayText);
    }

    [Fact]
    public void ExtractLinks_IdentifiesExternalLinks()
    {
        var content = "Visit [GitHub](https://github.com) for more.";

        var links = _parser.ExtractLinks(content);

        Assert.Single(links);
        Assert.Equal(LinkType.External, links[0].Type);
    }

    [Fact]
    public void ExtractLinks_SkipsLinksInCodeBlocks()
    {
        var content = """
            Real link: [Guide](./guide.md)

            ```markdown
            Example: [Example](./example.md)
            ```

            Another real link: [Reference](./reference.md)
            """;

        var links = _parser.ExtractLinks(content);

        Assert.Equal(2, links.Count);
        Assert.Equal("./guide.md", links[0].Target);
        Assert.Equal("./reference.md", links[1].Target);
        Assert.DoesNotContain(links, l => l.Target == "./example.md");
    }

    [Fact]
    public void ExtractLinks_SkipsLinksInInlineCode()
    {
        var content = """
            Use format: `[Link Text](./path.md)`

            Real link: [Real](./real.md)
            """;

        var links = _parser.ExtractLinks(content);

        Assert.Single(links);
        Assert.Equal("./real.md", links[0].Target);
    }

    [Fact]
    public void ExtractLinks_HandlesMultipleCodeBlocks()
    {
        var content = """
            [Before](./before.md)

            ```
            [Inside1](./inside1.md)
            ```

            [Middle](./middle.md)

            ```bash
            [Inside2](./inside2.md)
            ```

            [After](./after.md)
            """;

        var links = _parser.ExtractLinks(content);

        Assert.Equal(3, links.Count);
        Assert.Equal("./before.md", links[0].Target);
        Assert.Equal("./middle.md", links[1].Target);
        Assert.Equal("./after.md", links[2].Target);
    }

    [Fact]
    public void ExtractTitle_FindsFirstHeading()
    {
        var content = """
            ---
            area: general
            type: guide
            ---

            # My Document Title

            Some content here.
            """;

        var title = _parser.ExtractTitle(content);

        Assert.Equal("My Document Title", title);
    }

    [Fact]
    public void ExtractSummaryParagraph_FindsParagraphAfterTitle()
    {
        var content = """
            ---
            area: general
            type: guide
            ---

            # My Title

            This is the summary paragraph that explains what this doc is about.

            ## Next Section
            """;

        var summary = _parser.ExtractSummaryParagraph(content);

        Assert.Equal("This is the summary paragraph that explains what this doc is about.", summary);
    }

    [Fact]
    public void ExtractSummaryParagraph_ReturnsNullWhenMissing()
    {
        var content = """
            ---
            area: general
            type: guide
            ---

            # My Title

            ## Section without summary
            """;

        var summary = _parser.ExtractSummaryParagraph(content);

        Assert.Null(summary);
    }

    [Fact]
    public void ExtractFrontmatter_ParsesMustReadTrue()
    {
        var content = """
            ---
            area: understand
            type: context
            must-read: true
            ---
            # Title
            """;

        var frontmatter = _parser.ExtractFrontmatter(content);

        Assert.NotNull(frontmatter);
        Assert.True(frontmatter.MustRead);
    }

    [Fact]
    public void ExtractFrontmatter_MustReadDefaultsFalse()
    {
        var content = """
            ---
            area: understand
            type: context
            ---
            # Title
            """;

        var frontmatter = _parser.ExtractFrontmatter(content);

        Assert.NotNull(frontmatter);
        Assert.False(frontmatter.MustRead);
    }

    [Fact]
    public void ExtractFrontmatter_ParsesMustReadFalse()
    {
        var content = """
            ---
            area: understand
            type: context
            must-read: false
            ---
            # Title
            """;

        var frontmatter = _parser.ExtractFrontmatter(content);

        Assert.NotNull(frontmatter);
        Assert.False(frontmatter.MustRead);
    }

    [Fact]
    public void ExtractAnchors_FindsAllHeadings()
    {
        var content = """
            # Main Title

            ## First Section

            ### Subsection

            ## Second Section
            """;

        var anchors = _parser.ExtractAnchors(content);

        Assert.Contains("main-title", anchors);
        Assert.Contains("first-section", anchors);
        Assert.Contains("subsection", anchors);
        Assert.Contains("second-section", anchors);
    }
}
