namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using Xunit;

public class OrphanDocsRuleTests
{
    private readonly OrphanDocsRule _rule = new();
    private static readonly string BasePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "test-docs"));

    [Fact]
    public void Validate_AcceptsDirectlyLinkedDoc()
    {
        var index = CreateDoc("index.md", linksTo: ["./guide.md"]);
        var guide = CreateDoc("guide.md");
        var allDocs = new List<DocFile> { index, guide };

        var violations = _rule.Validate(guide, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsTransitivelyLinkedDoc()
    {
        var index = CreateDoc("index.md", linksTo: ["./guide.md"]);
        var guide = CreateDoc("guide.md", linksTo: ["./reference.md"]);
        var reference = CreateDoc("reference.md");
        var allDocs = new List<DocFile> { index, guide, reference };

        var violations = _rule.Validate(reference, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_WarnsOrphanDoc()
    {
        var index = CreateDoc("index.md", linksTo: []);
        var orphan = CreateDoc("orphan.md");
        var allDocs = new List<DocFile> { index, orphan };

        var violations = _rule.Validate(orphan, allDocs, BasePath).ToList();

        Assert.Single(violations);
        Assert.Equal(ViolationSeverity.Warning, violations[0].Severity);
        Assert.Contains("Orphan", violations[0].Message);
    }

    [Fact]
    public void Validate_SkipsIndexFile()
    {
        var index = CreateDoc("index.md");
        var allDocs = new List<DocFile> { index };

        var violations = _rule.Validate(index, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsWhenNoIndexExists()
    {
        var doc = CreateDoc("guide.md");
        var allDocs = new List<DocFile> { doc };

        var violations = _rule.Validate(doc, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsDeepChain()
    {
        var index = CreateDoc("index.md", linksTo: ["./a.md"]);
        var a = CreateDoc("a.md", linksTo: ["./b.md"]);
        var b = CreateDoc("b.md", linksTo: ["./c.md"]);
        var c = CreateDoc("c.md", linksTo: ["./d.md"]);
        var d = CreateDoc("d.md");
        var allDocs = new List<DocFile> { index, a, b, c, d };

        var violations = _rule.Validate(d, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_HandlesCircularLinks()
    {
        var index = CreateDoc("index.md", linksTo: ["./a.md"]);
        var a = CreateDoc("a.md", linksTo: ["./b.md"]);
        var b = CreateDoc("b.md", linksTo: ["./a.md"]); // Circular
        var allDocs = new List<DocFile> { index, a, b };

        // Both a and b should be reachable despite circular link
        Assert.Empty(_rule.Validate(a, allDocs, BasePath).ToList());
        Assert.Empty(_rule.Validate(b, allDocs, BasePath).ToList());
    }

    [Fact]
    public void Validate_AcceptsNestedLinks()
    {
        var index = CreateDoc("index.md", linksTo: ["./guides/how-to.md"]);
        var howTo = CreateDoc("guides/how-to.md");
        var allDocs = new List<DocFile> { index, howTo };

        var violations = _rule.Validate(howTo, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsExternalLinks()
    {
        var index = CreateDoc("index.md", linksTo: ["https://example.com"]);
        var orphan = CreateDoc("guide.md");
        var allDocs = new List<DocFile> { index, orphan };

        var violations = _rule.Validate(orphan, allDocs, BasePath).ToList();

        Assert.Single(violations); // Still orphan because external link doesn't count
    }

    #region Exclusions

    [Fact]
    public void Validate_SkipsTemplateFiles()
    {
        var index = CreateDoc("index.md", linksTo: []);
        var template = CreateDoc("_system/templates/agent-workflow.template.md");
        var allDocs = new List<DocFile> { index, template };

        var violations = _rule.Validate(template, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsAgentWorkspaceFiles()
    {
        var index = CreateDoc("index.md", linksTo: []);
        var agentDoc = CreateDoc("agents/Adele/workflow.md");
        var allDocs = new List<DocFile> { index, agentDoc };

        var violations = _rule.Validate(agentDoc, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsAgentModeFiles()
    {
        var index = CreateDoc("index.md", linksTo: []);
        var modeDoc = CreateDoc("agents/Adele/modes/code-writer.md");
        var allDocs = new List<DocFile> { index, modeDoc };

        var violations = _rule.Validate(modeDoc, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    #endregion

    private static DocFile CreateDoc(string relativePath, string[]? linksTo = null)
    {
        var links = (linksTo ?? []).Select(t => new LinkInfo(
            RawText: $"[link]({t})",
            DisplayText: "link",
            Target: t,
            Anchor: null,
            Type: t.StartsWith("http") ? LinkType.External : LinkType.Markdown,
            LineNumber: 1
        )).ToList();

        var fileName = Path.GetFileName(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(BasePath, relativePath));
        return new DocFile
        {
            FilePath = fullPath.Replace('\\', '/'),
            RelativePath = relativePath,
            FileName = fileName,
            Content = "# Test",
            Links = links
        };
    }
}
