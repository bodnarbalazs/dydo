namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using Xunit;

public class OrphanDocsRuleTests
{
    private readonly OrphanDocsRule _rule = new();
    private static readonly string BasePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "test-docs"));

    #region Basic Reachability

    [Fact]
    public void Validate_AcceptsDirectlyLinkedDoc()
    {
        var hub = CreateDoc("guides/_index.md", linksTo: ["./setup.md"]);
        var guide = CreateDoc("guides/setup.md");
        var allDocs = new List<DocFile> { hub, guide };

        var violations = _rule.Validate(guide, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_AcceptsTransitivelyLinkedDoc()
    {
        var hub = CreateDoc("guides/_index.md", linksTo: ["./getting-started.md"]);
        var gettingStarted = CreateDoc("guides/getting-started.md", linksTo: ["./advanced.md"]);
        var advanced = CreateDoc("guides/advanced.md");
        var allDocs = new List<DocFile> { hub, gettingStarted, advanced };

        var violations = _rule.Validate(advanced, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_WarnsOrphanDoc()
    {
        var hub = CreateDoc("guides/_index.md", linksTo: []);
        var orphan = CreateDoc("guides/orphan.md");
        var allDocs = new List<DocFile> { hub, orphan };

        var violations = _rule.Validate(orphan, allDocs, BasePath).ToList();

        Assert.Single(violations);
        Assert.Equal(ViolationSeverity.Warning, violations[0].Severity);
        Assert.Contains("Orphan", violations[0].Message);
        Assert.Contains("guides/_index.md", violations[0].Message);
    }

    [Fact]
    public void Validate_AcceptsDeepChain()
    {
        var hub = CreateDoc("project/_index.md", linksTo: ["./a.md"]);
        var a = CreateDoc("project/a.md", linksTo: ["./b.md"]);
        var b = CreateDoc("project/b.md", linksTo: ["./c.md"]);
        var c = CreateDoc("project/c.md", linksTo: ["./d.md"]);
        var d = CreateDoc("project/d.md");
        var allDocs = new List<DocFile> { hub, a, b, c, d };

        var violations = _rule.Validate(d, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_HandlesCircularLinks()
    {
        var hub = CreateDoc("reference/_index.md", linksTo: ["./a.md"]);
        var a = CreateDoc("reference/a.md", linksTo: ["./b.md"]);
        var b = CreateDoc("reference/b.md", linksTo: ["./a.md"]); // Circular
        var allDocs = new List<DocFile> { hub, a, b };

        // Both a and b should be reachable despite circular link
        Assert.Empty(_rule.Validate(a, allDocs, BasePath).ToList());
        Assert.Empty(_rule.Validate(b, allDocs, BasePath).ToList());
    }

    [Fact]
    public void Validate_SkipsExternalLinks()
    {
        var hub = CreateDoc("guides/_index.md", linksTo: ["https://example.com"]);
        var orphan = CreateDoc("guides/setup.md");
        var allDocs = new List<DocFile> { hub, orphan };

        var violations = _rule.Validate(orphan, allDocs, BasePath).ToList();

        Assert.Single(violations); // Still orphan because external link doesn't count
    }

    #endregion

    #region Main Folder Filtering

    [Fact]
    public void Validate_SkipsRootFiles()
    {
        // Files at root level (welcome.md, glossary.md) should not be checked
        var hub = CreateDoc("guides/_index.md", linksTo: []);
        var rootDoc = CreateDoc("welcome.md");
        var allDocs = new List<DocFile> { hub, rootDoc };

        var violations = _rule.Validate(rootDoc, allDocs, BasePath).ToList();

        Assert.Empty(violations); // Root files are not checked
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
    public void Validate_SkipsHubFiles()
    {
        var hub = CreateDoc("guides/_index.md", linksTo: []);
        var allDocs = new List<DocFile> { hub };

        var violations = _rule.Validate(hub, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsAssetsFolder()
    {
        var hub = CreateDoc("guides/_index.md", linksTo: []);
        var asset = CreateDoc("_assets/diagram.md"); // .md file in assets folder
        var allDocs = new List<DocFile> { hub, asset };

        var violations = _rule.Validate(asset, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsWhenNoHubExists()
    {
        // No _index.md in guides folder - can't validate
        var doc = CreateDoc("guides/setup.md");
        var allDocs = new List<DocFile> { doc };

        var violations = _rule.Validate(doc, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_ChecksAllFourMainFolders()
    {
        // Each folder has its own hub
        var hubGuides = CreateDoc("guides/_index.md", linksTo: ["./setup.md"]);
        var hubProject = CreateDoc("project/_index.md", linksTo: []); // No links
        var hubReference = CreateDoc("reference/_index.md", linksTo: []);
        var hubUnderstand = CreateDoc("understand/_index.md", linksTo: []);

        var guidesDoc = CreateDoc("guides/setup.md");
        var projectDoc = CreateDoc("project/adr-001.md"); // Orphan

        var allDocs = new List<DocFile>
        {
            hubGuides, hubProject, hubReference, hubUnderstand,
            guidesDoc, projectDoc
        };

        // guides/setup.md is linked - no violation
        Assert.Empty(_rule.Validate(guidesDoc, allDocs, BasePath).ToList());

        // project/adr-001.md is not linked - violation
        var violations = _rule.Validate(projectDoc, allDocs, BasePath).ToList();
        Assert.Single(violations);
        Assert.Contains("project/_index.md", violations[0].Message);
    }

    [Fact]
    public void Validate_ChecksSubfolderFromMainHub()
    {
        // project/tasks/task-1.md should be reachable from project/_index.md
        var hub = CreateDoc("project/_index.md", linksTo: ["./tasks/task-1.md"]);
        var task = CreateDoc("project/tasks/task-1.md");
        var allDocs = new List<DocFile> { hub, task };

        var violations = _rule.Validate(task, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_WarnsOrphanInSubfolder()
    {
        var hub = CreateDoc("project/_index.md", linksTo: []);
        var task = CreateDoc("project/tasks/orphan-task.md");
        var allDocs = new List<DocFile> { hub, task };

        var violations = _rule.Validate(task, allDocs, BasePath).ToList();

        Assert.Single(violations);
        Assert.Contains("project/_index.md", violations[0].Message);
    }

    [Fact]
    public void Validate_TransitiveReachabilityThroughSubfolder()
    {
        // Hub -> subfolder index -> doc in subfolder
        var hub = CreateDoc("project/_index.md", linksTo: ["./tasks/_index.md"]);
        var subfolderHub = CreateDoc("project/tasks/_index.md", linksTo: ["./task-1.md"]);
        var task = CreateDoc("project/tasks/task-1.md");
        var allDocs = new List<DocFile> { hub, subfolderHub, task };

        var violations = _rule.Validate(task, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsOtherFolders()
    {
        // A folder that's not one of the four main folders should be skipped
        var hub = CreateDoc("guides/_index.md", linksTo: []);
        var customDoc = CreateDoc("custom-folder/doc.md");
        var allDocs = new List<DocFile> { hub, customDoc };

        var violations = _rule.Validate(customDoc, allDocs, BasePath).ToList();

        Assert.Empty(violations); // Not in main folders - not checked
    }

    #endregion

    #region Exclusions

    [Fact]
    public void Validate_SkipsTemplateFiles()
    {
        var hub = CreateDoc("guides/_index.md", linksTo: []);
        var template = CreateDoc("_system/templates/agent-workflow.template.md");
        var allDocs = new List<DocFile> { hub, template };

        var violations = _rule.Validate(template, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsAgentWorkspaceFiles()
    {
        var hub = CreateDoc("guides/_index.md", linksTo: []);
        var agentDoc = CreateDoc("agents/Adele/workflow.md");
        var allDocs = new List<DocFile> { hub, agentDoc };

        var violations = _rule.Validate(agentDoc, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_SkipsAgentModeFiles()
    {
        var hub = CreateDoc("guides/_index.md", linksTo: []);
        var modeDoc = CreateDoc("agents/Adele/modes/code-writer.md");
        var allDocs = new List<DocFile> { hub, modeDoc };

        var violations = _rule.Validate(modeDoc, allDocs, BasePath).ToList();

        Assert.Empty(violations);
    }

    #endregion

    #region Cross-Folder Links

    [Fact]
    public void Validate_CrossFolderLinkDoesNotMakeTargetReachable()
    {
        // A link from guides/ to reference/ doesn't make the reference doc reachable
        // Each doc must be reachable from its OWN folder's hub
        var hubGuides = CreateDoc("guides/_index.md", linksTo: ["./setup.md"]);
        var hubReference = CreateDoc("reference/_index.md", linksTo: []); // No links
        var guidesDoc = CreateDoc("guides/setup.md", linksTo: ["../reference/api.md"]);
        var referenceDoc = CreateDoc("reference/api.md");

        var allDocs = new List<DocFile> { hubGuides, hubReference, guidesDoc, referenceDoc };

        // guides/setup.md is reachable from guides/_index.md
        Assert.Empty(_rule.Validate(guidesDoc, allDocs, BasePath).ToList());

        // reference/api.md is NOT reachable from reference/_index.md (only linked from guides)
        var violations = _rule.Validate(referenceDoc, allDocs, BasePath).ToList();
        Assert.Single(violations);
        Assert.Contains("reference/_index.md", violations[0].Message);
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
