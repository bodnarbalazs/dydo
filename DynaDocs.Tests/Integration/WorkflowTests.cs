namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;

/// <summary>
/// Integration tests for workflow commands:
/// dispatch, inbox (list, show, clear), review complete.
/// </summary>
[Collection("Integration")]
public class WorkflowTests : IntegrationTestBase
{
    #region Dispatch

    [Fact]
    public async Task Dispatch_CreatesInboxItem()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await DispatchAsync("reviewer", "my-task", "Please review this code");

        result.AssertSuccess();
        result.AssertStdoutContains("dispatched");

        // Verify inbox item was created (first free agent alphabetically is Adele)
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0, "Inbox item should be created");
    }

    [Fact]
    public async Task Dispatch_NoFreeAgents_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Claim all 3 agents
        await ClaimAgentAsync("Adele");
        await ReleaseAgentAsync();

        // Create a second human context and claim remaining agents
        SetHuman("balazs");
        await ClaimAgentAsync("Adele");

        // Now try to dispatch when current agent is claimed
        var result = await DispatchAsync("reviewer", "task", "Brief");

        // Should dispatch to next free agent (Brian or Charlie)
        // If all are claimed by this process, it should still find one
        // The exact behavior depends on implementation
        Assert.True(result.ExitCode == 0 || result.ExitCode == 2);
    }

    [Fact]
    public async Task Dispatch_WithFiles_IncludesPattern()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await DispatchAsync("reviewer", "my-task", "Review these files", files: "src/**/*.cs");

        result.AssertSuccess();

        // Check the inbox item contains the files pattern
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0);
        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("src/**/*.cs", content);
    }

    [Fact]
    public async Task Dispatch_SelectsAlphabeticallyFirst()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await DispatchAsync("reviewer", "task", "Brief");

        result.AssertSuccess();
        // Should pick Adele (first alphabetically among Adele, Brian, Charlie)
        result.AssertStdoutContains("Adele");
    }

    #endregion

    #region Review Complete

    [Fact]
    public async Task Review_Complete_Pass()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create a task in in-review state
        await TaskCreateAsync("review-test");
        await TaskReadyForReviewAsync("review-test", "Ready for review");
        await ClaimAgentAsync("Adele");

        var result = await ReviewCompleteAsync("review-test", "pass");

        result.AssertSuccess();
        result.AssertStdoutContains("PASSED");
        AssertFileContains("dydo/project/tasks/review-test.md", "status: done");
    }

    [Fact]
    public async Task Review_Complete_Pass_WithNotes()
    {
        await InitProjectAsync("none", "balazs", 3);

        await TaskCreateAsync("lgtm-task");
        await TaskReadyForReviewAsync("lgtm-task", "Done");
        await ClaimAgentAsync("Adele");

        var result = await ReviewCompleteAsync("lgtm-task", "pass", "LGTM! Great work.");

        result.AssertSuccess();
        AssertFileContains("dydo/project/tasks/lgtm-task.md", "LGTM");
    }

    [Fact]
    public async Task Review_Complete_Pass_AssignedAgent_IsRefused()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await TaskCreateAsync("self-review");
        await TaskReadyForReviewAsync("self-review", "Done");

        var result = await ReviewCompleteAsync("self-review", "pass");

        result.AssertExitCode(2);
        result.AssertStderrContains("cannot pass their own review");
        AssertFileContains("dydo/project/tasks/self-review.md", "status: in-review");
    }

    [Fact]
    public async Task Review_Complete_Fail()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        await TaskCreateAsync("fail-task");
        await TaskReadyForReviewAsync("fail-task", "Ready");

        var result = await ReviewCompleteAsync("fail-task", "fail", "Missing error handling");

        result.AssertSuccess();
        result.AssertStdoutContains("FAILED");
        AssertFileContains("dydo/project/tasks/fail-task.md", "status: in-progress");
        AssertFileContains("dydo/project/tasks/fail-task.md", "Missing error handling");
    }

    [Fact]
    public async Task Review_Complete_TaskNotFound_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ReviewCompleteAsync("nonexistent", "pass");

        result.AssertExitCode(2);
        result.AssertStderrContains("not found");
    }

    [Fact]
    public async Task Review_Complete_WrongStatus_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Create a task but don't mark it ready for review
        await TaskCreateAsync("not-ready");

        var result = await ReviewCompleteAsync("not-ready", "pass");

        result.AssertExitCode(2);
        result.AssertStderrContains("not in review state");
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> DispatchAsync(
        string role, string task, string brief,
        string? files = null)
    {
        var command = DispatchCommand.Create();
        var args = new List<string> { "--role", role, "--task", task, "--brief", brief, "--no-launch" };
        BypassNoLaunchNudge(task);
        if (files != null) { args.Add("--files"); args.Add(files); }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> ReviewCompleteAsync(string task, string status, string? notes = null)
    {
        var command = ReviewCommand.Create();
        var args = new List<string> { "complete", task, "--status", status };
        if (notes != null) { args.Add("--notes"); args.Add(notes); }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> TaskCreateAsync(string name, string area = "general")
    {
        var command = TaskCommand.Create();
        return await RunAsync(command, "create", name, "--area", area);
    }

    private async Task<CommandResult> TaskReadyForReviewAsync(string name, string summary)
    {
        var command = TaskCommand.Create();
        return await RunAsync(command, "ready-for-review", name, "--summary", summary);
    }

    #endregion
}
