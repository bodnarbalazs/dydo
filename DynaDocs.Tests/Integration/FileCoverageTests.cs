namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;

/// <summary>
/// Integration tests for the file coverage heatmap feature.
/// </summary>
[Collection("Integration")]
public class FileCoverageTests : IntegrationTestBase, IDisposable
{
    public FileCoverageTests()
    {
        // Override git operations — integration tests don't need real git for file coverage
        FileCoverageService.GitLsFilesOverride = _ => ["Commands/Foo.cs", "Services/Bar.cs", "Models/Baz.cs"];
        FileCoverageService.GetPercentChangeOverride = (_, _, _) => 0;
    }

    public new void Dispose()
    {
        FileCoverageService.GitLsFilesOverride = null;
        FileCoverageService.GetPercentChangeOverride = null;
        base.Dispose();
    }

    #region No Audit Data

    [Fact]
    public async Task FileCoverage_NoAuditData_AllFilesShowAsGap()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await RunFileCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Gaps: 3");
        result.AssertStdoutContains("Covered: 0");
    }

    #endregion

    #region No Inquisitor Sessions

    [Fact]
    public async Task FileCoverage_NoInquisitorSessions_AllFilesShowAsGap()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create audit session without inquisitor role
        CreateAuditSession("code-writer", "some-task", DateTime.UtcNow.AddDays(-5),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read));

        var result = await RunFileCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Gaps: 3");
    }

    #endregion

    #region Basic End-to-End

    [Fact]
    public async Task FileCoverage_WithInquisitorSession_ShowsScores()
    {
        await InitProjectAsync("none", "balazs", 3);

        CreateAuditSession("inquisitor", "test-inq", DateTime.UtcNow.AddDays(-5),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read),
            (Path.Combine(TestDir, "Services", "Bar.cs"), AuditEventType.Read));

        var result = await RunFileCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("File coverage report written to:");
        // Should have some covered/low files
        Assert.True(
            result.Stdout.Contains("Low: ") || result.Stdout.Contains("Covered: "),
            "Should show score information");
    }

    [Fact]
    public async Task FileCoverage_ProducesMarkdownFile()
    {
        await InitProjectAsync("none", "balazs", 3);

        CreateAuditSession("inquisitor", "test-inq", DateTime.UtcNow.AddDays(-5),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read));

        var result = await RunFileCoverageAsync();

        result.AssertSuccess();

        // Output should be in project inquisitions (no agent claimed)
        var expectedPath = Path.Combine(TestDir, "dydo", "project", "inquisitions", "_coverage.md");
        Assert.True(File.Exists(expectedPath), $"Coverage report should exist at {expectedPath}");

        var content = File.ReadAllText(expectedPath);
        Assert.Contains("# File Coverage Heatmap", content);
        Assert.Contains("## Summary", content);
    }

    #endregion

    #region Filters

    [Fact]
    public async Task FileCoverage_PathFilter_OnlyMatchingFiles()
    {
        await InitProjectAsync("none", "balazs", 3);

        CreateAuditSession("inquisitor", "test-inq", DateTime.UtcNow.AddDays(-5),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read),
            (Path.Combine(TestDir, "Services", "Bar.cs"), AuditEventType.Read));

        var result = await RunFileCoverageAsync("--path", "Services");

        result.AssertSuccess();
        result.AssertStdoutContains("Total: 1");
    }

    [Fact]
    public async Task FileCoverage_GapsOnly_ExcludesCovered()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Read Foo.cs enough times across groups to make it "covered" (7+)
        CreateAuditSession("inquisitor", "t1", DateTime.UtcNow.AddDays(-30),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read));
        CreateAuditSession("inquisitor", "t2", DateTime.UtcNow.AddDays(-20),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read));
        CreateAuditSession("inquisitor", "t3", DateTime.UtcNow.AddDays(-10),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read));

        var result = await RunFileCoverageAsync("--gaps-only");

        result.AssertSuccess();
        // Foo.cs has score 9 → covered → excluded. Remaining 2 files are gaps.
        result.AssertStdoutContains("Total: 2");
    }

    [Fact]
    public async Task FileCoverage_Summary_FolderAggregatesOnly()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await RunFileCoverageAsync("--summary");

        result.AssertSuccess();

        var expectedPath = Path.Combine(TestDir, "dydo", "project", "inquisitions", "_coverage.md");
        var content = File.ReadAllText(expectedPath);
        Assert.Contains("Folder Summary", content);
        Assert.DoesNotContain("[gap]", content);
    }

    [Fact]
    public async Task FileCoverage_Since_ExcludesOldSessions()
    {
        await InitProjectAsync("none", "balazs", 3);

        CreateAuditSession("inquisitor", "recent", DateTime.UtcNow.AddDays(-10),
            (Path.Combine(TestDir, "Commands", "Foo.cs"), AuditEventType.Read));
        CreateAuditSession("inquisitor", "old", DateTime.UtcNow.AddDays(-400),
            (Path.Combine(TestDir, "Services", "Bar.cs"), AuditEventType.Read));

        var result = await RunFileCoverageAsync("--since", "30");

        result.AssertSuccess();
        // Only recent session counted; old one outside 30-day window
        result.AssertStdoutContains("Gaps: 2"); // Bar.cs and Baz.cs are gaps
    }

    #endregion

    #region Output Placement

    [Fact]
    public async Task FileCoverage_WithAgent_WritesToAgentWorkspace()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("A");
        await SetRoleAsync("inquisitor", "test-task");

        var result = await RunFileCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("inquisition-coverage.md");
        // Should be in agent workspace
        AssertFileExists("dydo/agents/Adele/inquisition-coverage.md");
    }

    [Fact]
    public async Task FileCoverage_NoAgent_WritesToProjectPath()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await RunFileCoverageAsync();

        result.AssertSuccess();
        AssertFileExists("dydo/project/inquisitions/_coverage.md");
    }

    #endregion

    #region Backward Compatibility

    [Fact]
    public async Task Coverage_WithoutFiles_StillWorksAsBeforeWithNewOptions()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Running coverage without --files should use the original area-level command
        var result = await RunCoverageAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("No inquisitions found");
    }

    #endregion

    #region Helpers

    private async Task<CommandResult> RunFileCoverageAsync(params string[] extraArgs)
    {
        var command = InquisitionCommand.Create();
        var args = new List<string> { "coverage", "--files" };
        args.AddRange(extraArgs);
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> RunCoverageAsync()
    {
        var command = InquisitionCommand.Create();
        return await RunAsync(command, "coverage");
    }

    private void CreateAuditSession(string role, string task, DateTime started, params (string Path, AuditEventType Type)[] events)
    {
        var sessionId = Guid.NewGuid().ToString();
        var auditEvents = new List<AuditEvent>
        {
            new() { EventType = AuditEventType.Role, Role = role, Task = task, Timestamp = started }
        };

        foreach (var (path, type) in events)
        {
            auditEvents.Add(new AuditEvent
            {
                EventType = type,
                Path = path,
                Timestamp = started.AddMinutes(auditEvents.Count)
            });
        }

        var session = new AuditSession
        {
            SessionId = sessionId,
            Started = started,
            Events = auditEvents
        };

        var auditPath = Path.Combine(TestDir, "dydo", "_system", "audit", started.Year.ToString());
        Directory.CreateDirectory(auditPath);

        var filename = $"{started:yyyy-MM-dd}-{sessionId}.json";
        var json = JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AuditSession);
        File.WriteAllText(Path.Combine(auditPath, filename), json);
    }

    #endregion
}
