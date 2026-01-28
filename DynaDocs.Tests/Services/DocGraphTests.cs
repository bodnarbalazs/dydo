namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;
using Xunit;

public class DocGraphTests
{
    private readonly DocGraph _graph = new();

    [Fact]
    public void Build_CreatesGraphFromDocs()
    {
        var docs = CreateDocsWithLinks();

        _graph.Build(docs, "/base");

        Assert.True(_graph.HasDoc("index.md"));
        Assert.True(_graph.HasDoc("guide.md"));
        Assert.True(_graph.HasDoc("reference.md"));
    }

    [Fact]
    public void GetIncoming_ReturnsDocsLinkingToTarget()
    {
        var docs = CreateDocsWithLinks();
        _graph.Build(docs, "/base");

        var incoming = _graph.GetIncoming("reference.md");

        Assert.Single(incoming);
        Assert.Equal("guide.md", incoming[0].Doc);
        Assert.Equal(5, incoming[0].LineNumber);
    }

    [Fact]
    public void GetIncoming_ReturnsMultipleIncoming()
    {
        var docs = CreateDocsWithLinks();
        _graph.Build(docs, "/base");

        var incoming = _graph.GetIncoming("guide.md");

        Assert.Equal(2, incoming.Count);
        var sources = incoming.Select(x => x.Doc).OrderBy(x => x).ToList();
        Assert.Contains("index.md", sources);
        Assert.Contains("reference.md", sources);
    }

    [Fact]
    public void GetIncoming_ReturnsEmptyForNoIncoming()
    {
        var docs = CreateDocsWithLinks();
        _graph.Build(docs, "/base");

        var incoming = _graph.GetIncoming("orphan.md");

        Assert.Empty(incoming);
    }

    [Fact]
    public void GetWithinDegree_ReturnsDegreeOneLinks()
    {
        var docs = CreateDocsWithLinks();
        _graph.Build(docs, "/base");

        var result = _graph.GetWithinDegree("index.md", 1);

        Assert.Single(result);
        Assert.Equal("guide.md", result[0].Doc);
        Assert.Equal(1, result[0].Degree);
    }

    [Fact]
    public void GetWithinDegree_ReturnsDegreeTwoLinks()
    {
        var docs = CreateDocsWithLinks();
        _graph.Build(docs, "/base");

        var result = _graph.GetWithinDegree("index.md", 2);

        Assert.Equal(2, result.Count);
        var degree1 = result.Where(x => x.Degree == 1).Select(x => x.Doc).ToList();
        var degree2 = result.Where(x => x.Degree == 2).Select(x => x.Doc).ToList();

        Assert.Contains("guide.md", degree1);
        Assert.Contains("reference.md", degree2);
    }

    [Fact]
    public void GetWithinDegree_DoesNotIncludeStartDoc()
    {
        var docs = CreateDocsWithLinks();
        _graph.Build(docs, "/base");

        var result = _graph.GetWithinDegree("index.md", 2);

        Assert.DoesNotContain(result, x => x.Doc == "index.md");
    }

    [Fact]
    public void GetWithinDegree_HandlesCircularLinks()
    {
        var docs = CreateDocsWithLinks();
        _graph.Build(docs, "/base");

        var result = _graph.GetWithinDegree("guide.md", 3);

        // guide -> reference -> guide (circular), but we should not revisit
        var guideCount = result.Count(x => x.Doc == "guide.md");
        Assert.Equal(0, guideCount); // Start doc not included, and not revisited
    }

    [Fact]
    public void GetWithinDegree_ReturnsEmptyForNoLinks()
    {
        var docs = CreateDocsWithLinks();
        _graph.Build(docs, "/base");

        var result = _graph.GetWithinDegree("orphan.md", 2);

        Assert.Empty(result);
    }

    [Fact]
    public void HasDoc_ReturnsTrueForExistingDoc()
    {
        var docs = CreateDocsWithLinks();
        _graph.Build(docs, "/base");

        Assert.True(_graph.HasDoc("index.md"));
        Assert.True(_graph.HasDoc("INDEX.MD")); // Case insensitive
    }

    [Fact]
    public void HasDoc_ReturnsFalseForNonExistingDoc()
    {
        var docs = CreateDocsWithLinks();
        _graph.Build(docs, "/base");

        Assert.False(_graph.HasDoc("nonexistent.md"));
    }

    [Fact]
    public void Build_IgnoresExternalLinks()
    {
        var doc = CreateDoc("test.md", [
            new LinkInfo("[ext](https://example.com)", "ext", "https://example.com", null, LinkType.External, 1)
        ]);

        _graph.Build([doc], "/base");

        var result = _graph.GetWithinDegree("test.md", 1);
        Assert.Empty(result);
    }

    [Fact]
    public void Build_HandlesNestedPaths()
    {
        var indexDoc = CreateDoc("index.md", [
            new LinkInfo("[guide](./guides/setup.md)", "guide", "./guides/setup.md", null, LinkType.Markdown, 3)
        ]);
        var guideDoc = CreateDoc("guides/setup.md", [], "guides/setup.md");

        _graph.Build([indexDoc, guideDoc], "/base");

        var result = _graph.GetWithinDegree("index.md", 1);
        Assert.Single(result);
        Assert.Contains("guides/setup.md", result[0].Doc);
    }

    private static List<DocFile> CreateDocsWithLinks()
    {
        // index.md -> guide.md
        // guide.md -> reference.md
        // reference.md -> guide.md (circular)
        // orphan.md (no links)

        var indexDoc = CreateDoc("index.md", [
            new LinkInfo("[Guide](./guide.md)", "Guide", "./guide.md", null, LinkType.Markdown, 3)
        ]);

        var guideDoc = CreateDoc("guide.md", [
            new LinkInfo("[Ref](./reference.md)", "Ref", "./reference.md", null, LinkType.Markdown, 5)
        ]);

        var refDoc = CreateDoc("reference.md", [
            new LinkInfo("[Guide](./guide.md)", "Guide", "./guide.md", null, LinkType.Markdown, 2)
        ]);

        var orphanDoc = CreateDoc("orphan.md", []);

        return [indexDoc, guideDoc, refDoc, orphanDoc];
    }

    private static DocFile CreateDoc(string relativePath, List<LinkInfo> links, string? actualRelativePath = null)
    {
        return new DocFile
        {
            FilePath = $"/base/{actualRelativePath ?? relativePath}",
            RelativePath = actualRelativePath ?? relativePath,
            FileName = Path.GetFileName(relativePath),
            Content = "# Test",
            Links = links
        };
    }
}
