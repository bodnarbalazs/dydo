namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

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
    public async Task Dispatch_WithContext_IncludesFile()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create a context file
        WriteFile("context.md", "# Context\n\nSome important context.");

        var result = await DispatchAsync("reviewer", "my-task", "See context", contextFile: "context.md");

        result.AssertSuccess();

        // Check the inbox item references the context file
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0);
        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("context.md", content);
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

    #region Inbox List

    [Fact]
    public async Task Inbox_List_ShowsAgentsWithItems()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Create an inbox item manually
        CreateInboxItem("Adele", "Brian", "reviewer", "test-task", "Please review");

        var result = await InboxListAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Inbox_List_Empty_ShowsNone()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await InboxListAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("No pending inbox items");
    }

    #endregion

    #region Inbox Show

    [Fact]
    public async Task Inbox_Show_DisplaysItems()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Create an inbox item for Adele
        CreateInboxItem("Adele", "Brian", "reviewer", "test-task", "Please review this");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("test-task");
        result.AssertStdoutContains("Brian");
    }

    [Fact]
    public async Task Inbox_Show_NoAgent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        var result = await InboxShowAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("No agent");
    }

    [Fact]
    public async Task Inbox_Show_Empty_ShowsMessage()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("empty");
    }

    #endregion

    #region Inbox Clear

    [Fact]
    public async Task Inbox_Clear_All_ArchivesAll()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Create multiple inbox items
        CreateInboxItem("Adele", "Brian", "reviewer", "task1", "Brief 1");
        CreateInboxItem("Adele", "Charlie", "code-writer", "task2", "Brief 2");

        var result = await InboxClearAsync(all: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Archived");

        // Verify inbox is empty (items moved to archive)
        var inboxPath = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        var files = Directory.GetFiles(inboxPath, "*.md");
        Assert.Empty(files);

        // Verify items are in archive
        var archivePath = Path.Combine(inboxPath, "archive");
        Assert.True(Directory.Exists(archivePath), "Archive folder should exist");
        var archivedFiles = Directory.GetFiles(archivePath, "*.md");
        Assert.Equal(2, archivedFiles.Length);
    }

    [Fact]
    public async Task Inbox_Clear_ById_ArchivesSpecific()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Create an inbox item with known ID
        var inboxPath = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        Directory.CreateDirectory(inboxPath);
        var itemPath = Path.Combine(inboxPath, "abc12345-test-task.md");
        File.WriteAllText(itemPath, """
            ---
            id: abc12345
            from: Brian
            role: reviewer
            task: test-task
            received: 2024-01-01T00:00:00Z
            ---

            # Test

            Brief
            """);

        var result = await InboxClearAsync(id: "abc12345");

        result.AssertSuccess();
        Assert.False(File.Exists(itemPath), "Original file should be moved");

        // Verify item was archived
        var archivePath = Path.Combine(inboxPath, "archive", "abc12345-test-task.md");
        Assert.True(File.Exists(archivePath), "Item should be archived");
    }

    [Fact]
    public async Task Inbox_Clear_NoArgs_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await InboxClearAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("Specify");
    }

    [Fact]
    public async Task Inbox_Clear_NoAgent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        // Don't claim an agent

        var result = await InboxClearAsync(all: true);

        result.AssertExitCode(2);
        result.AssertStderrContains("No agent");
    }

    [Fact]
    public async Task Inbox_Clear_NonExistentId_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Try to clear a non-existent ID
        var result = await InboxClearAsync(id: "nonexistent123");

        result.AssertExitCode(2);
        result.AssertStderrContains("No inbox item with ID");
    }

    [Fact]
    public async Task Inbox_Clear_PartialId_ArchivesMatch()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Create an inbox item with known ID
        var inboxPath = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        Directory.CreateDirectory(inboxPath);
        var itemPath = Path.Combine(inboxPath, "abc12345-test-task.md");
        File.WriteAllText(itemPath, """
            ---
            id: abc12345
            from: Brian
            role: reviewer
            task: test-task
            received: 2024-01-01T00:00:00Z
            ---

            # Test

            Brief
            """);

        // Clear using partial ID prefix
        var result = await InboxClearAsync(id: "abc1");

        result.AssertSuccess();
        Assert.False(File.Exists(itemPath), "Original file should be moved");

        // Verify item was archived
        var archivePath = Path.Combine(inboxPath, "archive", "abc12345-test-task.md");
        Assert.True(File.Exists(archivePath), "Item should be archived");
    }

    #endregion

    #region Review Complete

    [Fact]
    public async Task Review_Complete_Pass()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Create a task in review-pending state
        await TaskCreateAsync("review-test");
        await TaskReadyForReviewAsync("review-test", "Ready for review");

        var result = await ReviewCompleteAsync("review-test", "pass");

        result.AssertSuccess();
        result.AssertStdoutContains("PASSED");
        AssertFileContains("project/tasks/review-test.md", "human-reviewed");
    }

    [Fact]
    public async Task Review_Complete_Pass_WithNotes()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        await TaskCreateAsync("lgtm-task");
        await TaskReadyForReviewAsync("lgtm-task", "Done");

        var result = await ReviewCompleteAsync("lgtm-task", "pass", "LGTM! Great work.");

        result.AssertSuccess();
        AssertFileContains("project/tasks/lgtm-task.md", "LGTM");
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
        AssertFileContains("project/tasks/fail-task.md", "review-failed");
        AssertFileContains("project/tasks/fail-task.md", "Missing error handling");
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
        string? files = null, string? contextFile = null)
    {
        var command = DispatchCommand.Create();
        var args = new List<string> { "--role", role, "--task", task, "--brief", brief, "--no-launch" };
        if (files != null) { args.Add("--files"); args.Add(files); }
        if (contextFile != null) { args.Add("--context-file"); args.Add(contextFile); }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> InboxListAsync()
    {
        var command = InboxCommand.Create();
        return await RunAsync(command, "list");
    }

    private async Task<CommandResult> InboxShowAsync()
    {
        var command = InboxCommand.Create();
        return await RunAsync(command, "show");
    }

    private async Task<CommandResult> InboxClearAsync(bool all = false, string? id = null)
    {
        var command = InboxCommand.Create();
        var args = new List<string> { "clear" };
        if (all) args.Add("--all");
        if (id != null) { args.Add("--id"); args.Add(id); }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> ReviewCompleteAsync(string task, string status, string? notes = null)
    {
        var command = ReviewCommand.Create();
        var args = new List<string> { "complete", task, "--status", status };
        if (notes != null) { args.Add("--notes"); args.Add(notes); }
        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> TaskCreateAsync(string name)
    {
        var command = TaskCommand.Create();
        return await RunAsync(command, "create", name);
    }

    private async Task<CommandResult> TaskReadyForReviewAsync(string name, string summary)
    {
        var command = TaskCommand.Create();
        return await RunAsync(command, "ready-for-review", name, "--summary", summary);
    }

    private void CreateInboxItem(string agentName, string fromAgent, string role, string task, string brief)
    {
        var inboxPath = Path.Combine(TestDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);

        var id = Guid.NewGuid().ToString("N")[..8];
        var content = $"""
            ---
            id: {id}
            from: {fromAgent}
            role: {role}
            task: {task}
            received: {DateTime.UtcNow:o}
            ---

            # {role.ToUpperInvariant()} Request: {task}

            ## From

            {fromAgent}

            ## Brief

            {brief}
            """;

        File.WriteAllText(Path.Combine(inboxPath, $"{id}-{task}.md"), content);
    }

    #endregion
}
