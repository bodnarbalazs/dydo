namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for documentation commands:
/// check, fix, index, graph.
/// </summary>
[Collection("Integration")]
public class DocumentationTests : IntegrationTestBase
{
    #region Check

    [Fact]
    public async Task Check_ValidDocs_Passes()
    {
        // Create a minimal valid doc structure (don't use init which creates docs with known issues)
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
        // Init creates docs with known validation issues (broken links, etc.)
        await InitProjectAsync("none", "balazs", 3);

        var result = await CheckAsync();

        // Should find the broken links in templates
        result.AssertExitCode(1);
        result.AssertStdoutContains("Found");
        result.AssertStdoutContains("errors");
    }

    [Fact]
    public async Task Check_InvalidFrontmatter_ReportsErrors()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create a doc without frontmatter
        WriteFile("dydo/guides/bad-doc.md", "# Bad Doc\n\nNo frontmatter here.");

        var result = await CheckAsync();

        result.AssertExitCode(1);
        result.AssertStdoutContains("bad-doc.md");
        result.AssertStdoutContains("frontmatter");
    }

    [Fact]
    public async Task Check_SpecificPath_ChecksOnlyThatPath()
    {
        // Create a minimal structure with valid understand folder and invalid guides folder
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

        // Invalid doc in guides
        WriteFile("dydo/guides/invalid.md", "# No frontmatter");

        // Check only understand folder (which is valid)
        var result = await CheckAsync("dydo/understand");

        // Should pass because we're only checking understand folder
        result.AssertSuccess();
    }

    [Fact]
    public async Task Check_BrokenLinks_ReportsErrors()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create a doc with a broken link
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
        // Don't initialize, try to check non-existent path
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
        await InitProjectAsync("none", "balazs", 3);

        // Create a doc with non-kebab-case name
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
    public async Task Fix_CreatesMissingHubFiles()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create a subfolder without _index.md
        WriteFile("dydo/guides/tutorials/first.md", """
            ---
            area: guides
            type: tutorial
            ---

            # First Tutorial

            Content here.
            """);

        var result = await FixAsync();

        result.AssertSuccess();
        AssertFileExists("dydo/guides/tutorials/_index.md");
    }

    [Fact]
    public async Task Fix_ReportsManualFixNeeded()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create a doc without frontmatter (can't be auto-fixed)
        WriteFile("dydo/guides/needs-manual.md", "# Needs Manual Fix\n\nNo frontmatter.");

        var result = await FixAsync();

        result.AssertSuccess(); // Fix command still succeeds
        result.AssertStdoutContains("NEEDS MANUAL FIX");
        result.AssertStdoutContains("needs-manual.md");
    }

    [Fact]
    public async Task Fix_HubFiles_ContainActualLinks()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create a subfolder with docs but no hub
        WriteFile("dydo/guides/tutorials/getting-started.md", """
            ---
            area: guides
            type: guide
            ---

            # Getting Started

            This guide helps you get started quickly.
            """);

        var result = await FixAsync();

        result.AssertSuccess();
        AssertFileExists("dydo/guides/tutorials/_index.md");

        var hubContent = ReadFile("dydo/guides/tutorials/_index.md");
        Assert.Contains("[Getting Started](./getting-started.md)", hubContent);
        Assert.Contains("This guide helps you get started quickly.", hubContent);
        Assert.DoesNotContain("TODO", hubContent);
    }

    [Fact]
    public async Task Fix_HubFiles_SortsLinksAlphabetically()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create multiple docs - they should be sorted alphabetically by title
        WriteFile("dydo/guides/tutorials/zebra.md", """
            ---
            area: guides
            type: guide
            ---

            # Zebra Guide

            About zebras.
            """);

        WriteFile("dydo/guides/tutorials/alpha.md", """
            ---
            area: guides
            type: guide
            ---

            # Alpha Guide

            About alphas.
            """);

        var result = await FixAsync();

        result.AssertSuccess();

        var hubContent = ReadFile("dydo/guides/tutorials/_index.md");
        var alphaIndex = hubContent.IndexOf("[Alpha Guide]");
        var zebraIndex = hubContent.IndexOf("[Zebra Guide]");

        Assert.True(alphaIndex < zebraIndex, "Alpha should come before Zebra (alphabetical order)");
    }

    [Fact]
    public async Task Fix_HubFiles_FallsBackToFilenameWhenNoTitle()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create a doc without a # title heading
        WriteFile("dydo/guides/tutorials/user-authentication.md", """
            ---
            area: guides
            type: guide
            ---

            Some content without a title heading.
            """);

        var result = await FixAsync();

        result.AssertSuccess();

        var hubContent = ReadFile("dydo/guides/tutorials/_index.md");
        // Should convert "user-authentication" to "User Authentication"
        Assert.Contains("[User Authentication](./user-authentication.md)", hubContent);
    }

    [Fact]
    public async Task Fix_HubFiles_ShowsLinkCountInOutput()
    {
        await InitProjectAsync("none", "balazs", 3);

        WriteFile("dydo/guides/tutorials/doc1.md", """
            ---
            area: guides
            type: guide
            ---

            # Doc One

            First doc.
            """);

        WriteFile("dydo/guides/tutorials/doc2.md", """
            ---
            area: guides
            type: guide
            ---

            # Doc Two

            Second doc.
            """);

        var result = await FixAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("(2 links)");
    }

    [Fact]
    public async Task Fix_HubFiles_ExcludesIndexFiles()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create a folder that already has index.md (not _index.md)
        WriteFile("dydo/guides/tutorials/index.md", """
            ---
            area: guides
            type: hub
            ---

            # Tutorials Index

            This is an index file.
            """);

        WriteFile("dydo/guides/tutorials/actual-guide.md", """
            ---
            area: guides
            type: guide
            ---

            # Actual Guide

            Real content.
            """);

        var result = await FixAsync();

        result.AssertSuccess();

        var hubContent = ReadFile("dydo/guides/tutorials/_index.md");
        // Should include actual-guide but not index.md
        Assert.Contains("[Actual Guide]", hubContent);
        Assert.DoesNotContain("[Tutorials Index]", hubContent);
    }

    #endregion

    #region Index

    [Fact]
    public async Task Index_RegeneratesIndex()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Modify index.md
        WriteFile("dydo/index.md", "# Old Index\n\nOld content.");

        var result = await IndexAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Generated");

        // Index should be regenerated with proper structure
        var content = ReadFile("dydo/index.md");
        Assert.Contains("understand", content);
        Assert.Contains("guides", content);
    }

    [Fact]
    public async Task Index_ListsHubFolders()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await IndexAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Scanned top-level hubs");
        result.AssertStdoutContains("understand");
        result.AssertStdoutContains("guides");
    }

    [Fact]
    public async Task Index_NoDocsFolder_Fails()
    {
        // Don't initialize
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
        await InitProjectAsync("none", "balazs", 3);

        var result = await GraphAsync("dydo/index.md");

        result.AssertSuccess();
        result.AssertStdoutContains("index.md");
    }

    [Fact]
    public async Task Graph_ShowsIncomingLinks()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Check incoming links to a file that is linked from index.md
        var result = await GraphAsync("dydo/welcome.md", incoming: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Incoming links");
    }

    [Fact]
    public async Task Graph_FileNotFound_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await GraphAsync("nonexistent.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("not found");
    }

    [Fact]
    public async Task Graph_WithDegree_ShowsMultipleHops()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await GraphAsync("dydo/index.md", degree: 2);

        result.AssertSuccess();
        result.AssertStdoutContains("hops");
    }

    [Fact]
    public async Task Graph_WithZeroDegree_ReturnsQuickly()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await GraphAsync("dydo/index.md", degree: 0);

        // Degree 0 still works, but may show only help/usage hint
        result.AssertSuccess();
    }

    [Fact]
    public async Task Graph_WithNegativeDegree_TreatedAsZero()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await GraphAsync("dydo/index.md", degree: -1);

        // Negative degree is not validated, treated as 0 or handled gracefully
        result.AssertSuccess();
    }

    [Fact]
    public async Task Graph_WithLargeDegree_HandlesGracefully()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Very large degree shouldn't cause infinite loops
        var result = await GraphAsync("dydo/index.md", degree: 100);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Graph_Stats_ShowsTopDocsByIncomingLinks()
    {
        // Create docs with known link structure
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
        await InitProjectAsync("none", "balazs", 3);

        var result = await GraphStatsAsync(top: 3);

        result.AssertSuccess();
        result.AssertStdoutContains("Top 3");
    }

    [Fact]
    public async Task Graph_Stats_NoDocsFolder_Fails()
    {
        // Don't create any docs folder

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
