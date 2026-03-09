namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

[Collection("Integration")]
public class IssueTests : IntegrationTestBase
{
    #region Issue Create

    [Fact]
    public async Task Issue_Create_CreatesFile()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await IssueCreateAsync("Hook bypass via Agent tool", area: "general", severity: "high");

        result.AssertSuccess();
        result.AssertStdoutContains("Created issue #1");
        AssertFileExists("dydo/project/issues/0001-hook-bypass-via-agent-tool.md");
        AssertFileContains("dydo/project/issues/0001-hook-bypass-via-agent-tool.md", "area: general");
        AssertFileContains("dydo/project/issues/0001-hook-bypass-via-agent-tool.md", "severity: high");
        AssertFileContains("dydo/project/issues/0001-hook-bypass-via-agent-tool.md", "status: open");
        AssertFileContains("dydo/project/issues/0001-hook-bypass-via-agent-tool.md", "type: issue");
    }

    [Fact]
    public async Task Issue_Create_AutoIncrementsId()
    {
        await InitProjectAsync("none", "balazs", 3);

        await IssueCreateAsync("First issue", area: "general", severity: "low");
        var result = await IssueCreateAsync("Second issue", area: "general", severity: "medium");

        result.AssertSuccess();
        result.AssertStdoutContains("Created issue #2");
        AssertFileExists("dydo/project/issues/0002-second-issue.md");
    }

    [Fact]
    public async Task Issue_Create_InvalidArea_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await IssueCreateAsync("Bad area issue", area: "nonexistent", severity: "low");

        result.AssertExitCode(2);
        result.AssertStderrContains("Invalid area");
    }

    [Fact]
    public async Task Issue_Create_InvalidSeverity_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await IssueCreateAsync("Bad sev issue", area: "general", severity: "extreme");

        result.AssertExitCode(2);
        result.AssertStderrContains("Invalid severity");
    }

    [Fact]
    public async Task Issue_Create_DefaultFoundBy_IsManual()
    {
        await InitProjectAsync("none", "balazs", 3);

        await IssueCreateAsync("Manual issue", area: "general", severity: "low");

        AssertFileContains("dydo/project/issues/0001-manual-issue.md", "found-by: manual");
    }

    [Fact]
    public async Task Issue_Create_WithFoundBy_SetsCorrectly()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await IssueCreateAsync("Inquisition issue", area: "general", severity: "high", foundBy: "inquisition");

        result.AssertSuccess();
        AssertFileContains("dydo/project/issues/0001-inquisition-issue.md", "found-by: inquisition");
    }

    #endregion

    #region Issue List

    [Fact]
    public async Task Issue_List_ShowsOpenIssues()
    {
        await InitProjectAsync("none", "balazs", 3);
        await IssueCreateAsync("Open bug", area: "general", severity: "high");

        var result = await IssueListAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Open bug");
    }

    [Fact]
    public async Task Issue_List_Empty_ShowsMessage()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await IssueListAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("No issues found");
    }

    [Fact]
    public async Task Issue_List_FilterByArea()
    {
        await InitProjectAsync("none", "balazs", 3);
        await IssueCreateAsync("Backend issue", area: "backend", severity: "low");
        await IssueCreateAsync("Frontend issue", area: "frontend", severity: "low");

        var result = await IssueListAsync(area: "backend");

        result.AssertSuccess();
        result.AssertStdoutContains("Backend issue");
        Assert.DoesNotContain("Frontend issue", result.Stdout);
    }

    [Fact]
    public async Task Issue_List_FilterByStatus()
    {
        await InitProjectAsync("none", "balazs", 3);
        await IssueCreateAsync("Open issue", area: "general", severity: "low");
        await IssueCreateAsync("Resolved issue", area: "general", severity: "low");
        await IssueResolveAsync(2, "Fixed it");

        var result = await IssueListAsync(status: "open");

        result.AssertSuccess();
        result.AssertStdoutContains("Open issue");
        Assert.DoesNotContain("Resolved issue", result.Stdout);
    }

    [Fact]
    public async Task Issue_List_All_IncludesResolved()
    {
        await InitProjectAsync("none", "balazs", 3);
        await IssueCreateAsync("Open issue", area: "general", severity: "low");
        await IssueCreateAsync("Will resolve", area: "general", severity: "low");
        await IssueResolveAsync(2, "Done");

        var result = await IssueListAsync(all: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Open issue");
        result.AssertStdoutContains("Will resolve");
    }

    #endregion

    #region Issue Resolve

    [Fact]
    public async Task Issue_Resolve_MovesToResolved()
    {
        await InitProjectAsync("none", "balazs", 3);
        await IssueCreateAsync("To resolve", area: "general", severity: "medium");

        var result = await IssueResolveAsync(1, "Fixed the problem");

        result.AssertSuccess();
        result.AssertStdoutContains("Resolved issue #1");
        AssertFileNotExists("dydo/project/issues/0001-to-resolve.md");
        AssertFileExists("dydo/project/issues/resolved/0001-to-resolve.md");
    }

    [Fact]
    public async Task Issue_Resolve_RequiresSummary()
    {
        await InitProjectAsync("none", "balazs", 3);
        await IssueCreateAsync("No summary issue", area: "general", severity: "low");

        // --summary is required by System.CommandLine, so omitting it should fail
        var command = IssueCommand.Create();
        var result = await RunAsync(command, "resolve", "1");

        result.AssertExitCode(1); // System.CommandLine validation error
    }

    [Fact]
    public async Task Issue_Resolve_NotFound_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await IssueResolveAsync(999, "Doesn't exist");

        result.AssertExitCode(2);
        result.AssertStderrContains("not found");
    }

    [Fact]
    public async Task Issue_Resolve_AlreadyResolved_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await IssueCreateAsync("Already done", area: "general", severity: "low");
        await IssueResolveAsync(1, "First resolution");

        var result = await IssueResolveAsync(1, "Second resolution");

        result.AssertExitCode(2);
        result.AssertStderrContains("already resolved");
    }

    [Fact]
    public async Task Issue_Resolve_UpdatesFrontmatter()
    {
        await InitProjectAsync("none", "balazs", 3);
        await IssueCreateAsync("Frontmatter check", area: "general", severity: "high");

        await IssueResolveAsync(1, "Updated correctly");

        var content = ReadFile("dydo/project/issues/resolved/0001-frontmatter-check.md");
        Assert.Contains("status: resolved", content);
        Assert.Contains("resolved-date:", content);
    }

    [Fact]
    public async Task Issue_Resolve_WritesSummary()
    {
        await InitProjectAsync("none", "balazs", 3);
        await IssueCreateAsync("Summary check", area: "general", severity: "low");

        await IssueResolveAsync(1, "The root cause was X, fixed by Y");

        var content = ReadFile("dydo/project/issues/resolved/0001-summary-check.md");
        Assert.Contains("The root cause was X, fixed by Y", content);
        Assert.DoesNotContain("(Filled when resolved)", content);
    }

    [Fact]
    public async Task Issue_Create_AcrossResolvedGap_IncrementsCorrectly()
    {
        await InitProjectAsync("none", "balazs", 3);
        await IssueCreateAsync("First issue", area: "general", severity: "low");
        await IssueResolveAsync(1, "Done");

        var result = await IssueCreateAsync("Second issue", area: "general", severity: "low");

        result.AssertSuccess();
        result.AssertStdoutContains("Created issue #2");
        AssertFileExists("dydo/project/issues/0002-second-issue.md");
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> IssueCreateAsync(string title, string area = "general", string severity = "low", string? foundBy = null)
    {
        var command = IssueCommand.Create();
        var args = new List<string> { "create", "--title", title, "--area", area, "--severity", severity };
        if (foundBy != null)
        {
            args.Add("--found-by");
            args.Add(foundBy);
        }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> IssueListAsync(string? area = null, string? status = null, bool all = false)
    {
        var command = IssueCommand.Create();
        var args = new List<string> { "list" };
        if (area != null) { args.Add("--area"); args.Add(area); }
        if (status != null) { args.Add("--status"); args.Add(status); }
        if (all) args.Add("--all");
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> IssueResolveAsync(int id, string summary)
    {
        var command = IssueCommand.Create();
        return await RunAsync(command, "resolve", id.ToString(), "--summary", summary);
    }

    #endregion
}
