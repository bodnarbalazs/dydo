namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;

/// <summary>
/// Integration tests for dispatch command --to and --escalate flags.
/// </summary>
[Collection("Integration")]
public class DispatchCommandTests : IntegrationTestBase
{
    #region --to Success Cases

    [Fact]
    public async Task Dispatch_ToValidAgent_DispatchesToSpecifiedAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Implement feature", to: "Brian");

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");

        // Verify inbox file created for Brian specifically
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0, "Inbox item should be created for Brian");

        // Verify NOT created for Adele (first alphabetically)
        var adeleInbox = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        if (Directory.Exists(adeleInbox))
        {
            var adeleFiles = Directory.GetFiles(adeleInbox, "*.md");
            Assert.Empty(adeleFiles);
        }
    }

    [Fact]
    public async Task Dispatch_WithoutTo_AutoSelectsFirstFreeAlphabetically()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Implement feature");

        result.AssertSuccess();
        result.AssertStdoutContains("Adele"); // First alphabetically

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0);
    }

    #endregion

    #region --to Error Cases

    [Fact]
    public async Task Dispatch_ToNonExistentAgent_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Zorro");

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");
    }

    [Fact]
    public async Task Dispatch_ToAgentAssignedToDifferentHuman_Fails()
    {
        // Init project with alice's agents
        await InitProjectAsync("none", "alice", 3);

        // Switch to bob's context
        SetHuman("bob");

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Adele");

        result.AssertExitCode(2);
        result.AssertStderrContains("not assigned to you");
    }

    [Fact]
    public async Task Dispatch_ToBusyAgent_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Claim Adele (makes it Working status)
        await ClaimAgentAsync("Adele");

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Adele");

        result.AssertExitCode(2);
        result.AssertStderrContains("not free");
    }

    #endregion

    #region --escalate Tests

    [Fact]
    public async Task Dispatch_WithEscalate_SetsEscalatedFlag()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Urgent fix needed", escalate: true);

        result.AssertSuccess();
        result.AssertStdoutContains("[ESCALATED]");

        // Verify inbox file contains escalation fields
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0);

        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("escalated: true", content);
        Assert.Contains("escalated_at:", content);
    }

    [Fact]
    public async Task Dispatch_WithEscalate_InboxHeaderShowsEscalated()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "urgent-fix", "Fix this now", escalate: true);

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var content = File.ReadAllText(inboxFiles[0]);

        // Header should have ESCALATED prefix
        Assert.Contains("# ESCALATED CODE-WRITER Request: urgent-fix", content);
    }

    [Fact]
    public async Task Dispatch_WithoutEscalate_NoEscalationInFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "normal-task", "Normal work");

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var content = File.ReadAllText(inboxFiles[0]);

        Assert.DoesNotContain("escalated:", content);
        Assert.DoesNotContain("ESCALATED", content);
    }

    #endregion

    #region Inbox Display Tests

    [Fact]
    public async Task InboxShow_DisplaysEscalatedIndicator()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create an escalated inbox item
        CreateEscalatedInboxItem("Adele", "Brian", "code-writer", "urgent-task", "Urgent work");

        // Claim Adele to view inbox
        await ClaimAgentAsync("Adele");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("[ESCALATED]");
    }

    [Fact]
    public async Task InboxShow_NonEscalatedItem_NoIndicator()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create a non-escalated inbox item
        CreateInboxItem("Adele", "Brian", "code-writer", "normal-task", "Normal work");

        await ClaimAgentAsync("Adele");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        Assert.DoesNotContain("[ESCALATED]", result.Stdout);
    }

    #endregion

    #region Combined Flag Tests

    [Fact]
    public async Task Dispatch_ToValidAgent_WithEscalate_BothWork()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "critical-fix", "Critical bug", to: "Brian", escalate: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");
        result.AssertStdoutContains("[ESCALATED]");

        // Verify dispatched to Brian
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0);

        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("escalated: true", content);
    }

    [Fact]
    public async Task Dispatch_ToInvalidAgent_WithEscalate_FailsBeforeEscalation()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "InvalidAgent", escalate: true);

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");

        // Verify no inbox files created anywhere
        foreach (var agentName in new[] { "Adele", "Brian", "Charlie" })
        {
            var inboxPath = Path.Combine(TestDir, "dydo/agents", agentName, "inbox");
            if (Directory.Exists(inboxPath))
            {
                var files = Directory.GetFiles(inboxPath, "*.md");
                Assert.Empty(files);
            }
        }
    }

    #endregion

    #region CanTakeRole at Dispatch Time

    [Fact]
    public async Task Dispatch_ReviewerToCodeWriter_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Make Adele the code-writer on "auth" by writing task role history into state
        SetTaskRoleHistory("Adele", "auth", "code-writer");

        var result = await DispatchAsync("reviewer", "auth", "Review this code", to: "Adele");

        result.AssertExitCode(2);
        result.AssertStderrContains("code-writer");
    }

    [Fact]
    public async Task Dispatch_AutoSelect_SkipsIneligibleAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Make Adele the code-writer on "auth" — she can't be reviewer
        SetTaskRoleHistory("Adele", "auth", "code-writer");

        var result = await DispatchAsync("reviewer", "auth", "Review this code");

        result.AssertSuccess();
        // Should skip Adele (ineligible) and select Brian (next alphabetically)
        result.AssertStdoutContains("Brian");
    }

    #endregion

    #region Auto-Return Routing

    [Fact]
    public async Task Dispatch_WithoutTo_ReturnsToOriginAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Simulate: Adele dispatched to Brian, so Brian has an inbox item with origin: Adele
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");

        // Brian is the sender (claimed), dispatching back without --to
        await ClaimAgentAsync("Brian");

        var result = await DispatchAsync("code-writer", "auth", "Review failed. Fix issues.");

        result.AssertSuccess();
        // Should auto-return to Adele (the origin)
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Dispatch_WithoutTo_FallsBackToAlphabetical_WhenOriginBusy()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Simulate: Adele dispatched to Brian, so Brian has an inbox item with origin: Adele
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");

        // Brian is claimed (sender)
        await ClaimAgentAsync("Brian");

        // Adele is also busy (claimed by someone else)
        var registry = new DynaDocs.Services.AgentRegistry(TestDir);
        registry.StorePendingSessionId("Adele", "other-session");
        registry.StoreSessionContext("other-session");
        var adeleCmd = DynaDocs.Commands.AgentCommand.Create();
        await RunAsync(adeleCmd, "claim", "Adele");

        // Restore session context for Brian
        StoreSessionContext();

        var result = await DispatchAsync("code-writer", "auth", "Review failed. Fix issues.");

        result.AssertSuccess();
        // Adele is busy, so should fall through to alphabetical — Charlie is next free
        result.AssertStdoutContains("Charlie");
    }

    [Fact]
    public async Task Dispatch_WithoutTo_OriginFromArchive_ReturnsToOriginAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Simulate: Adele dispatched to Brian, Brian cleared inbox (moved to archive)
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");

        // Move item from inbox to archive/inbox (simulates `inbox clear`)
        var brianInboxPath = Path.Combine(TestDir, "dydo", "agents", "Brian", "inbox");
        var brianArchivePath = Path.Combine(TestDir, "dydo", "agents", "Brian", "archive", "inbox");
        Directory.CreateDirectory(brianArchivePath);
        foreach (var file in Directory.GetFiles(brianInboxPath, "*.md"))
        {
            File.Move(file, Path.Combine(brianArchivePath, Path.GetFileName(file)));
        }

        // Brian is the sender (claimed), dispatching back without --to
        await ClaimAgentAsync("Brian");

        var result = await DispatchAsync("code-writer", "auth", "Review failed. Fix issues.");

        result.AssertSuccess();
        // Should still find origin from archive and auto-return to Adele
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Dispatch_OriginPropagatesAcrossMultipleHops()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Hop 1: Adele dispatches to Brian (origin: Adele)
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");

        // Brian claims and dispatches explicitly to Charlie — origin should carry forward
        await ClaimAgentAsync("Brian");
        var result = await DispatchAsync("reviewer", "auth", "Please review", to: "Charlie");
        result.AssertSuccess();

        // Verify origin: Adele is in Charlie's inbox (not origin: Brian)
        var charlieInbox = Path.Combine(TestDir, "dydo", "agents", "Charlie", "inbox");
        Assert.True(Directory.Exists(charlieInbox), "Charlie should have an inbox");
        var charlieFiles = Directory.GetFiles(charlieInbox, "*-auth.md");
        Assert.Single(charlieFiles);

        var content = File.ReadAllText(charlieFiles[0]);
        Assert.Contains("origin: Adele", content);
        Assert.Contains("from: Brian", content);
    }

    [Fact]
    public async Task Dispatch_CodeWriterToFormerReviewer_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Make Adele a former reviewer on "auth" — should NOT block code-writer dispatch
        SetTaskRoleHistory("Adele", "auth", "reviewer");

        var result = await DispatchAsync("code-writer", "auth", "Implement this feature", to: "Adele");

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
    }

    #endregion

    #region Origin in Inbox File

    [Fact]
    public async Task Dispatch_WritesOriginToInboxFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Implement feature", to: "Brian");

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.Single(inboxFiles);

        var content = File.ReadAllText(inboxFiles[0]);
        // Origin should be present — sender is Unknown (no claimed agent) so origin is Unknown
        Assert.Contains("origin:", content);
    }

    [Fact]
    public async Task InboxShow_DisplaysOriginWhenDifferentFromSender()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create inbox item where origin differs from sender
        CreateInboxItemWithOrigin("Adele", "Brian", "Zara", "code-writer", "auth", "Fix review issues");

        await ClaimAgentAsync("Adele");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("From: Brian");
        result.AssertStdoutContains("Origin: Zara");
    }

    [Fact]
    public async Task InboxShow_HidesOriginWhenSameAsSender()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Create inbox item where origin == sender (first dispatch)
        CreateInboxItemWithOrigin("Adele", "Brian", "Brian", "code-writer", "auth", "Implement auth");

        await ClaimAgentAsync("Adele");

        var result = await InboxShowAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("From: Brian");
        Assert.DoesNotContain("Origin:", result.Stdout);
    }

    #endregion

    #region Double-Dispatch Race Condition Tests

    [Fact]
    public async Task Dispatch_ToSameAgentTwice_SecondFails()
    {
        await InitProjectAsync("none", "testuser", 3);

        // First dispatch to Brian succeeds
        var result1 = await DispatchAsync("code-writer", "task-1", "First dispatch", to: "Brian");
        result1.AssertSuccess();
        result1.AssertStdoutContains("Brian");

        // Second dispatch to Brian fails (status: dispatched)
        var result2 = await DispatchAsync("code-writer", "task-2", "Second dispatch", to: "Brian");
        result2.AssertExitCode(2);
        result2.AssertStderrContains("not free");
    }

    [Fact]
    public async Task Dispatch_AutoSelect_SkipsDispatchedAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        // First auto-dispatch selects Adele (first alphabetically)
        var result1 = await DispatchAsync("code-writer", "task-1", "First dispatch");
        result1.AssertSuccess();
        result1.AssertStdoutContains("Adele");

        // Second auto-dispatch should skip Adele (dispatched) and select Brian
        var result2 = await DispatchAsync("code-writer", "task-2", "Second dispatch");
        result2.AssertSuccess();
        result2.AssertStdoutContains("Brian");

        // Verify Adele was NOT selected for second dispatch
        Assert.DoesNotContain("Adele", result2.Stdout.Split("Brian")[0].Length > 0 ? result2.Stdout : "");

        // Verify both inbox items exist
        var adeleInbox = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var brianInbox = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.Single(adeleInbox);
        Assert.Single(brianInbox);
    }

    #endregion

    #region --tab / --new-window Tests

    [Fact]
    public async Task Dispatch_TabAndNewWindow_BothSpecified_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", tab: true, newWindow: true);

        result.AssertExitCode(2);
        result.AssertStderrContains("Cannot specify both --tab and --new-window");
    }

    [Fact]
    public async Task Dispatch_WithTabOnly_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", tab: true);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Dispatch_WithNewWindowOnly_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", newWindow: true);

        result.AssertSuccess();
    }

    #endregion

    #region Reviewer Auto-Transition Tests

    [Fact]
    public async Task Dispatch_ToReviewer_TransitionsTaskToReviewPending()
    {
        await InitProjectAsync("none", "testuser", 3);
        await TaskCreateAsync("review-task", area: "backend");

        var result = await DispatchAsync("reviewer", "review-task", "Implemented OAuth flow");

        result.AssertSuccess();
        result.AssertStdoutContains("Task state: marked ready for review");

        // Verify task file status changed to review-pending
        var taskContent = ReadFile("dydo/project/tasks/review-task.md");
        Assert.Contains("status: review-pending", taskContent);

        // Verify brief became the review summary
        Assert.Contains("## Review Summary", taskContent);
        Assert.Contains("Implemented OAuth flow", taskContent);
        Assert.DoesNotContain("(Pending)", taskContent);
    }

    [Fact]
    public async Task Dispatch_ToNonReviewer_DoesNotTransitionTask()
    {
        await InitProjectAsync("none", "testuser", 3);
        await TaskCreateAsync("code-task", area: "backend");

        var result = await DispatchAsync("code-writer", "code-task", "Implement feature");

        result.AssertSuccess();
        Assert.DoesNotContain("Task state:", result.Stdout);

        // Verify task file status remains pending
        var taskContent = ReadFile("dydo/project/tasks/code-task.md");
        Assert.Contains("status: pending", taskContent);
    }

    [Fact]
    public async Task Dispatch_ToReviewer_WithoutTaskFile_StillSucceeds()
    {
        await InitProjectAsync("none", "testuser", 3);

        // No task file created — dispatch should still succeed
        var result = await DispatchAsync("reviewer", "nonexistent-task", "Review this");

        result.AssertSuccess();
        Assert.DoesNotContain("Task state:", result.Stdout);
    }

    [Fact]
    public async Task Dispatch_ToReviewer_ReviewFailedToReviewPending()
    {
        await InitProjectAsync("none", "testuser", 3);
        await TaskCreateAsync("failed-task", area: "backend");

        // Manually set task status to review-failed (simulates a failed review)
        var taskPath = Path.Combine(TestDir, "dydo/project/tasks/failed-task.md");
        var content = File.ReadAllText(taskPath);
        content = content.Replace("status: pending", "status: review-failed");
        File.WriteAllText(taskPath, content);

        var result = await DispatchAsync("reviewer", "failed-task", "Fixed issues, ready for re-review");

        result.AssertSuccess();
        result.AssertStdoutContains("Task state: marked ready for review");

        // Verify task transitions from review-failed to review-pending
        var taskContent = ReadFile("dydo/project/tasks/failed-task.md");
        Assert.Contains("status: review-pending", taskContent);
    }

    #endregion

    #region --agent Alias Tests

    [Fact]
    public async Task Dispatch_AgentAlias_DispatchesToSpecifiedAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchWithAgentAliasAsync("code-writer", "my-task", "Implement feature", agent: "Brian");

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        Assert.True(inboxFiles.Length > 0, "Inbox item should be created for Brian");
    }

    [Fact]
    public async Task Dispatch_AgentAlias_NonExistentAgent_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchWithAgentAliasAsync("code-writer", "my-task", "Brief", agent: "Zorro");

        result.AssertExitCode(2);
        result.AssertStderrContains("does not exist");
    }

    [Fact]
    public async Task Dispatch_AgentAlias_ProducesSameResultAsTo()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Dispatch with --agent to Brian
        var agentResult = await DispatchWithAgentAliasAsync("code-writer", "task-a", "Brief A", agent: "Brian");
        agentResult.AssertSuccess();

        // Dispatch with --to to Charlie
        var toResult = await DispatchAsync("code-writer", "task-b", "Brief B", to: "Charlie");
        toResult.AssertSuccess();

        // Both should have inbox items with same structure
        var brianFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Brian/inbox"), "*.md");
        var charlieFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Charlie/inbox"), "*.md");
        Assert.Single(brianFiles);
        Assert.Single(charlieFiles);

        var brianContent = File.ReadAllText(brianFiles[0]);
        var charlieContent = File.ReadAllText(charlieFiles[0]);

        // Both should have same structure (role, from, brief section)
        Assert.Contains("role: code-writer", brianContent);
        Assert.Contains("role: code-writer", charlieContent);
    }

    #endregion

    #region Filename Sanitization Tests

    [Fact]
    public async Task Dispatch_TaskNameWithColon_CreatesInboxFileWithSanitizedName()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "Review Coordinator: Auth", "Implement feature");

        result.AssertSuccess();
        result.AssertStdoutContains("Warning: Task name sanitized");

        // Verify inbox file was created with sanitized filename
        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.Single(inboxFiles);
        Assert.Contains("Review Coordinator- Auth", Path.GetFileName(inboxFiles[0]));

        // Verify original task name preserved in file content
        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("task: Review Coordinator: Auth", content);
    }

    [Fact]
    public async Task Dispatch_TaskNameWithColon_InboxShowDisplaysCorrectly()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "Review Coordinator: Auth", "Implement feature", to: "Brian");
        result.AssertSuccess();

        // Claim Brian and check inbox
        await ClaimAgentAsync("Brian");
        var inboxResult = await InboxShowAsync();

        inboxResult.AssertSuccess();
        inboxResult.AssertStdoutContains("Review Coordinator: Auth");
    }

    #endregion

    #region --brief-file Tests

    [Fact]
    public async Task Dispatch_WithBriefFile_ReadsBriefFromFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        var briefPath = Path.Combine(TestDir, "brief.txt");
        File.WriteAllText(briefPath, "This is a brief from a file.");

        var result = await DispatchWithBriefFileAsync("code-writer", "my-task", briefPath);

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        Assert.Single(inboxFiles);

        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("This is a brief from a file.", content);
    }

    [Fact]
    public async Task Dispatch_WithBriefFileNotFound_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchWithBriefFileAsync("code-writer", "my-task", "/nonexistent/brief.txt");

        result.AssertExitCode(2);
        result.AssertStderrContains("Brief file not found");
    }

    [Fact]
    public async Task Dispatch_WithNeitherBriefNorBriefFile_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "my-task", "--no-launch", "--no-wait" };
        var result = await RunAsync(command, args);

        result.AssertExitCode(2);
        result.AssertStderrContains("Provide --brief or --brief-file");
    }

    [Fact]
    public async Task Dispatch_WithBothBriefAndBriefFile_BriefFileWins()
    {
        await InitProjectAsync("none", "testuser", 3);

        var briefPath = Path.Combine(TestDir, "brief.txt");
        File.WriteAllText(briefPath, "Brief from file.");

        var command = DispatchCommand.Create();
        var args = new[]
        {
            "--role", "code-writer",
            "--task", "my-task",
            "--brief", "Inline brief",
            "--brief-file", briefPath,
            "--no-launch",
            "--no-wait"
        };
        var result = await RunAsync(command, args);

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("Brief from file.", content);
    }

    #endregion

    #region --auto-close Tests

    [Fact]
    public async Task Dispatch_WithAutoClose_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", autoClose: true);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Dispatch_WithAutoClose_CreatesMarkerFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", autoClose: true);

        result.AssertSuccess();

        // Find which agent was selected (first free = Adele)
        var markerPath = Path.Combine(TestDir, "dydo/agents/Adele/.auto-close");
        Assert.True(File.Exists(markerPath), "Auto-close marker file should exist");
    }

    [Fact]
    public async Task Dispatch_WithoutAutoClose_NoMarkerFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief");

        result.AssertSuccess();

        var markerPath = Path.Combine(TestDir, "dydo/agents/Adele/.auto-close");
        Assert.False(File.Exists(markerPath), "Auto-close marker file should not exist");
    }

    [Fact]
    public async Task Dispatch_AutoClose_CombinesWithTab()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", tab: true, autoClose: true);

        result.AssertSuccess();

        var markerPath = Path.Combine(TestDir, "dydo/agents/Adele/.auto-close");
        Assert.True(File.Exists(markerPath));
    }

    [Fact]
    public async Task Dispatch_AutoClose_CombinesWithEscalate()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", escalate: true, autoClose: true);

        result.AssertSuccess();
        result.AssertStdoutContains("[ESCALATED]");

        var markerPath = Path.Combine(TestDir, "dydo/agents/Adele/.auto-close");
        Assert.True(File.Exists(markerPath));
    }

    [Fact]
    public async Task Dispatch_AutoClose_CombinesWithTo()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Brian", autoClose: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");

        var markerPath = Path.Combine(TestDir, "dydo/agents/Brian/.auto-close");
        Assert.True(File.Exists(markerPath));
    }

    #endregion

    #region Shell Metacharacter Rejection Tests

    [Theory]
    [InlineData("Fixed hang when chaining dydo whoami && dydo agent status", "&&")]
    [InlineData("Check this || that condition", "||")]
    [InlineData("Run $(whoami) to find user", "$(")]
    [InlineData("Use `command` for output", "`")]
    [InlineData("Path is ${HOME}/projects", "${")]
    public async Task Dispatch_BriefWithShellMetacharacters_Fails(string brief, string expectedPattern)
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", brief);

        result.AssertExitCode(2);
        result.AssertStderrContains(expectedPattern);
        result.AssertStderrContains("--brief-file");
    }

    [Theory]
    [InlineData("Simple brief without special chars")]
    [InlineData("Fixed the login & signup flow")]
    [InlineData("Added retry logic (3 attempts max)")]
    [InlineData("Refactored auth module - see PR #42")]
    [InlineData("Fixed all 5 review issues: (1) Removed dead code ternary in TemplateCommand.cs:157. (2) Simplified logic.")]
    [InlineData("Fixed all 5 review issues. 1 Removed dead code ternary in TemplateCommand.cs line 157. 2 Simplified.")]
    [InlineData("Implemented OAuth flow; added token refresh")]
    [InlineData("See file Services/Auth.cs for details")]
    public async Task Dispatch_BriefWithSafeContent_Succeeds(string brief)
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", brief);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Dispatch_BriefFile_BypassesShellMetacharacterCheck()
    {
        await InitProjectAsync("none", "testuser", 3);

        var briefPath = Path.Combine(TestDir, "brief.txt");
        File.WriteAllText(briefPath, "Fixed hang when chaining dydo whoami && dydo agent status");

        var result = await DispatchWithBriefFileAsync("code-writer", "my-task", briefPath);

        result.AssertSuccess();

        var inboxFiles = Directory.GetFiles(Path.Combine(TestDir, "dydo/agents/Adele/inbox"), "*.md");
        var content = File.ReadAllText(inboxFiles[0]);
        Assert.Contains("&&", content);
    }

    #endregion

    #region --worktree Tests

    [Fact]
    public async Task Dispatch_WithWorktreeAndNoLaunch_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "my-task", "--brief", "Brief", "--worktree", "--no-launch", "--no-wait" };
        var result = await RunAsync(command, args);

        result.AssertExitCode(2);
        result.AssertStderrContains("Cannot specify both --worktree and --no-launch");
    }

    [Fact]
    public async Task Dispatch_WithWorktree_CreatesMarkerFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", worktree: true);

        result.AssertSuccess();

        var markerPath = Path.Combine(TestDir, "dydo/agents/Adele/.worktree");
        Assert.True(File.Exists(markerPath), "Worktree marker file should exist");

        var content = File.ReadAllText(markerPath);
        Assert.StartsWith("Adele-", content);
    }

    [Fact]
    public async Task Dispatch_WithoutWorktree_NoWorktreeMarkerFile()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief");

        result.AssertSuccess();

        var markerPath = Path.Combine(TestDir, "dydo/agents/Adele/.worktree");
        Assert.False(File.Exists(markerPath), "Worktree marker file should not exist");
    }

    [Fact]
    public async Task Dispatch_WithWorktree_CombinesWithAutoClose()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", worktree: true, autoClose: true);

        result.AssertSuccess();

        var wtMarker = Path.Combine(TestDir, "dydo/agents/Adele/.worktree");
        var acMarker = Path.Combine(TestDir, "dydo/agents/Adele/.auto-close");
        Assert.True(File.Exists(wtMarker));
        Assert.True(File.Exists(acMarker));
    }

    [Fact]
    public async Task Dispatch_WithWorktree_CombinesWithTab()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", worktree: true, tab: true);

        result.AssertSuccess();

        var markerPath = Path.Combine(TestDir, "dydo/agents/Adele/.worktree");
        Assert.True(File.Exists(markerPath));
    }

    [Fact]
    public async Task Dispatch_WithWorktree_CombinesWithTo()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", to: "Brian", worktree: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");

        var markerPath = Path.Combine(TestDir, "dydo/agents/Brian/.worktree");
        Assert.True(File.Exists(markerPath));
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

    private async Task<CommandResult> DispatchAsync(
        string role,
        string task,
        string brief,
        string? files = null,
        string? to = null,
        bool escalate = false,
        bool tab = false,
        bool newWindow = false,
        bool autoClose = false,
        bool noWait = true,
        bool wait = false,
        bool worktree = false)
    {
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief", brief,
        };

        // --worktree is incompatible with --no-launch, so omit --no-launch when worktree is set
        if (!worktree) { args.Add("--no-launch"); }

        if (files != null) { args.Add("--files"); args.Add(files); }
        if (to != null) { args.Add("--to"); args.Add(to); }
        if (escalate) { args.Add("--escalate"); }
        if (tab) { args.Add("--tab"); }
        if (newWindow) { args.Add("--new-window"); }
        if (autoClose) { args.Add("--auto-close"); }
        if (worktree) { args.Add("--worktree"); }
        if (wait) { args.Add("--wait"); }
        else if (noWait) { args.Add("--no-wait"); }

        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> DispatchWithAgentAliasAsync(
        string role,
        string task,
        string brief,
        string? agent = null)
    {
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief", brief,
            "--no-launch",
            "--no-wait"
        };

        if (agent != null) { args.Add("--agent"); args.Add(agent); }

        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> DispatchWithBriefFileAsync(
        string role,
        string task,
        string briefFile)
    {
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief-file", briefFile,
            "--no-launch",
            "--no-wait"
        };

        return await RunAsync(command, args.ToArray());
    }

    private async Task<CommandResult> InboxShowAsync()
    {
        var command = InboxCommand.Create();
        return await RunAsync(command, "show");
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

    private void CreateInboxItemWithOrigin(string agentName, string fromAgent, string origin, string role, string task, string brief)
    {
        var inboxPath = Path.Combine(TestDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);

        var id = Guid.NewGuid().ToString("N")[..8];
        var content = $"""
            ---
            id: {id}
            from: {fromAgent}
            origin: {origin}
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

    private void SetTaskRoleHistory(string agentName, string task, string role)
    {
        var statePath = Path.Combine(TestDir, "dydo", "agents", agentName, "state.md");
        if (File.Exists(statePath))
        {
            var content = File.ReadAllText(statePath);
            // Replace the task-role-history line
            content = System.Text.RegularExpressions.Regex.Replace(
                content,
                @"^task-role-history:.*$",
                $"task-role-history: {{ \"{task}\": [\"{role}\"] }}",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            File.WriteAllText(statePath, content);
        }
        else
        {
            // Create a minimal state file with task role history
            var workspace = Path.Combine(TestDir, "dydo", "agents", agentName);
            Directory.CreateDirectory(workspace);
            var historyValue = $"{{ \"{task}\": [\"{role}\"] }}";
            var content = $"""
                ---
                agent: {agentName}
                role: null
                task: null
                status: free
                assigned: testuser
                started: null
                writable-paths: []
                readonly-paths: []
                unread-must-reads: []
                task-role-history: {historyValue}
                ---

                # {agentName} — Session State
                """;
            File.WriteAllText(statePath, content);
        }
    }

    private void CreateEscalatedInboxItem(string agentName, string fromAgent, string role, string task, string brief)
    {
        var inboxPath = Path.Combine(TestDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);

        var id = Guid.NewGuid().ToString("N")[..8];
        var escalatedAt = DateTime.UtcNow;
        var content = $"""
            ---
            id: {id}
            from: {fromAgent}
            role: {role}
            task: {task}
            received: {DateTime.UtcNow:o}
            escalated: true
            escalated_at: {escalatedAt:o}
            ---

            # ESCALATED {role.ToUpperInvariant()} Request: {task}

            ## From

            {fromAgent}

            ## Brief

            {brief}
            """;

        File.WriteAllText(Path.Combine(inboxPath, $"{id}-{task}.md"), content);
    }

    #endregion
}
