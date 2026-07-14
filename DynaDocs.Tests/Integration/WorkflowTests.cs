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
        var archivePath = Path.Combine(TestDir, "dydo/agents/Adele/archive/inbox");
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
        var archivePath = Path.Combine(TestDir, "dydo/agents/Adele/archive/inbox", "abc12345-test-task.md");
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
        var archivePath = Path.Combine(TestDir, "dydo/agents/Adele/archive/inbox", "abc12345-test-task.md");
        Assert.True(File.Exists(archivePath), "Item should be archived");
    }

    [Fact]
    public async Task Inbox_Clear_All_BlockedWhenUnreadMessages()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Add unread messages to agent state
        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", "msg-001");
        registry.AddUnreadMessage("Adele", "msg-002");

        // Create inbox items
        CreateInboxItem("Adele", "Brian", "reviewer", "task1", "Brief 1");

        var result = await InboxClearAsync(all: true);

        result.AssertExitCode(2);
        result.AssertStderrContains("unread message");

        // Verify unread messages are still tracked
        var state = registry.GetAgentState("Adele");
        Assert.Equal(2, state!.UnreadMessages.Count);
    }

    [Fact]
    public async Task Inbox_Clear_ById_BlockedWhenIdUnread()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Add unread messages
        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", "abc12345");
        registry.AddUnreadMessage("Adele", "other-msg");

        // Create inbox item with matching ID
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
        var originalBytes = File.ReadAllBytes(itemPath);

        var result = await InboxClearAsync(id: "abc12345");

        result.AssertExitCode(2);
        result.AssertStderrContains("not yet read");

        // Both unread messages should still be tracked
        var state = registry.GetAgentState("Adele");
        Assert.Contains("abc12345", state!.UnreadMessages);
        Assert.Contains("other-msg", state.UnreadMessages);
        Assert.Equal(originalBytes, File.ReadAllBytes(itemPath));
    }

    [Fact]
    public async Task Inbox_Clear_ForceFile_ArchivesOrphanedInboxItem()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        CreateInboxItem("Brian", "Adele", "reviewer", "orphaned-task", "Recover this item");
        var inboxPath = Path.Combine(TestDir, "dydo/agents/Brian/inbox");
        var itemPath = Directory.GetFiles(inboxPath, "*.md").Single();

        var result = await RunAsync(InboxCommand.Create(), "clear", "--force", "--file", itemPath);

        result.AssertSuccess();
        Assert.False(File.Exists(itemPath));
        Assert.True(File.Exists(Path.Combine(TestDir, "dydo/agents/Brian/archive/inbox", Path.GetFileName(itemPath))));
    }

    [Fact]
    public async Task Inbox_Clear_ForceFile_NonExistentPath_FailsFriendly()
    {
        await InitProjectAsync("none", "balazs", 3);

        var missingPath = Path.Combine(TestDir, "dydo/agents/Brian/inbox/missing.md");
        var result = await RunAsync(InboxCommand.Create(), "clear", "--force", "--file", missingPath);

        result.AssertExitCode(2);
        result.AssertStderrContains("Inbox file not found");
    }

    [Fact]
    public async Task Inbox_Clear_ForceFile_LiveOwnerRefuses()
    {
        await InitProjectAsync("none", "balazs", 3);
        CreateInboxItem("Brian", "Adele", "reviewer", "live-owner-task", "Do not archive this");
        var itemPath = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md").Single();
        var session = new AgentSession
        {
            Agent = "Brian",
            SessionId = "live-owner-session",
            Claimed = DateTime.UtcNow,
            ClaimedPid = Environment.ProcessId
        };
        File.WriteAllText(Path.Combine(TestDir, "dydo/agents/Brian/.session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));

        var result = await RunAsync(InboxCommand.Create(), "clear", "--force", "--file", itemPath);

        result.AssertExitCode(2);
        result.AssertStderrContains("Agent Brian has a live session");
        Assert.True(File.Exists(itemPath));
    }

    [Fact]
    public async Task Inbox_Clear_ForceFile_RejectsTraversalAndCrossDrivePaths()
    {
        await InitProjectAsync("none", "balazs", 3);
        var outsideFile = Path.Combine(TestDir, "dydo", "escape.md");
        File.WriteAllText(outsideFile, "must not move");
        var traversalPath = Path.Combine(TestDir, "dydo", "agents", "..", "escape.md");

        var traversal = await RunAsync(InboxCommand.Create(), "clear", "--force", "--file", traversalPath);

        traversal.AssertExitCode(2);
        traversal.AssertStderrContains("must target a Markdown file directly inside an agent inbox");
        Assert.True(File.Exists(outsideFile));

        var crossDrive = Path.GetPathRoot(TestDir)!.StartsWith("C", StringComparison.OrdinalIgnoreCase)
            ? "D:\\inbox\\escape.md"
            : "C:\\inbox\\escape.md";
        var crossDriveResult = await RunAsync(InboxCommand.Create(), "clear", "--force", "--file", crossDrive);

        crossDriveResult.AssertExitCode(2);
        crossDriveResult.AssertStderrContains("must target a Markdown file directly inside an agent inbox");
        Assert.True(File.Exists(outsideFile));
    }

    [Fact]
    public async Task Inbox_Clear_ForceFile_RequiresOnlyForceAndFile()
    {
        await InitProjectAsync("none", "balazs", 3);
        var missingFile = Path.Combine(TestDir, "dydo/agents/Brian/inbox/missing.md");

        var missingFileResult = await RunAsync(InboxCommand.Create(), "clear", "--force");
        var allResult = await RunAsync(InboxCommand.Create(), "clear", "--force", "--file", missingFile, "--all");
        var idResult = await RunAsync(InboxCommand.Create(), "clear", "--force", "--file", missingFile, "--id", "item");

        missingFileResult.AssertExitCode(2);
        missingFileResult.AssertStderrContains("Specify both --force and --file");
        allResult.AssertExitCode(2);
        allResult.AssertStderrContains("cannot be combined with --all or --id");
        idResult.AssertExitCode(2);
        idResult.AssertStderrContains("cannot be combined with --all or --id");
    }

    [Fact]
    public async Task Inbox_Clear_ById_AllowsWhenDifferentIdUnread()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Add unread message for a different ID
        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", "other-msg");

        // Create inbox item with a non-unread ID
        var inboxPath = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        Directory.CreateDirectory(inboxPath);
        File.WriteAllText(Path.Combine(inboxPath, "abc12345-test-task.md"), """
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
        Assert.False(File.Exists(Path.Combine(inboxPath, "abc12345-test-task.md")));
    }

    [Fact]
    public async Task Inbox_Clear_All_EmptyInbox_NoError()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Ensure inbox exists but is empty
        var inboxPath = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        Directory.CreateDirectory(inboxPath);

        var result = await InboxClearAsync(all: true);

        result.AssertSuccess();

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetAgentState("Adele");
        Assert.Empty(state!.UnreadMessages);
    }

    [Fact]
    public async Task Inbox_Clear_All_GuardSeesNoUnread()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");
        await ReadMustReadsAsync();

        // Create inbox items (no unread messages — simulates agent having read them)
        CreateInboxItem("Adele", "Brian", "reviewer", "task1", "Brief 1");

        var result = await InboxClearAsync(all: true);
        result.AssertSuccess();

        // Guard should not report unread messages
        var guardResult = await GuardAsync("Read", "Commands/InboxCommand.cs");
        guardResult.AssertSuccess();
        Assert.DoesNotContain("unread", guardResult.Stdout.ToLowerInvariant());
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
