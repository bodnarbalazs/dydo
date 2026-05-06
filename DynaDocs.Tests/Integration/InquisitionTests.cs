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
        RunGit("init");
        // CI runners have no global user.email / user.name. Pass -c overrides on
        // this invocation rather than mutating global config or the helper's env.
        RunGit("-c user.email=test@example.com -c user.name=Test commit --allow-empty -m \"init\"");
    }

    private void RunGit(string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = TestDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("git failed to start");

        // Drain both streams concurrently. Reading after WaitForExit deadlocks
        // when git fills the OS pipe buffer (~64 KB on Windows).
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(5000))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new InvalidOperationException(
                $"git {args} timed out after 5s in {TestDir}");
        }

        if (process.ExitCode != 0)
        {
            var stderr = stderrTask.GetAwaiter().GetResult();
            throw new InvalidOperationException(
                $"git {args} failed in {TestDir} (exit {process.ExitCode}): {stderr}");
        }
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

    #region RunGit Helper Tests

    [Fact]
    public void InitGitRepo_CompletesAndProducesValidRepository()
    {
        // Pins the contract that the RunGit helper completes its drain without
        // throwing or hanging. Pre-fix, redirect-without-drain could deadlock
        // and trip the 5 s timeout. This is the lighter sibling of
        // SnapshotServiceTests.RunGit_NoisyOutput_DoesNotDeadlock — init and
        // commit --allow-empty produce little output, so the deadlock surface is
        // small, but the helper shape is identical and the contract is the same.
        InitGitRepo();

        Assert.True(Directory.Exists(Path.Combine(TestDir, ".git")));
        Assert.True(File.Exists(Path.Combine(TestDir, ".git", "HEAD")));
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
