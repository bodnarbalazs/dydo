namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for inquisition commands.
/// </summary>
[Collection("Integration")]
public class InquisitionTests : IntegrationTestBase
{
    /// <summary>
    /// Initialize git in the test directory so HasChangesSince doesn't crash the test host.
    /// </summary>
    private void InitGitRepo()
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = "init",
            WorkingDirectory = TestDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = System.Diagnostics.Process.Start(psi);
        process?.WaitForExit(5000);

        // Create an initial commit so HEAD exists
        psi.Arguments = "commit --allow-empty -m \"init\"";
        using var commit = System.Diagnostics.Process.Start(psi);
        commit?.WaitForExit(5000);
    }

    #region Coverage Command — No Reports

    [Fact]
    public async Task Coverage_NoInquisitionsDir_CreatesItAndShowsMessage()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await RunCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("No inquisitions found");
        AssertDirectoryExists("dydo/project/inquisitions");
    }

    [Fact]
    public async Task Coverage_EmptyDir_ShowsNoInquisitions()
    {
        await InitProjectAsync("none", "balazs", 3);
        var inqPath = Path.Combine(TestDir, "dydo", "project", "inquisitions");
        Directory.CreateDirectory(inqPath);
        File.WriteAllText(Path.Combine(inqPath, "_template.md"), "template");

        var result = await RunCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("No inquisitions found");
    }

    #endregion

    #region Coverage Command — With Reports

    [Fact]
    public async Task Coverage_WithReports_ShowsTableHeader()
    {
        await InitProjectAsync("none", "balazs", 3);
        InitGitRepo();
        CreateInquisitionReport("backend", "## 2026-03-01 — Adele\n\nLooks good.");

        var result = await RunCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Inquisition Coverage:");
        result.AssertStdoutContains("Area");
        result.AssertStdoutContains("Last Inquisition");
        result.AssertStdoutContains("Status");
    }

    [Fact]
    public async Task Coverage_ReportWithDate_ShowsDateAndStatus()
    {
        await InitProjectAsync("none", "balazs", 3);
        InitGitRepo();
        CreateInquisitionReport("backend", "## 2026-03-01 — Adele\n\nReview.");

        var result = await RunCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("backend");
        result.AssertStdoutContains("2026-03-01");
    }

    [Fact]
    public async Task Coverage_ReportWithNoDateHeader_ShowsGap()
    {
        await InitProjectAsync("none", "balazs", 3);
        CreateInquisitionReport("frontend", "# Frontend\n\nNo dates here.");

        var result = await RunCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("frontend");
        result.AssertStdoutContains("none");
        result.AssertStdoutContains("gap");
    }

    [Fact]
    public async Task Coverage_ReportWithInvalidDate_ShowsGap()
    {
        await InitProjectAsync("none", "balazs", 3);
        CreateInquisitionReport("bad-dates", "## not-a-date — Agent\n\nBad.");

        var result = await RunCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("none");
        result.AssertStdoutContains("gap");
    }

    [Fact]
    public async Task Coverage_ReportWithMultipleDates_ShowsLatest()
    {
        await InitProjectAsync("none", "balazs", 3);
        InitGitRepo();
        CreateInquisitionReport("services", "## 2026-01-15 — Adele\n\nFirst.\n\n## 2026-03-10 — Brian\n\nLatest.\n\n## 2026-02-20 — Charlie\n\nMiddle.");

        var result = await RunCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("2026-03-10");
    }

    [Fact]
    public async Task Coverage_MultipleReports_SortedAlphabetically()
    {
        await InitProjectAsync("none", "balazs", 3);
        CreateInquisitionReport("zebra", "# Zebra\n\nNo dates.");
        CreateInquisitionReport("alpha", "# Alpha\n\nNo dates.");

        var result = await RunCoverageAsync();

        result.AssertSuccess();
        var alphaIdx = result.Stdout.IndexOf("alpha");
        var zebraIdx = result.Stdout.IndexOf("zebra");
        Assert.True(alphaIdx < zebraIdx, "Reports should be sorted alphabetically");
    }

    [Fact]
    public async Task Coverage_MixedReports_ShowsCorrectStatuses()
    {
        await InitProjectAsync("none", "balazs", 3);
        InitGitRepo();
        CreateInquisitionReport("area-gap", "# No dates here.");
        CreateInquisitionReport("area-dated", "## 2026-03-01 — Agent\n\nReview.");

        var result = await RunCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("gap");
        // Dated area gets either "stale", "covered", or "unknown" depending on git state
        var stdout = result.Stdout;
        Assert.True(
            stdout.Contains("stale") || stdout.Contains("covered") || stdout.Contains("unknown"),
            "Dated area should show stale, covered, or unknown");
    }

    #endregion

    #region Helpers

    private async Task<CommandResult> RunCoverageAsync()
    {
        var command = InquisitionCommand.Create();
        return await RunAsync(command, "coverage");
    }

    private void CreateInquisitionReport(string area, string content)
    {
        var inqPath = Path.Combine(TestDir, "dydo", "project", "inquisitions");
        Directory.CreateDirectory(inqPath);
        File.WriteAllText(Path.Combine(inqPath, $"{area}.md"), content);
    }

    #endregion
}
