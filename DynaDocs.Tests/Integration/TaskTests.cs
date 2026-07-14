namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Utils;

/// <summary>
/// Integration tests for task management commands:
/// task create, list, ready-for-review, done.
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
        AssertFileContains("dydo/project/tasks/my-feature.md", "status: backlog");
    }

    [Fact]
    public async Task Task_Create_EmitsPrettifiedTitle()
    {
        // Spine docs are born with a title: key (issue 0290) so the Notion board never shows "New page".
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskCreateAsync("my-feature", area: "backend");

        result.AssertSuccess();
        AssertFileContains("dydo/project/tasks/my-feature.md", "title: My Feature");
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
        AssertFileContains("dydo/project/tasks/my-task.md", "status: in-progress");
    }

    [Fact]
    public async Task Task_Create_AssignedCurrentAgent_WritesRuntimeProvenance()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentWithRuntimeAsync("Adele", "codex", "gpt-5");

        var result = await TaskCreateAsync("provenance-task", area: "general");

        result.AssertSuccess();
        var content = ReadFile("dydo/project/tasks/provenance-task.md");
        Assert.Contains("assigned: Adele", content);
        Assert.Contains("assigned-vendor: codex", content);
        Assert.Contains("assigned-model: gpt-5", content);
    }

    [Fact]
    public async Task Task_Create_WithSpecialChars_SanitizesFilename()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TaskCreateAsync("Review: Auth & Email", area: "general");

        result.AssertSuccess();
        result.AssertStdoutContains("sanitized");

        // File should use sanitized name
        AssertFileExists("dydo/project/tasks/Review- Auth & Email.md");

        // Original name preserved in content
        AssertFileContains("dydo/project/tasks/Review- Auth & Email.md", "Review: Auth & Email");
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
    public async Task Task_Create_FailsWhenChangelogExistsForToday()
    {
        await InitProjectAsync("none", "balazs", 3);

        var changelogPath = Path.Combine(TestDir, "dydo", "project", "changelog",
            DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(changelogPath);
        File.WriteAllText(Path.Combine(changelogPath, "collision-task.md"), "existing");

        // Creating a new task with the same name should fail
        var result = await TaskCreateAsync("collision-task", area: "backend");

        result.AssertExitCode(2);
        result.AssertStderrContains("changelog entry named 'collision-task' already exists for today");
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
        AssertFileContains("dydo/project/tasks/feature-x.md", "status: in-review");
        AssertFileContains("dydo/project/tasks/feature-x.md", "Feature complete, tests pass");
    }

    [Fact]
    public async Task Task_ReadyForReview_WithoutSummary_GivesHelpfulError()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("no-summary-task", area: "backend");

        // Call ready-for-review without --summary
        var command = TaskCommand.Create();
        var result = await RunAsync(command, "ready-for-review", "no-summary-task");

        result.AssertExitCode(2);
        result.AssertStderrContains("--summary is required");
        result.AssertStderrContains("Brief description of completed work");
    }

    [Fact]
    public async Task Task_ReadyForReview_WithEmptySummary_GivesHelpfulError()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("empty-summary-task", area: "backend");

        // Call ready-for-review with --summary "" (empty string)
        var command = TaskCommand.Create();
        var result = await RunAsync(command, "ready-for-review", "empty-summary-task", "--summary", "");

        result.AssertExitCode(2);
        result.AssertStderrContains("--summary is required");
    }

    [Fact]
    public async Task Task_ReadyForReview_WithWhitespaceSummary_GivesHelpfulError()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("ws-summary-task", area: "backend");

        // Call ready-for-review with --summary "   " (whitespace only)
        var command = TaskCommand.Create();
        var result = await RunAsync(command, "ready-for-review", "ws-summary-task", "--summary", "   ");

        result.AssertExitCode(2);
        result.AssertStderrContains("--summary is required");
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

    #region Task Done

    [Fact]
    public async Task Task_Done_AssignedAgent_IsRefused()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await TaskCreateAsync("self-done", area: "backend");

        var result = await TaskDoneAsync("self-done");

        result.AssertExitCode(2);
        result.AssertStderrContains("cannot mark their own task done");
        AssertFileContains("dydo/project/tasks/self-done.md", "status: in-progress");
    }

    [Fact]
    public async Task Task_Done_DifferentAgent_MarksDoneAndKeepsTaskFile()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await TaskCreateAsync("peer-done", area: "backend");
        SetTaskAssigned("peer-done", "Brian");

        var result = await TaskDoneAsync("peer-done");

        result.AssertSuccess();
        AssertFileContains("dydo/project/tasks/peer-done.md", "status: done");
        AssertFileExists("dydo/project/tasks/peer-done.md");
    }

    [Fact]
    public async Task Task_Done_HumanTerminal_MarksDone()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("human-done", area: "backend");
        SetTaskStatus("human-done", "in-progress");

        var result = await TaskDoneAsync("human-done");

        result.AssertSuccess();
        AssertFileContains("dydo/project/tasks/human-done.md", "status: done");
    }

    [Fact]
    public async Task Task_Done_BacklogTask_IsRefused()
    {
        await InitProjectAsync("none", "balazs", 3);
        await TaskCreateAsync("backlog-task", area: "backend");

        var result = await TaskDoneAsync("backlog-task");

        result.AssertExitCode(2);
        result.AssertStderrContains("must be in-progress or in-review");
        AssertFileContains("dydo/project/tasks/backlog-task.md", "status: backlog");
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Task_ApproveAndReject_AreNotCommands(string commandName)
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await RunAsync(TaskCommand.Create(), commandName);

        Assert.True(result.HasError);
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

    private void SetTaskAssigned(string name, string agent) => WriteFile($"dydo/project/tasks/{name}.md", FrontmatterParser.UpsertField(ReadFile($"dydo/project/tasks/{name}.md"), "assigned", agent));

    private void SetTaskStatus(string name, string status)
    {
        var path = $"dydo/project/tasks/{name}.md";
        WriteFile(path, FrontmatterParser.UpsertField(ReadFile(path), "status", status));
    }

    private async Task<CommandResult> TaskDoneAsync(string name)
    {
        return await RunAsync(TaskCommand.Create(), "done", name);
    }

    #endregion
}
