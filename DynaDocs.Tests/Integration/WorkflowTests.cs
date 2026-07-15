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
    #region Review Complete

    [Fact]
    public async Task Review_Complete_Pass()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create a task in in-review state
        await TaskCreateAsync("review-test");
        await TaskReadyForReviewAsync("review-test", "Ready for review");

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

        var result = await ReviewCompleteAsync("lgtm-task", "pass", "LGTM! Great work.");

        result.AssertSuccess();
        AssertFileContains("dydo/project/tasks/lgtm-task.md", "LGTM");
    }

    [Fact]
    public async Task Review_Complete_Fail()
    {
        await InitProjectAsync("none", "balazs", 3);

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

        // Create a task but don't mark it ready for review
        await TaskCreateAsync("not-ready");

        var result = await ReviewCompleteAsync("not-ready", "pass");

        result.AssertExitCode(2);
        result.AssertStderrContains("not in review state");
    }

    #endregion

    #region Helper Methods

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
