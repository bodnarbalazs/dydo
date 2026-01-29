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

        var result = await TaskCreateAsync("my-feature");

        result.AssertSuccess();
        result.AssertStdoutContains("Created task");
        AssertFileExists("project/tasks/my-feature.md");
    }

    [Fact]
    public async Task Task_Create_WithDescription()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskCreateAsync("auth-fix", "Fix authentication bug in login");

        result.AssertSuccess();
        AssertFileContains("project/tasks/auth-fix.md", "Fix authentication bug");
    }

    [Fact]
    public async Task Task_Create_AssignsCurrentAgent()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await TaskCreateAsync("my-task");

        result.AssertSuccess();
        AssertFileContains("project/tasks/my-task.md", "assigned: Adele");
    }

    [Fact]
    public async Task Task_Create_DuplicateFails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("duplicate-task");

        var result = await TaskCreateAsync("duplicate-task");

        result.AssertExitCode(2);
        result.AssertStderrContains("already exists");
    }

    #endregion

    #region Task List

    [Fact]
    public async Task Task_List_ShowsTasks()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("task-one");
        await TaskCreateAsync("task-two");

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
        await TaskCreateAsync("pending-task");
        await TaskCreateAsync("review-task");
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
        await TaskCreateAsync("feature-x");

        var result = await TaskReadyForReviewAsync("feature-x", "Feature complete, tests pass");

        result.AssertSuccess();
        result.AssertStdoutContains("ready for review");
        AssertFileContains("project/tasks/feature-x.md", "status: review-pending");
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
    public async Task Task_Approve_ClosesTask()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("approved-task");
        await TaskReadyForReviewAsync("approved-task", "Done");

        var result = await TaskApproveAsync("approved-task");

        result.AssertSuccess();
        result.AssertStdoutContains("approved");
        AssertFileContains("project/tasks/approved-task.md", "status: closed");
    }

    [Fact]
    public async Task Task_Approve_WithNotes()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("good-task");
        await TaskReadyForReviewAsync("good-task", "Implemented feature");

        var result = await TaskApproveAsync("good-task", "Great work!");

        result.AssertSuccess();
        AssertFileContains("project/tasks/good-task.md", "Great work!");
    }

    [Fact]
    public async Task Task_Reject_MarksForRework()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("rejected-task");
        await TaskReadyForReviewAsync("rejected-task", "Needs review");

        var result = await TaskRejectAsync("rejected-task", "Missing error handling");

        result.AssertSuccess();
        result.AssertStdoutContains("rejected");
        AssertFileContains("project/tasks/rejected-task.md", "status: review-failed");
        AssertFileContains("project/tasks/rejected-task.md", "Missing error handling");
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> TaskCreateAsync(string name, string? description = null)
    {
        var command = TaskCommand.Create();
        var args = description != null
            ? new[] { "create", name, "--description", description }
            : new[] { "create", name };
        return await RunAsync(command, args);
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
