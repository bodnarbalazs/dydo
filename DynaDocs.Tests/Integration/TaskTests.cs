namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for task management commands:
/// task create, list, ready-for-review, approve, reject.
/// </summary>
[Collection("Integration")]
public class TaskTests : IntegrationTestBase
{
    #region Task Create

    [Fact]
    public async Task Task_Create_CreatesFile()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskCreateAsync("my-feature", area: "backend");

        result.AssertSuccess();
        result.AssertStdoutContains("Created task");
        AssertFileExists("dydo/project/tasks/my-feature.md");
        AssertFileContains("dydo/project/tasks/my-feature.md", "area: backend");
    }

    [Fact]
    public async Task Task_Create_WithDescription()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskCreateAsync("auth-fix", "Fix authentication bug in login", area: "backend");

        result.AssertSuccess();
        AssertFileContains("dydo/project/tasks/auth-fix.md", "Fix authentication bug");
    }

    [Fact]
    public async Task Task_Create_AssignsCurrentAgent()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await TaskCreateAsync("my-task", area: "general");

        result.AssertSuccess();
        AssertFileContains("dydo/project/tasks/my-task.md", "assigned: Adele");
    }

    [Fact]
    public async Task Task_Create_DuplicateFails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("duplicate-task", area: "general");

        var result = await TaskCreateAsync("duplicate-task", area: "general");

        result.AssertExitCode(2);
        result.AssertStderrContains("already exists");
    }

    [Fact]
    public async Task Task_Create_WithoutArea_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        // System.CommandLine will reject the command since --area is required
        var command = TaskCommand.Create();
        var result = await RunAsync(command, "create", "no-area-task");

        // Required option missing produces a non-zero exit code
        Assert.True(result.HasError, "Expected error when --area is omitted");
    }

    [Fact]
    public async Task Task_Create_InvalidArea_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskCreateAsync("bad-area-task", area: "invalid-area");

        result.AssertExitCode(2);
        result.AssertStderrContains("Invalid area");
    }

    #endregion

    #region Task List

    [Fact]
    public async Task Task_List_ShowsTasks()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("task-one", area: "backend");
        await TaskCreateAsync("task-two", area: "frontend");

        var result = await TaskListAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("task-one");
        result.AssertStdoutContains("task-two");
    }

    [Fact]
    public async Task Task_List_Empty_ShowsMessage()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskListAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("No tasks found");
    }

    [Fact]
    public async Task Task_List_NeedsReview_FiltersCorrectly()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("pending-task", area: "general");
        await TaskCreateAsync("review-task", area: "general");
        await TaskReadyForReviewAsync("review-task", "Ready for review");

        var result = await TaskListAsync(needsReview: true);

        result.AssertSuccess();
        result.AssertStdoutContains("review-task");
        Assert.DoesNotContain("pending-task", result.Stdout);
    }

    #endregion

    #region Task Ready For Review

    [Fact]
    public async Task Task_ReadyForReview_UpdatesStatus()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("feature-x", area: "backend");

        var result = await TaskReadyForReviewAsync("feature-x", "Feature complete, tests pass");

        result.AssertSuccess();
        result.AssertStdoutContains("ready for review");
        AssertFileContains("dydo/project/tasks/feature-x.md", "status: review-pending");
    }

    [Fact]
    public async Task Task_ReadyForReview_NotFound_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskReadyForReviewAsync("nonexistent", "Summary");

        result.AssertExitCode(2);
        result.AssertStderrContains("not found");
    }

    #endregion

    #region Task Approve/Reject

    [Fact]
    public async Task Task_Approve_MovesToChangelog()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("approved-task", area: "backend");
        await TaskReadyForReviewAsync("approved-task", "Done");

        var result = await TaskApproveAsync("approved-task");

        result.AssertSuccess();
        result.AssertStdoutContains("approved");
        result.AssertStdoutContains("Changelog entry created");

        // Original task file should be gone
        AssertFileNotExists("dydo/project/tasks/approved-task.md");

        // Changelog entry should exist
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");
        AssertFileExists($"dydo/project/changelog/{year}/{today}/approved-task.md");
    }

    [Fact]
    public async Task Task_Approve_WithNotes()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("good-task", area: "frontend");
        await TaskReadyForReviewAsync("good-task", "Implemented feature");

        var result = await TaskApproveAsync("good-task", "Great work!");

        result.AssertSuccess();

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");
        AssertFileContains($"dydo/project/changelog/{year}/{today}/good-task.md", "Great work!");
    }

    [Fact]
    public async Task Task_Approve_GeneratesChangelogFrontmatter()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("fm-task", area: "backend");
        await TaskReadyForReviewAsync("fm-task", "Done");

        await TaskApproveAsync("fm-task");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");
        var changelogPath = $"dydo/project/changelog/{year}/{today}/fm-task.md";
        AssertFileContains(changelogPath, "type: changelog");
        AssertFileContains(changelogPath, $"date: {today}");
        AssertFileContains(changelogPath, "area: backend");

        // Task-specific fields should be removed
        var content = ReadFile(changelogPath);
        Assert.DoesNotContain("name: fm-task", content);
        Assert.DoesNotContain("status:", content);
        Assert.DoesNotContain("assigned:", content);
    }

    [Fact]
    public async Task Task_Approve_CreatesChangelogDirectoryStructure()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("dir-task", area: "general");
        await TaskReadyForReviewAsync("dir-task", "Done");

        await TaskApproveAsync("dir-task");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");

        // Hub files should be created
        AssertFileExists("dydo/project/changelog/_index.md");
        AssertFileExists($"dydo/project/changelog/{year}/_index.md");
        AssertFileExists($"dydo/project/changelog/{year}/{today}/_index.md");

        // Hub files should have proper frontmatter
        AssertFileContains("dydo/project/changelog/_index.md", "type: hub");
        AssertFileContains($"dydo/project/changelog/{year}/_index.md", "type: hub");

        // Date index should link to the task
        AssertFileContains($"dydo/project/changelog/{year}/{today}/_index.md", "dir-task.md");
    }

    [Fact]
    public async Task Task_Approve_IncludesAuditFileChanges()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "audit-task");
        await ReadMustReadsAsync();

        // Mark task ready so it can be approved
        await TaskReadyForReviewAsync("audit-task", "Done with file changes");

        // Simulate real file operations through the guard — this creates actual audit entries
        var writeResult = await GuardAsync("write", "src/Services/AuthService.cs");
        writeResult.AssertSuccess();
        var editResult = await GuardAsync("edit", "src/Models/User.cs");
        editResult.AssertSuccess();
        var deleteResult = await GuardAsync("delete", "src/Legacy/OldAuth.cs");
        deleteResult.AssertSuccess();

        var result = await TaskApproveAsync("audit-task");

        result.AssertSuccess();

        // Console output should show file change prefixes
        result.AssertStdoutContains("+ src/Services/AuthService.cs");
        result.AssertStdoutContains("~ src/Models/User.cs");
        result.AssertStdoutContains("- src/Legacy/OldAuth.cs");

        // Changelog entry should contain the Files Changed section
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var thisYear = DateTime.UtcNow.ToString("yyyy");
        var changelogPath = $"dydo/project/changelog/{thisYear}/{today}/audit-task.md";
        AssertFileContains(changelogPath, "src/Services/AuthService.cs — Created");
        AssertFileContains(changelogPath, "src/Models/User.cs — Modified");
        AssertFileContains(changelogPath, "src/Legacy/OldAuth.cs — Deleted");
    }

    [Fact]
    public async Task Task_Approve_MultipleOnSameDate_LinksCorrectly()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("first-task", area: "backend");
        await TaskCreateAsync("second-task", area: "frontend");
        await TaskReadyForReviewAsync("first-task", "First done");
        await TaskReadyForReviewAsync("second-task", "Second done");

        await TaskApproveAsync("first-task");
        await TaskApproveAsync("second-task");

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var year = DateTime.UtcNow.ToString("yyyy");

        // Both changelog entries should exist
        AssertFileExists($"dydo/project/changelog/{year}/{today}/first-task.md");
        AssertFileExists($"dydo/project/changelog/{year}/{today}/second-task.md");

        // Date _index.md should link to both tasks
        var dateIndex = ReadFile($"dydo/project/changelog/{year}/{today}/_index.md");
        Assert.Contains("first-task.md", dateIndex);
        Assert.Contains("second-task.md", dateIndex);

        // Year _index.md should have only one link to the date folder (not duplicated)
        var yearIndex = ReadFile($"dydo/project/changelog/{year}/_index.md");
        var dateLinks = yearIndex.Split('\n').Count(l => l.Contains($"{today}/"));
        Assert.Equal(1, dateLinks);
    }

    [Fact]
    public async Task Task_Reject_MarksForRework()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("rejected-task", area: "general");
        await TaskReadyForReviewAsync("rejected-task", "Needs review");

        var result = await TaskRejectAsync("rejected-task", "Missing error handling");

        result.AssertSuccess();
        result.AssertStdoutContains("rejected");
        AssertFileContains("dydo/project/tasks/rejected-task.md", "status: review-failed");
        AssertFileContains("dydo/project/tasks/rejected-task.md", "Missing error handling");
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> TaskCreateAsync(string name, string? description = null, string area = "general")
    {
        var command = TaskCommand.Create();
        var args = new List<string> { "create", name, "--area", area };
        if (description != null)
        {
            args.Add("--description");
            args.Add(description);
        }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> TaskListAsync(bool needsReview = false, bool all = false)
    {
        var command = TaskCommand.Create();
        var args = new List<string> { "list" };
        if (needsReview) args.Add("--needs-review");
        if (all) args.Add("--all");
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> TaskReadyForReviewAsync(string name, string summary)
    {
        var command = TaskCommand.Create();
        return await RunAsync(command, "ready-for-review", name, "--summary", summary);
    }

    private async Task<CommandResult> TaskApproveAsync(string name, string? notes = null)
    {
        var command = TaskCommand.Create();
        var args = notes != null
            ? new[] { "approve", name, "--notes", notes }
            : new[] { "approve", name };
        return await RunAsync(command, args);
    }

    private async Task<CommandResult> TaskRejectAsync(string name, string notes)
    {
        var command = TaskCommand.Create();
        return await RunAsync(command, "reject", name, "--notes", notes);
    }

    #endregion
}
