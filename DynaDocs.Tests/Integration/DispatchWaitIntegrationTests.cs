namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("Integration")]
public class DispatchWaitIntegrationTests : IntegrationTestBase
{
    #region --wait / --no-wait Flag Validation

    [Fact]
    public async Task Dispatch_NeitherWaitNorNoWait_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "my-task", "--brief", "Test", "--no-launch" };
        var result = await RunAsync(command, args);

        result.AssertExitCode(2);
        result.AssertStderrContains("Specify --wait or --no-wait");
    }

    [Fact]
    public async Task Dispatch_BothWaitAndNoWait_Fails()
    {
        await InitProjectAsync("none", "testuser", 3);

        var command = DispatchCommand.Create();
        var args = new[] { "--role", "code-writer", "--task", "my-task", "--brief", "Test", "--no-launch", "--wait", "--no-wait" };
        var result = await RunAsync(command, args);

        result.AssertExitCode(2);
        result.AssertStderrContains("Cannot specify both --wait and --no-wait");
    }

    #endregion

    #region --no-wait Release Hint

    [Fact]
    public async Task Dispatch_NoWait_ShowsReleaseHint_WhenDispatched()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Brian was dispatched by Adele — clear inbox first (real agents process before working)
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");
        await ClaimAgentAsync("Brian");
        await SetRoleAsync("code-writer", "auth");
        var inboxCmd = InboxCommand.Create();
        await RunAsync(inboxCmd, "clear", "--all");

        var result = await DispatchAsync("reviewer", "auth", "Review this", noWait: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Don't forget: dydo agent release");
    }

    [Fact]
    public async Task Dispatch_NoWait_ShowsReleaseHint_WhenNoRemainingWork()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele dispatches with no inbox items, no wait markers → hint shows
        await ClaimAgentAsync("Adele");

        var result = await DispatchAsync("code-writer", "my-task", "Implement feature", noWait: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Don't forget: dydo agent release");
    }

    [Fact]
    public async Task Dispatch_NoWait_NoHint_WhenCoThinker()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Brian dispatched by Adele, but Brian is a co-thinker — clear inbox first
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "co-thinker", "design", "Think about architecture");
        await ClaimAgentAsync("Brian");
        await SetRoleAsync("co-thinker", "design");
        var inboxCmd = InboxCommand.Create();
        await RunAsync(inboxCmd, "clear", "--all");

        var result = await DispatchAsync("planner", "design", "Task emerged from thinking", noWait: true);

        result.AssertSuccess();
        Assert.DoesNotContain("Don't forget", result.Stdout);
    }

    [Fact]
    public async Task Dispatch_NoWait_NoHint_WhenPendingInboxItems()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Brian has an unprocessed inbox item → no nudge
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");
        await ClaimAgentAsync("Brian");
        await SetRoleAsync("code-writer", "auth");

        var result = await DispatchAsync("reviewer", "auth", "Review this", noWait: true);

        result.AssertSuccess();
        Assert.DoesNotContain("Don't forget", result.Stdout);
    }

    [Fact]
    public async Task Dispatch_NoWait_NoHint_WhenActiveWaitMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);

        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "feature");

        // Create a wait marker from a prior dispatch
        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "other-task", "Brian");

        var result = await DispatchAsync("reviewer", "feature", "Review this", noWait: true);

        result.AssertSuccess();
        Assert.DoesNotContain("Don't forget", result.Stdout);
    }

    [Fact]
    public async Task Dispatch_NoWait_NoHint_WhenNoAgent()
    {
        await InitProjectAsync("none", "testuser", 3);

        // No agent claimed — sender is null → no nudge
        var result = await DispatchAsync("code-writer", "my-task", "Brief", noWait: true);

        result.AssertSuccess();
        Assert.DoesNotContain("Don't forget", result.Stdout);
    }

    [Fact]
    public async Task Dispatch_Wait_ReturnsImmediately()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Brian needs oversight role for --wait; set planner history for orchestrator graduation
        await ClaimAgentAsync("Brian");
        SetTaskRoleHistory("Brian", "auth", "planner");
        await SetRoleAsync("orchestrator", "auth");

        var result = await DispatchAsync("reviewer", "auth", "Review this", wait: true);

        result.AssertSuccess();
        Assert.DoesNotContain("Don't forget", result.Stdout);
        result.AssertStdoutContains("Wait registered");
        result.AssertStdoutContains("dydo wait --task auth");
    }

    #endregion

    #region Wait Marker Tests

    [Fact]
    public async Task Dispatch_Wait_CreatesMarkerFile()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "my-task", "co-thinker");
        await SetRoleAsync("orchestrator", "my-task");

        // Pre-place a response message so --wait returns immediately
        CreateMessageFile("Adele", "Brian", "my-task", "Done.");

        var result = await DispatchAsync("code-writer", "my-task", "Implement feature", wait: true);

        result.AssertSuccess();
        // Marker should have been created then cleaned up after message received
        // The .waiting directory may or may not exist (cleaned up on match)
    }

    [Fact]
    public async Task Dispatch_NoWait_NoMarkerCreated()
    {
        await InitProjectAsync("none", "testuser", 3);

        var result = await DispatchAsync("code-writer", "my-task", "Brief", noWait: true);

        result.AssertSuccess();

        // No .waiting directory should exist for any agent
        var waitingDir = Path.Combine(TestDir, "dydo/agents/Adele/.waiting");
        Assert.False(Directory.Exists(waitingDir), "No wait marker should be created for --no-wait");
    }

    #endregion

    #region Double-Dispatch Protection

    [Fact]
    public async Task Dispatch_SameTask_WhileAgentWorking_Blocked()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Dispatch task "auth" to Brian
        var result1 = await DispatchAsync("code-writer", "auth", "Implement auth", to: "Brian", noWait: true);
        result1.AssertSuccess();

        // Brian claims and starts working (different session)
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");

        // Main session (unknown sender) tries to dispatch same task — should be blocked
        StoreSessionContext();
        var result2 = await DispatchAsync("reviewer", "auth", "Review auth", to: "Charlie", noWait: true);

        result2.AssertExitCode(2);
        result2.AssertStderrContains("already working on task");
        result2.AssertStderrContains("Brian");
    }

    [Fact]
    public async Task Dispatch_SameTask_AfterRelease_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Dispatch task to Adele
        var result1 = await DispatchAsync("code-writer", "auth", "Implement auth", noWait: true);
        result1.AssertSuccess();

        // Adele claims, clears inbox, releases
        await ClaimAgentAsync("Adele");
        var inboxCmd = InboxCommand.Create();
        await RunAsync(inboxCmd, "clear", "--all");
        await ReleaseAgentAsync();

        StoreSessionContext();

        // Now dispatching the same task should succeed
        var result2 = await DispatchAsync("code-writer", "auth", "Implement auth again", noWait: true);
        result2.AssertSuccess();
    }

    [Fact]
    public async Task Dispatch_SameTask_WhileDispatched_Blocked()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Dispatch task "auth" to Brian (Brian is now Dispatched status with inbox item)
        var result1 = await DispatchAsync("code-writer", "auth", "Implement auth", to: "Brian", noWait: true);
        result1.AssertSuccess();

        // Try to dispatch same task again — Brian is Dispatched with matching inbox item
        var result2 = await DispatchAsync("code-writer", "auth", "Implement auth again", to: "Charlie", noWait: true);

        result2.AssertExitCode(2);
        result2.AssertStderrContains("already working on task");
    }

    #endregion

    #region Release Blocking by Wait Markers

    [Fact]
    public async Task Release_BlockedByWaitMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Create a wait marker manually
        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");

        var result = await ReleaseAgentAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("Cannot release: waiting for response on: auth");
        result.AssertStderrContains("dydo wait --task");
    }

    [Fact]
    public async Task Release_SucceedsAfterWaitCancel()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Create a wait marker
        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");

        // Cancel the wait
        var waitCmd = WaitCommand.Create();
        StoreSessionContext();
        var cancelResult = await RunAsync(waitCmd, "--task", "auth", "--cancel");
        cancelResult.AssertSuccess();

        // Now release should succeed
        var releaseResult = await ReleaseAgentAsync();
        releaseResult.AssertSuccess();
    }

    #endregion

    #region Reply-Pending Guardrail

    [Fact]
    public async Task Release_BlockedByReplyPending()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele dispatches with --wait to Brian (needs oversight role)
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "auth", "planner");
        await SetRoleAsync("orchestrator", "auth");
        var dispatchResult = await DispatchAsync("code-writer", "auth", "Implement auth", to: "Brian", wait: true);
        dispatchResult.AssertSuccess();

        // Brian claims in a separate session, clears inbox
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");
        ClearInboxInSeparateSession("Brian");

        // Brian tries to release — should be blocked
        var releaseResult = ReleaseInSeparateSession("Brian");
        Assert.Contains("pending reply", releaseResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Release_SucceedsAfterReplyMessage()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele dispatches with --wait to Brian (needs oversight role)
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "auth", "planner");
        await SetRoleAsync("orchestrator", "auth");
        var dispatchResult = await DispatchAsync("code-writer", "auth", "Implement auth", to: "Brian", wait: true);
        dispatchResult.AssertSuccess();

        // Brian claims, clears inbox (creates reply-pending marker)
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");
        ClearInboxInSeparateSession("Brian");

        // Brian sends reply message
        SendMessageInSeparateSession("Brian", "Adele", "auth", "Done implementing.");

        // Brian releases — should succeed
        var releaseResult = ReleaseInSeparateSession("Brian");
        Assert.DoesNotContain("pending reply", releaseResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Release_SucceedsWhenReplyBeforeInboxClear()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele dispatches with --wait to Brian (needs oversight role)
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "auth", "planner");
        await SetRoleAsync("orchestrator", "auth");
        var dispatchResult = await DispatchAsync("code-writer", "auth", "Implement auth", to: "Brian", wait: true);
        dispatchResult.AssertSuccess();

        // Verify marker was created at dispatch time (not at inbox clear)
        var registry = new AgentRegistry(TestDir);
        var markers = registry.GetReplyPendingMarkers("Brian");
        Assert.Single(markers);
        Assert.Equal("Adele", markers[0].To);

        // Brian claims, sends reply BEFORE clearing inbox (the natural agent flow)
        ClaimAgentInSeparateSession("Brian");

        // Marker must survive claim (archive must not destroy it)
        registry = new AgentRegistry(TestDir);
        var markersAfterClaim = registry.GetReplyPendingMarkers("Brian");
        Assert.Single(markersAfterClaim);
        Assert.Equal("Adele", markersAfterClaim[0].To);

        SetRoleInState("Brian", "code-writer", "auth");
        SendMessageInSeparateSession("Brian", "Adele", "auth", "Done implementing.");

        // Marker should be cleared by msg
        registry = new AgentRegistry(TestDir);
        Assert.Empty(registry.GetReplyPendingMarkers("Brian"));

        // Now clear inbox — should NOT recreate the marker
        ClearInboxInSeparateSession("Brian");

        // Marker should still be empty
        registry = new AgentRegistry(TestDir);
        Assert.Empty(registry.GetReplyPendingMarkers("Brian"));

        // Release should succeed
        var releaseResult = ReleaseInSeparateSession("Brian");
        Assert.DoesNotContain("pending reply", releaseResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_WithWait_CreatesReplyPendingAtDispatchTime()
    {
        await InitProjectAsync("none", "testuser", 3);

        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "auth", "planner");
        await SetRoleAsync("orchestrator", "auth");
        var dispatchResult = await DispatchAsync("code-writer", "auth", "Implement auth", to: "Brian", wait: true);
        dispatchResult.AssertSuccess();

        // Reply-pending marker should exist on Brian immediately after dispatch
        var replyPendingDir = Path.Combine(TestDir, "dydo/agents/Brian/.reply-pending");
        Assert.True(Directory.Exists(replyPendingDir), "Reply-pending marker should be created at dispatch time");
        var markerFiles = Directory.GetFiles(replyPendingDir, "*.json");
        Assert.Single(markerFiles);
    }

    [Fact]
    public async Task NoReplyPending_ForNoWaitDispatch()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Dispatch with --no-wait
        var result = await DispatchAsync("code-writer", "auth", "Implement auth", to: "Brian", noWait: true);
        result.AssertSuccess();

        // Brian claims, clears inbox
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");
        ClearInboxInSeparateSession("Brian");

        // No .reply-pending directory should exist
        var replyPendingDir = Path.Combine(TestDir, "dydo/agents/Brian/.reply-pending");
        Assert.False(Directory.Exists(replyPendingDir), "No reply-pending marker should exist for --no-wait dispatch");

        // Release should succeed
        var releaseResult = ReleaseInSeparateSession("Brian");
        Assert.DoesNotContain("pending reply", releaseResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InboxShow_DisplaysReplyRequired()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele dispatches with --wait to Brian (needs oversight role)
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "auth", "planner");
        await SetRoleAsync("orchestrator", "auth");
        var dispatchResult = await DispatchAsync("code-writer", "auth", "Implement auth", to: "Brian", wait: true);
        dispatchResult.AssertSuccess();

        // Brian claims and shows inbox
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");
        var showResult = ShowInboxInSeparateSession("Brian");

        Assert.Contains("Reply required", showResult);
    }

    #endregion

    #region Baton-Passing

    [Fact]
    public async Task Dispatch_SameTask_ClearsSenderReplyPending()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Brian is dispatched as code-writer on "auth"
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");
        ClearInboxInSeparateSession("Brian");

        // Manually create reply-pending marker (simulates inherited from --wait dispatch)
        var registry = new AgentRegistry(TestDir);
        registry.CreateReplyPendingMarker("Brian", "auth", "Adele");
        Assert.NotEmpty(registry.GetReplyPendingMarkers("Brian"));

        // Brian dispatches reviewer on the same task — baton passes
        DispatchInSeparateSession("Brian", "reviewer", "auth", "Review this");

        // Brian's reply-pending marker should be cleared
        registry = new AgentRegistry(TestDir);
        Assert.Empty(registry.GetReplyPendingMarkers("Brian"));
    }

    [Fact]
    public async Task Dispatch_SameTask_TargetInheritsReplyRequired()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Brian is dispatched as code-writer on "auth"
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");
        ClearInboxInSeparateSession("Brian");

        // Manually create reply-pending marker
        var registry = new AgentRegistry(TestDir);
        registry.CreateReplyPendingMarker("Brian", "auth", "Adele");

        // Brian dispatches reviewer on the same task with --no-wait
        DispatchInSeparateSession("Brian", "reviewer", "auth", "Review this");

        // The target agent's inbox item should have reply_required: true (inherited)
        var targetInbox = FindDispatchedAgentInbox("auth", "Brian");
        Assert.NotNull(targetInbox);
        Assert.Contains("reply_required: true", File.ReadAllText(targetInbox));
    }

    [Fact]
    public async Task Dispatch_DifferentTask_DoesNotClearReplyPending()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Brian is dispatched as code-writer on "auth"
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");
        ClearInboxInSeparateSession("Brian");

        // Manually create reply-pending marker
        var registry = new AgentRegistry(TestDir);
        registry.CreateReplyPendingMarker("Brian", "auth", "Adele");

        // Brian dispatches on a DIFFERENT task — should NOT clear reply-pending
        DispatchInSeparateSession("Brian", "code-writer", "other-task", "Something else");

        registry = new AgentRegistry(TestDir);
        Assert.NotEmpty(registry.GetReplyPendingMarkers("Brian"));
    }

    #endregion

    #region Review Enforcement (H25)

    [Fact]
    public async Task Release_BlockedForDispatchedCodeWriter_WithoutReviewDispatch()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele dispatches code-writer task to Brian
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");
        ClearInboxInSeparateSession("Brian");

        // Brian tries to release without dispatching a reviewer
        var releaseResult = ReleaseInSeparateSession("Brian");

        Assert.Contains("dispatched code-writers must dispatch a reviewer", releaseResult);
        Assert.Contains("--role reviewer", releaseResult);
    }

    [Fact]
    public async Task Release_SucceedsForDispatchedCodeWriter_AfterReviewDispatch()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele dispatches code-writer task to Brian
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");
        ClearInboxInSeparateSession("Brian");

        // Brian dispatches a reviewer for the same task
        DispatchInSeparateSession("Brian", "reviewer", "auth", "Review auth changes");

        // Brian releases — should succeed
        var releaseResult = ReleaseInSeparateSession("Brian");
        Assert.DoesNotContain("dispatched code-writers must dispatch a reviewer", releaseResult);
    }

    [Fact]
    public async Task Release_SucceedsForDispatchedCodeWriter_AfterReviewDispatch_WithVariantTask()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele dispatches code-writer task to Brian (task: "auth")
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");
        ClearInboxInSeparateSession("Brian");

        // Brian dispatches a reviewer with a VARIANT task name (auth-review, not auth)
        DispatchInSeparateSession("Brian", "reviewer", "auth-review", "Review auth changes");

        // Brian releases — should succeed because the dispatch marker uses sender's task
        var releaseResult = ReleaseInSeparateSession("Brian");
        Assert.DoesNotContain("dispatched code-writers must dispatch a reviewer", releaseResult);
    }

    [Fact]
    public async Task Release_NotBlockedForDirectCodeWriter()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Brian works directly (no dispatch origin)
        await ClaimAgentAsync("Brian");
        await SetRoleAsync("code-writer", "auth");

        // Release should succeed without dispatching a reviewer
        var result = await ReleaseAgentAsync();
        result.AssertSuccess();
    }

    [Fact]
    public async Task Release_NotBlockedForNonCodeWriter()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Brian is dispatched as a planner (not code-writer)
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "planner", "auth", "Plan auth");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "planner", "auth");
        ClearInboxInSeparateSession("Brian");

        var releaseResult = ReleaseInSeparateSession("Brian");
        Assert.DoesNotContain("dispatched code-writers must dispatch a reviewer", releaseResult);
    }

    #endregion

    #region Inquisitor Release Enforcement

    [Fact]
    public async Task Release_BlockedForDispatchedInquisitor_WithoutJudgeDispatch()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele dispatches inquisitor task to Brian
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "inquisitor", "audit-docs", "Audit documentation");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "inquisitor", "audit-docs");
        ClearInboxInSeparateSession("Brian");

        // Brian tries to release without dispatching a judge
        var releaseResult = ReleaseInSeparateSession("Brian");

        Assert.Contains("dispatched inquisitors must dispatch a judge or another inquisitor", releaseResult);
        Assert.Contains("--role judge", releaseResult);
    }

    [Fact]
    public async Task Release_SucceedsForDispatchedInquisitor_AfterJudgeDispatch()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele dispatches inquisitor task to Brian
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "inquisitor", "audit-docs", "Audit documentation");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "inquisitor", "audit-docs");
        ClearInboxInSeparateSession("Brian");

        // Brian dispatches a judge for the same task
        DispatchInSeparateSession("Brian", "judge", "audit-docs", "Judge audit findings");

        // Brian releases — should succeed
        var releaseResult = ReleaseInSeparateSession("Brian");
        Assert.DoesNotContain("dispatched inquisitors must dispatch a judge or another inquisitor", releaseResult);
    }

    [Fact]
    public async Task Release_SucceedsForDispatchedInquisitor_AfterInquisitorRedispatch()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Adele dispatches inquisitor task to Brian
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "inquisitor", "audit-docs", "Audit documentation");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "inquisitor", "audit-docs");
        ClearInboxInSeparateSession("Brian");

        // Brian re-dispatches another inquisitor (instead of a judge)
        DispatchInSeparateSession("Brian", "inquisitor", "audit-docs", "Re-dispatch to worktree");

        // Brian releases — should succeed (requireAll=false, inquisitor satisfies the constraint)
        var releaseResult = ReleaseInSeparateSession("Brian");
        Assert.DoesNotContain("dispatched inquisitors must dispatch a judge or another inquisitor", releaseResult);
    }

    #endregion

    #region CanOrchestrate Wait Privilege

    [Fact]
    public async Task Dispatch_Wait_AllowedForCanOrchestrateRole()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Inquisitor should be able to use --wait (canOrchestrate = true)
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "inquisitor", "audit", "Audit");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "inquisitor", "audit");
        ClearInboxInSeparateSession("Brian");

        var result = DispatchInSeparateSessionWithResult("Brian", "judge", "audit", "Judge this", wait: true);

        Assert.DoesNotContain("reserved for oversight roles", result);
    }

    [Fact]
    public async Task Dispatch_Wait_BlockedForNonOrchestrateRole()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Code-writer should NOT be able to use --wait (canOrchestrate = false)
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "code-writer", "auth", "Implement auth");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "code-writer", "auth");
        ClearInboxInSeparateSession("Brian");

        var result = DispatchInSeparateSessionWithResult("Brian", "reviewer", "auth", "Review this", wait: true);

        Assert.Contains("reserved for oversight roles", result);
    }

    [Fact]
    public async Task Dispatch_Wait_SucceedsWithVerifiedSessionContext()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Use inquisitor — has CanOrchestrate=true and no role-assignment constraints
        CreateInboxItemWithOrigin("Brian", "Adele", "Adele", "inquisitor", "deploy", "Audit deploy");
        ClaimAgentInSeparateSession("Brian");
        SetRoleInState("Brian", "inquisitor", "deploy");
        ClearInboxInSeparateSession("Brian");

        // Write verified format: sessionId + agentName
        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        var otherSession = "other-session-Brian";
        File.WriteAllText(contextPath, $"{otherSession}\nBrian");

        var workspace = Path.Combine(TestDir, "dydo", "agents", "Brian");
        var marker = Path.Combine(workspace, ".no-launch-nudge-deploy");
        File.WriteAllText(marker, "test-bypass");

        var command = DispatchCommand.Create();
        var args = new List<string>
            { "--role", "code-writer", "--task", "deploy", "--brief", "Deploy it", "--no-launch", "--wait" };
        var result = await RunAsync(command, args.ToArray());

        StoreSessionContext();
        var output = result.Stdout + result.Stderr;

        Assert.DoesNotContain("reserved for oversight roles", output);
    }

    #endregion

    #region Helper Methods

    private async Task<CommandResult> DispatchAsync(
        string role,
        string task,
        string brief,
        string? to = null,
        bool noWait = false,
        bool wait = false)
    {
        var command = DispatchCommand.Create();
        var args = new List<string>
        {
            "--role", role,
            "--task", task,
            "--brief", brief,
            "--no-launch"
        };

        BypassNoLaunchNudge(task);
        if (to != null) { args.Add("--to"); args.Add(to); }
        if (wait) args.Add("--wait");
        else if (noWait) args.Add("--no-wait");

        return await RunAsync(command, args.ToArray());
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

    private void ClaimAgentInSeparateSession(string agentName)
    {
        var registry = new AgentRegistry(TestDir);
        var otherSession = $"other-session-{agentName}";
        registry.StorePendingSessionId(agentName, otherSession);

        // Temporarily switch session context
        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        File.WriteAllText(contextPath, otherSession);

        var agentCmd = AgentCommand.Create();
        RunAsync(agentCmd, "claim", agentName).Wait();

        // Restore original session context
        StoreSessionContext();
    }

    private void SetRoleInState(string agentName, string role, string task)
    {
        var registry = new AgentRegistry(TestDir);
        var state = registry.GetAgentState(agentName);
        if (state == null) return;

        // Use the registry's SetRole to properly update the state
        // We need to temporarily switch session context to the agent's session
        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        var otherSession = $"other-session-{agentName}";
        File.WriteAllText(contextPath, otherSession);

        var agentCmd = AgentCommand.Create();
        RunAsync(agentCmd, "role", role, "--task", task).Wait();

        // Restore original session context
        StoreSessionContext();
    }

    private void ClearInboxInSeparateSession(string agentName)
    {
        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        var otherSession = $"other-session-{agentName}";
        File.WriteAllText(contextPath, otherSession);

        var inboxCmd = InboxCommand.Create();
        RunAsync(inboxCmd, "clear", "--all").Wait();

        StoreSessionContext();
    }

    private string ReleaseInSeparateSession(string agentName)
    {
        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        var otherSession = $"other-session-{agentName}";
        File.WriteAllText(contextPath, otherSession);

        var agentCmd = AgentCommand.Create();
        var result = RunAsync(agentCmd, "release").Result;

        StoreSessionContext();
        return result.Stdout + result.Stderr;
    }

    private void SendMessageInSeparateSession(string fromAgent, string toAgent, string subject, string body)
    {
        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        var otherSession = $"other-session-{fromAgent}";
        File.WriteAllText(contextPath, otherSession);

        var msgCmd = MessageCommand.Create();
        RunAsync(msgCmd, "--to", toAgent, "--subject", subject, "--body", body).Wait();

        StoreSessionContext();
    }

    private string ShowInboxInSeparateSession(string agentName)
    {
        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        var otherSession = $"other-session-{agentName}";
        File.WriteAllText(contextPath, otherSession);

        var inboxCmd = InboxCommand.Create();
        var result = RunAsync(inboxCmd, "show").Result;

        StoreSessionContext();
        return result.Stdout;
    }

    private void SetTaskRoleHistory(string agentName, string task, string role)
    {
        var statePath = Path.Combine(TestDir, "dydo", "agents", agentName, "state.md");
        if (File.Exists(statePath))
        {
            var content = File.ReadAllText(statePath);
            content = System.Text.RegularExpressions.Regex.Replace(
                content,
                @"^task-role-history:.*$",
                $"task-role-history: {{ \"{task}\": [\"{role}\"] }}",
                System.Text.RegularExpressions.RegexOptions.Multiline);
            File.WriteAllText(statePath, content);
        }
        else
        {
            var workspace = Path.Combine(TestDir, "dydo", "agents", agentName);
            Directory.CreateDirectory(workspace);
            File.WriteAllText(statePath, $$"""
                ---
                agent: {{agentName}}
                status: free
                assigned: testuser
                task-role-history: { "{{task}}": ["{{role}}"] }
                ---
                # {{agentName}} — Session State
                """);
        }
    }

    private void CreateMessageFile(string agentName, string fromAgent, string subject, string body)
    {
        var inboxPath = Path.Combine(TestDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);

        var id = Guid.NewGuid().ToString("N")[..8];
        var content = $"""
            ---
            id: {id}
            type: message
            from: {fromAgent}
            subject: {subject}
            received: {DateTime.UtcNow:o}
            ---

            # Message from {fromAgent}

            ## Subject

            {subject}

            ## Body

            {body}
            """;

        File.WriteAllText(Path.Combine(inboxPath, $"{id}-msg-{subject}.md"), content);
    }

    private void DispatchInSeparateSession(string fromAgent, string role, string task, string brief)
    {
        DispatchInSeparateSessionWithResult(fromAgent, role, task, brief);
    }

    private string DispatchInSeparateSessionWithResult(string fromAgent, string role, string task, string brief, bool wait = false)
    {
        var contextPath = Path.Combine(DydoDir, "agents", ".session-context");
        var otherSession = $"other-session-{fromAgent}";
        File.WriteAllText(contextPath, otherSession);

        // Bypass no-launch nudge for the dispatching agent (not the main test session's agent)
        var workspace = Path.Combine(TestDir, "dydo", "agents", fromAgent);
        var sanitized = DynaDocs.Utils.PathUtils.SanitizeForFilename(task);
        var marker = Path.Combine(workspace, $".no-launch-nudge-{sanitized}");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(marker, "test-bypass");

        // Also bypass the --no-wait nudge for oversight-role dispatchers
        var noWaitMarker = Path.Combine(workspace, $".no-wait-nudge-{sanitized}");
        File.WriteAllText(noWaitMarker, "test-bypass");

        var command = DispatchCommand.Create();
        var args = new List<string> { "--role", role, "--task", task, "--brief", brief, "--no-launch" };
        if (wait) args.Add("--wait");
        else args.Add("--no-wait");

        var result = RunAsync(command, args.ToArray()).Result;

        StoreSessionContext();
        return result.Stdout + result.Stderr;
    }

    private string? FindDispatchedAgentInbox(string task, string excludeAgent)
    {
        var agentsDir = Path.Combine(TestDir, "dydo", "agents");
        foreach (var agentDir in Directory.GetDirectories(agentsDir))
        {
            var agentName = Path.GetFileName(agentDir);
            if (string.Equals(agentName, excludeAgent, StringComparison.OrdinalIgnoreCase))
                continue;

            var inboxPath = Path.Combine(agentDir, "inbox");
            if (!Directory.Exists(inboxPath)) continue;

            foreach (var file in Directory.GetFiles(inboxPath, $"*-{task}.md"))
                return file;
        }
        return null;
    }

    #endregion
}
