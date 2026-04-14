namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Models;

public class FixFileHandlerTests : IDisposable
{
    private readonly string _testDir;

    public FixFileHandlerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-fixfile-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private DocFile CreateDocFile(string relativePath, string content, List<LinkInfo>? links = null)
    {
        var filePath = Path.Combine(_testDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content);
        return new DocFile
        {
            FilePath = filePath,
            RelativePath = relativePath,
            FileName = Path.GetFileName(relativePath),
            Content = content,
            HasFrontmatter = true,
            Links = links ?? []
        };
    }

    #region FixWikilinks

    [Fact]
    public void FixWikilinks_ResolvableWikilink_ConvertsToMarkdownLink()
    {
        var targetDoc = CreateDocFile("docs/target.md", "# Target");
        var sourceContent = "See [[target]] for details.";
        var sourceDoc = CreateDocFile("docs/source.md", sourceContent, [
            new LinkInfo("[[target]]", "target", "target", null, LinkType.Wikilink, 1)
        ]);

        var (converted, manualFixes) = FixFileHandler.FixWikilinks([sourceDoc, targetDoc]);

        Assert.Equal(1, converted);
        Assert.Empty(manualFixes);
        var updatedContent = File.ReadAllText(sourceDoc.FilePath);
        Assert.Contains("[target](./target.md)", updatedContent);
    }

    [Fact]
    public void FixWikilinks_UnresolvableWikilink_AddsToManualFixes()
    {
        var sourceContent = "See [[nonexistent]] for details.";
        var sourceDoc = CreateDocFile("docs/source.md", sourceContent, [
            new LinkInfo("[[nonexistent]]", "nonexistent", "nonexistent", null, LinkType.Wikilink, 1)
        ]);

        var (converted, manualFixes) = FixFileHandler.FixWikilinks([sourceDoc]);

        Assert.Equal(0, converted);
        Assert.Single(manualFixes);
        Assert.Contains("Ambiguous wikilink", manualFixes[0]);
    }

    [Fact]
    public void FixWikilinks_NoWikilinks_ReturnsZero()
    {
        var doc = CreateDocFile("docs/plain.md", "No wikilinks here.", [
            new LinkInfo("[link](other.md)", "link", "other.md", null, LinkType.Markdown, 1)
        ]);

        var (converted, manualFixes) = FixFileHandler.FixWikilinks([doc]);

        Assert.Equal(0, converted);
        Assert.Empty(manualFixes);
    }

    [Fact]
    public void FixWikilinks_MultipleWikilinks_MixedResolution()
    {
        var targetDoc = CreateDocFile("docs/found.md", "# Found");
        var sourceContent = "See [[found]] and [[missing]].";
        var sourceDoc = CreateDocFile("docs/source.md", sourceContent, [
            new LinkInfo("[[found]]", "found", "found", null, LinkType.Wikilink, 1),
            new LinkInfo("[[missing]]", "missing", "missing", null, LinkType.Wikilink, 1)
        ]);

        var (converted, manualFixes) = FixFileHandler.FixWikilinks([sourceDoc, targetDoc]);

        Assert.Equal(1, converted);
        Assert.Single(manualFixes);
    }

    #endregion

    #region FixNaming

    [Fact]
    public void FixNaming_NonKebabName_RenamesFile()
    {
        var doc = CreateDocFile("docs/My Task.md", "# Task");

        var (renamed, conflicts) = FixFileHandler.FixNaming([doc]);

        Assert.Equal(1, renamed);
        Assert.Empty(conflicts);
        Assert.False(File.Exists(doc.FilePath));
        Assert.True(File.Exists(Path.Combine(_testDir, "docs", "my-task.md")));
    }

    [Fact]
    public void FixNaming_TargetExists_RecordsConflictAndContinues()
    {
        var colliding = CreateDocFile("docs/My Task.md", "# First");
        var existing = CreateDocFile("docs/my-task.md", "# Existing");
        var otherNonKebab = CreateDocFile("docs/Other Thing.md", "# Other");

        var (renamed, conflicts) = FixFileHandler.FixNaming([colliding, existing, otherNonKebab]);

        Assert.Equal(1, renamed);
        Assert.Single(conflicts);
        Assert.Contains("docs/My Task.md", conflicts[0]);
        Assert.Contains("my-task.md already exists", conflicts[0]);
        Assert.True(File.Exists(colliding.FilePath));
        Assert.True(File.Exists(Path.Combine(_testDir, "docs", "other-thing.md")));
    }

    #endregion

    #region FindManualFixes

    [Fact]
    public void FindManualFixes_MissingFrontmatter_ReportsFixNeeded()
    {
        var doc = CreateDocFile("docs/no-fm.md", "No frontmatter");
        doc.HasFrontmatter = false;

        var fixes = FixFileHandler.FindManualFixes([doc]);

        Assert.Single(fixes);
        Assert.Contains("Add frontmatter", fixes[0]);
    }

    [Fact]
    public void FindManualFixes_MissingSummary_ReportsFixNeeded()
    {
        var doc = CreateDocFile("docs/no-summary.md", "---\narea: test\n---\n");
        doc.HasFrontmatter = true;
        doc.SummaryParagraph = "";

        var fixes = FixFileHandler.FindManualFixes([doc]);

        Assert.Single(fixes);
        Assert.Contains("Add summary", fixes[0]);
    }

    [Fact]
    public void FindManualFixes_ExcludedPath_Skipped()
    {
        var doc = CreateDocFile("_system/templates/test.md", "no frontmatter");
        doc.HasFrontmatter = false;

        var fixes = FixFileHandler.FindManualFixes([doc]);

        Assert.Empty(fixes);
    }

    [Fact]
    public void FindManualFixes_AgentsPath_Skipped()
    {
        var doc = CreateDocFile("agents/Grace/workflow.md", "no frontmatter");
        doc.HasFrontmatter = false;

        var fixes = FixFileHandler.FindManualFixes([doc]);

        Assert.Empty(fixes);
    }

    #endregion
}
