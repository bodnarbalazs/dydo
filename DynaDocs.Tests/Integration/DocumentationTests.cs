namespace DynaDocs.Tests.Integration;

using System.Text.RegularExpressions;
using DynaDocs.Commands;

[Collection("Integration")]
public partial class DocumentationTests : IntegrationTestBase
{
    #region Check

    [Fact]
    public async Task Check_FreshLinearNativeScaffold_PassesWithoutRepositoryWorkHierarchy()
    {
        var init = await InitProjectAsync("none");
        init.AssertSuccess();

        var check = await RunAsync(CheckCommand.Create());
        check.AssertSuccess();

        var projectRoot = Path.Combine(TestDir, "dydo", "project");
        foreach (var retired in new[] { "tasks", "issues", "campaigns", "sprints", "slices", "backlog" })
            Assert.False(Directory.Exists(Path.Combine(projectRoot, retired)));

        var futureFeaturesRoot = Path.Combine(projectRoot, "future-features");
        var futureFeatureFiles = Directory.GetFiles(futureFeaturesRoot, "*.md")
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToArray();
        Assert.Equal(new[] { "_future-features.md" }, futureFeatureFiles);

        AssertLegacyFutureFeatureGuide(File.ReadAllText(Path.Combine(futureFeaturesRoot, "_future-features.md")));
    }

    [Fact]
    public void RepositoryFutureFeatureGuide_SendsNewIntakeToLinear()
    {
        AssertLegacyFutureFeatureGuide(File.ReadAllText(Path.Combine(
            RepositoryRoot(), "dydo", "project", "future-features", "_future-features.md")));
    }

    [Fact]
    public void RepositoryLegacyFutureFeatureRecords_ClaimNoRepoNativeIntake()
    {
        var folder = Path.Combine(RepositoryRoot(), "dydo", "project", "future-features");
        var records = Directory.GetFiles(folder, "*.md")
            .Where(path => Path.GetFileName(path) != "_future-features.md")
            .ToArray();
        Assert.NotEmpty(records);

        foreach (var record in records)
            Assert.False(File.ReadAllText(record).Contains("repo-native", StringComparison.OrdinalIgnoreCase),
                $"{Path.GetFileName(record)} still calls a FutureFeature repo-native");
    }

    [Fact]
    public async Task Check_ProjectWithoutFutureFeaturesFolder_Passes()
    {
        // LC's shape: every FutureFeature lives in Linear and the folder does not exist.
        (await InitProjectAsync("none")).AssertSuccess();
        Directory.Delete(Path.Combine(DydoDir, "project", "future-features"), recursive: true);

        var check = await RunAsync(CheckCommand.Create());

        check.AssertSuccess();
        Assert.DoesNotContain("future-features", check.Stdout + check.Stderr);
        Assert.DoesNotContain("FutureFeature", check.Stdout + check.Stderr);
    }

    [Fact]
    public async Task Check_FutureFeaturesFolderHoldingOnlyItsMetaFile_Passes()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        var folder = Path.Combine(DydoDir, "project", "future-features");
        Assert.Equal(new[] { "_future-features.md" },
            Directory.GetFileSystemEntries(folder).Select(Path.GetFileName).ToArray());

        var check = await RunAsync(CheckCommand.Create());

        check.AssertSuccess();
        Assert.DoesNotContain("FutureFeature", check.Stdout + check.Stderr);
    }

    [Fact]
    public async Task FreshInit_EveryCitedDydoDocumentShips()
    {
        (await InitProjectAsync("none")).AssertSuccess();

        var missing = Directory.EnumerateFiles(DydoDir, "*", SearchOption.AllDirectories)
            .SelectMany(file => DydoDocumentPath().Matches(File.ReadAllText(file))
                .Select(match => (Citer: Path.GetRelativePath(TestDir, file), Cited: match.Value)))
            .Where(citation => !File.Exists(Path.Combine(TestDir, citation.Cited)))
            .Select(citation => $"{citation.Citer} -> {citation.Cited}")
            .Distinct()
            .ToArray();

        Assert.Empty(missing);
    }

    [GeneratedRegex(@"(?<![\w./-])dydo/[A-Za-z0-9_./-]+\.md")]
    private static partial Regex DydoDocumentPath();

    /// <summary>
    /// DR 044 and DR 047: a new FutureFeature is a Linear ticket in status `FutureFeature`; the
    /// folder only keeps legacy records until their migration is verified, and `dydo check`
    /// validates each of them now.
    /// </summary>
    private static void AssertLegacyFutureFeatureGuide(string guide)
    {
        Assert.Contains("A new FutureFeature is a Linear ticket in status `FutureFeature`", guide);
        Assert.Contains("stays until its migration to Linear is verified", guide);
        Assert.Contains("`dydo check` enforces this shape on every legacy record.", guide);
        foreach (var stale in new[]
                 {
                     "repo-native", "stay out of", "stay in this folder", "Project 3", "Strict content validation begins",
                 })
            Assert.DoesNotContain(stale, guide);
    }

    [Fact]
    public async Task Check_ValidDocs_Passes()
    {
        // Not init: it creates docs with known issues.
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo"));
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "guides"));

        WriteFile("dydo/index.md", """
            ---
            area: general
            type: hub
            ---

            # Index

            Documentation index.

            ## Guides

            - [coding-standards](./guides/coding-standards.md)
            """);

        WriteFile("dydo/guides/coding-standards.md", """
            ---
            area: guides
            type: guide
            ---

            # Coding Standards

            Follow these standards.
            """);

        var result = await CheckAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("All checks passed");
    }

    [Fact]
    public async Task Check_ReportsIssues_WhenPresent()
    {
        await InitProjectAsync("none");

        WriteFile("dydo/guides/test-broken.md", """
            ---
            area: guides
            type: guide
            ---

            # Test

            This has a [broken link](./does-not-exist.md).
            """);

        var result = await CheckAsync();

        result.AssertExitCode(1);
        result.AssertStdoutContains("Found");
        result.AssertStdoutContains("errors");
    }

    [Fact]
    public async Task Check_InvalidFrontmatter_ReportsErrors()
    {
        await InitProjectAsync("none");

        WriteFile("dydo/guides/bad-doc.md", "# Bad Doc\n\nNo frontmatter here.");

        var result = await CheckAsync();

        result.AssertExitCode(1);
        result.AssertStdoutContains("bad-doc.md");
        result.AssertStdoutContains("frontmatter");
    }

    [Fact]
    public async Task Check_SpecificPath_ChecksOnlyThatPath()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo"));
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "understand"));
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo", "guides"));

        WriteFile("dydo/understand/about.md", """
            ---
            area: understand
            type: concept
            ---

            # About

            This is valid.
            """);

        WriteFile("dydo/guides/invalid.md", "# No frontmatter");

        var result = await CheckAsync("dydo/understand");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Check_BrokenLinks_ReportsErrors()
    {
        await InitProjectAsync("none");

        WriteFile("dydo/guides/broken-links.md", """
            ---
            area: guides
            type: how-to
            ---

            # Broken Links

            This doc has a [broken link](./nonexistent.md).
            """);

        var result = await CheckAsync();

        result.AssertExitCode(1);
        result.AssertStdoutContains("broken");
    }

    [Fact]
    public async Task Check_NoDocsFolder_Fails()
    {
        var command = CheckCommand.Create();
        var result = await RunAsync(command, "nonexistent/path");

        result.AssertExitCode(2);
        result.AssertStderrContains("not found");
    }

    #endregion

    #region Fix

    [Fact]
    public async Task Fix_RenamesNonKebabCase()
    {
        await InitProjectAsync("none");

        WriteFile("dydo/guides/BadName.md", """
            ---
            area: guides
            type: how-to
            ---

            # Bad Name

            This file has a bad name.
            """);

        var result = await FixAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Renamed");
        AssertFileExists("dydo/guides/bad-name.md");
    }

    [Fact]
    public async Task Fix_ReportsManualFixNeeded()
    {
        await InitProjectAsync("none");
        WriteFile("dydo/guides/needs-manual.md", "# Needs Manual Fix\n");

        var result = await FixAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("NEEDS MANUAL FIX");
        result.AssertStdoutContains("needs-manual.md");
    }

    [Fact]
    public async Task Fix_DoesNotCreateMissingHubOrMetaFiles()
    {
        await InitProjectAsync("none");
        WriteFile("dydo/guides/tutorials/first.md", "---\narea: guides\ntype: guide\n---\n\n# First\n");

        var result = await FixAsync();

        result.AssertSuccess();
        AssertFileNotExists("dydo/guides/tutorials/_index.md");
        AssertFileNotExists("dydo/guides/tutorials/_tutorials.md");
    }

    [Fact]
    public async Task Fix_PreservesCustomNavigationAndSibling()
    {
        await InitProjectAsync("none");
        const string hub = "# Custom navigation\n";
        const string meta = "---\narea: guides\ntype: folder-meta\n---\n\n# Tutorials\n";
        WriteFile("dydo/guides/tutorials/_index.md", hub);
        WriteFile("dydo/guides/tutorials/_tutorials.md", meta);
        WriteFile("dydo/guides/tutorials/notes.md", "---\narea: guides\ntype: guide\n---\n\n# Notes\n");

        (await FixAsync()).AssertSuccess();

        Assert.Equal(hub, ReadFile("dydo/guides/tutorials/_index.md"));
        Assert.Equal(meta, ReadFile("dydo/guides/tutorials/_tutorials.md"));
        AssertFileExists("dydo/guides/tutorials/notes.md");
    }

    #region Fix Exclusions

    [Fact]
    public async Task Fix_DoesNotCreateHubFilesInAgentFolders()
    {
        await InitProjectAsync("none");

        // A modes folder with files but no _index.md (by design)
        var modesDir = Path.Combine(TestDir, "dydo/agents/sample/modes");
        Directory.CreateDirectory(modesDir);
        File.WriteAllText(Path.Combine(modesDir, "code-writer.md"), "# code-writer");

        AssertDirectoryExists("dydo/agents/sample/modes");
        Assert.False(File.Exists(Path.Combine(TestDir, "dydo/agents/sample/modes/_index.md")));

        var result = await FixAsync();

        result.AssertSuccess();
        Assert.False(File.Exists(Path.Combine(TestDir, "dydo/agents/sample/modes/_index.md")));
    }

    [Fact]
    public async Task Fix_DoesNotCreateHubFilesInSystemFolders()
    {
        await InitProjectAsync("none");

        // The _system folder holds files but no _index.md (by design)
        AssertDirectoryExists("dydo/_system");
        Assert.False(File.Exists(Path.Combine(TestDir, "dydo/_system/_index.md")));

        var result = await FixAsync();

        result.AssertSuccess();
        Assert.False(File.Exists(Path.Combine(TestDir, "dydo/_system/_index.md")));
    }

    [Fact]
    public async Task Fix_DoesNotReportManualFixesForAgentFiles()
    {
        await InitProjectAsync("none");

        var result = await FixAsync();

        result.AssertSuccess();
        Assert.DoesNotContain("agents/sample", result.Stdout);
    }

    #endregion

    #endregion

    #region Index

    [Fact]
    public async Task Index_RegeneratesIndex()
    {
        await InitProjectAsync("none");

        WriteFile("dydo/index.md", "# Old Index\n\nOld content.");

        var result = await IndexAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Generated");

        var content = ReadFile("dydo/index.md");
        Assert.Contains("understand", content);
        Assert.Contains("guides", content);
    }

    [Fact]
    public async Task Index_ListsHubFolders()
    {
        await InitProjectAsync("none");

        var result = await IndexAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Scanned documentation sections");
        result.AssertStdoutContains("understand");
        result.AssertStdoutContains("guides");
    }

    [Fact]
    public async Task Index_ReportsPopulatedSectionWithoutOptionalNavigationPage()
    {
        await InitProjectAsync("none");
        File.Delete(Path.Combine(TestDir, "dydo", "guides", "_guides.md"));

        var result = await IndexAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("guides (");
        Assert.DoesNotContain("guides/_guides.md", result.Stdout);
        var index = ReadFile("dydo/index.md");
        Assert.DoesNotContain("./guides/_guides.md", index);
        Assert.DoesNotContain("guides/ folder not found", index);
    }

    [Fact]
    public async Task Index_GeneratedRootPassesCheck()
    {
        await InitProjectAsync("none");

        (await IndexAsync()).AssertSuccess();

        var check = await CheckAsync();
        check.AssertSuccess();
        Assert.DoesNotContain("Broken link", check.Stdout);
    }

    [Fact]
    public async Task Index_NoDocsFolder_Fails()
    {
        var command = IndexCommand.Create();
        var result = await RunAsync(command, "nonexistent");

        result.AssertExitCode(2);
        result.AssertStderrContains("Could not find docs folder");
    }

    #endregion

    #region Graph

    [Fact]
    public async Task Graph_ShowsOutgoingLinks()
    {
        await InitProjectAsync("none");

        var result = await GraphAsync("dydo/index.md");

        result.AssertSuccess();
        result.AssertStdoutContains("index.md");
    }

    [Fact]
    public async Task Graph_ShowsIncomingLinks()
    {
        await InitProjectAsync("none");

        var result = await GraphAsync("dydo/welcome.md", incoming: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Incoming links");
    }

    [Fact]
    public async Task Graph_FileNotFound_Fails()
    {
        await InitProjectAsync("none");

        var result = await GraphAsync("nonexistent.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("not found");
    }

    [Fact]
    public async Task Graph_WithDegree_ShowsMultipleHops()
    {
        await InitProjectAsync("none");

        var result = await GraphAsync("dydo/index.md", degree: 2);

        result.AssertSuccess();
        result.AssertStdoutContains("hops");
    }

    [Fact]
    public async Task Graph_WithZeroDegree_ReturnsQuickly()
    {
        await InitProjectAsync("none");

        var result = await GraphAsync("dydo/index.md", degree: 0);

        // Degree 0 still works, but may show only help/usage hint
        result.AssertSuccess();
    }

    [Fact]
    public async Task Graph_WithNegativeDegree_TreatedAsZero()
    {
        await InitProjectAsync("none");

        var result = await GraphAsync("dydo/index.md", degree: -1);

        // Negative degree is not validated, treated as 0 or handled gracefully
        result.AssertSuccess();
    }

    [Fact]
    public async Task Graph_WithLargeDegree_HandlesGracefully()
    {
        await InitProjectAsync("none");

        var result = await GraphAsync("dydo/index.md", degree: 100);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Graph_Stats_ShowsTopDocsByIncomingLinks()
    {
        Directory.CreateDirectory(Path.Combine(TestDir, "dydo"));

        // Create minimal config for FindDocsFolder to work
        WriteFile("dydo.json", """
            {
              "version": 1,
              "docRoot": "dydo",
              "humans": { "test": { "agents": [] } }
            }
            """);

        WriteFile("dydo/index.md", """
            ---
            area: general
            type: hub
            ---

            # Index

            Documentation index.

            - [Guide A](./guide-a.md)
            - [Guide B](./guide-b.md)
            - [Glossary](./glossary.md)
            """);

        WriteFile("dydo/guide-a.md", """
            ---
            area: guides
            type: guide
            ---

            # Guide A

            See [Glossary](./glossary.md) for terms.
            """);

        WriteFile("dydo/guide-b.md", """
            ---
            area: guides
            type: guide
            ---

            # Guide B

            See [Glossary](./glossary.md) for terms.
            Also see [Guide A](./guide-a.md).
            """);

        WriteFile("dydo/glossary.md", """
            ---
            area: general
            type: reference
            ---

            # Glossary

            Terms defined here.
            """);

        var result = await GraphStatsAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Document Link Statistics");
        result.AssertStdoutContains("glossary.md"); // Most linked (3 incoming)
        result.AssertStdoutContains("Total:");
    }

    [Fact]
    public async Task Graph_Stats_RespectsTopOption()
    {
        await InitProjectAsync("none");

        var result = await GraphStatsAsync(top: 3);

        result.AssertSuccess();
        result.AssertStdoutContains("Top 3");
    }

    [Fact]
    public async Task Graph_Stats_NoDocsFolder_Fails()
    {
        var result = await GraphStatsAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("Could not find docs folder");
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> FixAsync(string? path = null)
    {
        var command = FixCommand.Create();
        var args = path != null ? new[] { path } : Array.Empty<string>();
        return await RunAsync(command, args);
    }

    private async Task<CommandResult> IndexAsync(string? path = null)
    {
        var command = IndexCommand.Create();
        var args = path != null ? new[] { path } : Array.Empty<string>();
        return await RunAsync(command, args);
    }

    private async Task<CommandResult> GraphAsync(string file, bool incoming = false, int degree = 1)
    {
        var command = GraphCommand.Create();
        var args = new List<string> { file };
        if (incoming) args.Add("--incoming");
        if (degree != 1) { args.Add("--degree"); args.Add(degree.ToString()); }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> GraphStatsAsync(int top = 100)
    {
        var command = GraphCommand.Create();
        var args = new List<string> { "stats" };
        if (top != 100) { args.Add("--top"); args.Add(top.ToString()); }
        return await RunAsync(command, args.ToArray());
    }

    #endregion
}
