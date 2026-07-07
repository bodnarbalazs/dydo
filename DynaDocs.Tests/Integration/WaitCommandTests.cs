namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("Integration")]
public class WaitCommandTests : IntegrationTestBase
{
    #region --cancel Tests

    [Fact]
    public async Task Wait_Cancel_SpecificTask_RemovesMarker()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command, "--task", "auth", "--cancel");

        result.AssertSuccess();
        result.AssertStdoutContains("Wait cancelled for task 'auth'");

        // Verify marker was removed
        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Empty(markers);
    }

    [Fact]
    public async Task Wait_Cancel_AllTasks_RemovesAllMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.CreateWaitMarker("Adele", "login", "Charlie");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command, "--cancel");

        result.AssertSuccess();
        result.AssertStdoutContains("All wait markers cleared");

        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Empty(markers);
    }

    [Fact]
    public async Task Wait_Cancel_NonexistentMarker_Succeeds()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command, "--task", "nonexistent", "--cancel");

        result.AssertSuccess();
    }

    #endregion

    #region Channel Isolation Tests

    [Fact]
    public async Task Wait_Cancel_PreservesOtherMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.CreateWaitMarker("Adele", "login", "Charlie");

        // Cancel only "auth"
        StoreSessionContext();
        var command = WaitCommand.Create();
        await RunAsync(command, "--task", "auth", "--cancel");

        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.Equal("login", markers[0].Task);
    }

    #endregion

    #region Wait Marker Infrastructure

    [Fact]
    public async Task WaitMarker_CreateAndRead_RoundTrips()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth-login", "Brian");

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.Equal("Brian", markers[0].Target);
        Assert.Equal("auth-login", markers[0].Task);
    }

    [Fact]
    public async Task WaitMarker_Remove_DeletesSpecificMarker()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.CreateWaitMarker("Adele", "login", "Charlie");

        var removed = registry.RemoveWaitMarker("Adele", "auth");
        Assert.True(removed);

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.Equal("login", markers[0].Task);
    }

    [Fact]
    public async Task WaitMarker_Remove_NonExistent_ReturnsFalse()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        var removed = registry.RemoveWaitMarker("Adele", "nonexistent");
        Assert.False(removed);
    }

    [Fact]
    public async Task WaitMarker_ClearAll_RemovesDirectory()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.CreateWaitMarker("Adele", "login", "Charlie");

        registry.ClearAllWaitMarkers("Adele");

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Empty(markers);
    }

    [Fact]
    public async Task WaitMarker_Overwrite_IsIdempotent()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.CreateWaitMarker("Adele", "auth", "Charlie");

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.Equal("Charlie", markers[0].Target);
    }

    #endregion

    #region No Agent Tests

    [Fact]
    public async Task Wait_WithoutClaimedAgent_ReturnsError()
    {
        await InitProjectAsync("none", "testuser", 3);
        // Do NOT claim an agent — session context exists but no agent claimed
        StoreSessionContext();

        var command = WaitCommand.Create();
        var result = await RunAsync(command);

        result.AssertExitCode(2);
        result.AssertStderrContains("No agent identity");
    }

    #endregion

    #region Wait With Message Tests

    [Fact]
    public async Task Wait_General_FindsArrivingMessage()
    {
        // Under #0141 semantics, the general wait snapshots from the inbox dir at
        // start and only fires on messages that arrive AFTER. Drop the message
        // after the snapshot via IsProcessRunningOverride and assert the wait pops.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var dropped = false;
        var originalPollMs = WaitCommand.PollIntervalMs;
        WaitCommand.PollIntervalMs = 25;

        ProcessUtils.IsProcessRunningOverride = _ =>
        {
            if (!dropped)
            {
                dropped = true;
                CreateMessageFile("Adele", "Brian", "test-subject", "Hello from Brian");
            }
            return true;
        };
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertSuccess();
            result.AssertStdoutContains("Message received from Brian");
            result.AssertStdoutContains("test-subject");
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
            WaitCommand.PollIntervalMs = originalPollMs;
        }
    }

    [Fact]
    public async Task Wait_ForTask_FindsExistingMessage()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Pre-populate inbox with a message matching the task filter
        CreateMessageFile("Adele", "Charlie", "my-task", "Task results ready");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command, "--task", "my-task");

        result.AssertSuccess();
        result.AssertStdoutContains("Message received from Charlie");
    }

    [Fact]
    public void MessageFinder_FindMessage_NonexistentPath_ReturnsNull()
    {
        var result = MessageFinder.FindMessage("/nonexistent/inbox", null);
        Assert.Null(result);
    }

    [Fact]
    public void MessageFinder_FindMessage_OrdersByReceivedTimestamp_NotCreationTime()
    {
        var inboxPath = Path.Combine(Path.GetTempPath(), "dydo-test-inbox-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(inboxPath);
        try
        {
            // Write the NEWER message first (earlier creation time on disk)
            var now = DateTime.UtcNow;
            WriteMessageFileDirect(inboxPath, "aaa", "Brian", "task-a", "Newer message", now);
            Thread.Sleep(50);
            // Write the OLDER message second (later creation time on disk)
            WriteMessageFileDirect(inboxPath, "bbb", "Charlie", "task-b", "Older message", now.AddMinutes(-10));

            // Should return the message with the earliest received timestamp, not earliest creation time
            var result = MessageFinder.FindMessage(inboxPath, null);
            Assert.NotNull(result);
            Assert.Equal("Charlie", result.From);
        }
        finally
        {
            Directory.Delete(inboxPath, true);
        }
    }

    [Fact]
    public void MessageFinder_FindMessage_FallsBackToCreationTime_WhenNoReceivedField()
    {
        var inboxPath = Path.Combine(Path.GetTempPath(), "dydo-test-inbox-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(inboxPath);
        try
        {
            // Write messages without received field — should fall back to creation time order
            var content1 = """
                ---
                id: aaa
                type: message
                from: Brian
                subject: task-a
                ---

                ## Body

                First
                """;
            File.WriteAllText(Path.Combine(inboxPath, "aaa-msg-task-a.md"), content1);
            Thread.Sleep(50);
            var content2 = """
                ---
                id: bbb
                type: message
                from: Charlie
                subject: task-b
                ---

                ## Body

                Second
                """;
            File.WriteAllText(Path.Combine(inboxPath, "bbb-msg-task-b.md"), content2);

            var result = MessageFinder.FindMessage(inboxPath, null);
            Assert.NotNull(result);
            Assert.Equal("Brian", result.From); // Earlier creation time
        }
        finally
        {
            Directory.Delete(inboxPath, true);
        }
    }

    #endregion

    #region Parent Liveness Tests

    [Fact]
    public async Task WaitForTask_ExitsWhenParentDies()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "my-task", "Brian");

        ProcessUtils.IsProcessRunningOverride = _ => false;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command, "--task", "my-task");

            result.AssertExitCode(2);

            // Marker should be reset (listening=false, pid=null)
            registry = new AgentRegistry(TestDir);
            var markers = registry.GetWaitMarkers("Adele");
            Assert.Single(markers);
            Assert.False(markers[0].Listening);
            Assert.Null(markers[0].Pid);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
        }
    }

    [Fact]
    public async Task WaitGeneral_ExitsWhenParentDies()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        ProcessUtils.IsProcessRunningOverride = _ => false;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertExitCode(2);

            // General wait marker should be cleaned up
            var registry = new AgentRegistry(TestDir);
            var markers = registry.GetWaitMarkers("Adele");
            Assert.Empty(markers);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
        }
    }

    [Fact]
    public async Task WaitForTask_MessageFound_StillWorks()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "my-task", "Brian");

        CreateMessageFile("Adele", "Charlie", "my-task", "Done");

        // Parent alive — should find message and succeed
        ProcessUtils.IsProcessRunningOverride = _ => true;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command, "--task", "my-task");

            result.AssertSuccess();
            result.AssertStdoutContains("Message received from Charlie");

            // Marker should be fully removed (message found)
            registry = new AgentRegistry(TestDir);
            var markers = registry.GetWaitMarkers("Adele");
            Assert.Empty(markers);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
        }
    }

    [Fact]
    public async Task WaitGeneral_RecordsPidInMarker()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Drop a fresh message after the snapshot so the loop exits via Success
        // (post-#0141 semantics: only post-snapshot arrivals fire the wait).
        var dropped = false;
        var originalPollMs = WaitCommand.PollIntervalMs;
        WaitCommand.PollIntervalMs = 25;

        ProcessUtils.IsProcessRunningOverride = _ =>
        {
            if (!dropped)
            {
                dropped = true;
                CreateMessageFile("Adele", "Brian", "general-subject", "Hello");
            }
            return true;
        };
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertSuccess();

            // General marker should be cleaned up after exit
            var registry = new AgentRegistry(TestDir);
            var markers = registry.GetWaitMarkers("Adele");
            Assert.Empty(markers);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
            WaitCommand.PollIntervalMs = originalPollMs;
        }
    }

    [Fact]
    public async Task WaitForTask_ExitsWhenClaimedHostPidDies()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Stamp .session.ClaimedPid = 9999 at claim time (ResolveClaimedPid uses the ancestor
        // walk). The wait keys host-liveness off this persisted PID, not a fresh walk.
        ProcessUtils.FindAncestorProcessOverride = (_, _) => 9999;
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "my-task", "Brian");

        // Parent alive, but the claim-validated host PID is dead.
        ProcessUtils.IsProcessRunningOverride = pid => pid != 9999;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command, "--task", "my-task");

            result.AssertExitCode(2);

            registry = new AgentRegistry(TestDir);
            var markers = registry.GetWaitMarkers("Adele");
            Assert.Single(markers);
            Assert.False(markers[0].Listening);
            Assert.Null(markers[0].Pid);
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
            ProcessUtils.IsProcessRunningOverride = null;
        }
    }

    [Fact]
    public async Task WaitGeneral_ExitsWhenClaimedHostPidDies()
    {
        await InitProjectAsync("none", "testuser", 3);

        ProcessUtils.FindAncestorProcessOverride = (_, _) => 9999;
        await ClaimAgentAsync("Adele");

        // Parent alive, but the claim-validated host PID is dead.
        ProcessUtils.IsProcessRunningOverride = pid => pid != 9999;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertExitCode(2);

            var registry = new AgentRegistry(TestDir);
            var markers = registry.GetWaitMarkers("Adele");
            Assert.Empty(markers);
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
            ProcessUtils.IsProcessRunningOverride = null;
        }
    }

    [Fact]
    public async Task ResolveHostLivenessPid_PrefersClaimedPid_OverFreshAncestryWalk()
    {
        // Regression for the 49b2981 wait flake (#224): the wait must anchor host-liveness to
        // the claim-validated session.ClaimedPid, NOT re-walk the ancestry at wait time. From a
        // backgrounded `dydo wait` the fresh walk can bind to a transient host ancestor and drop
        // the wait with a spurious exit-1 while the tab is alive.
        await InitProjectAsync("none", "testuser", 3);

        ProcessUtils.FindAncestorProcessOverride = (_, _) => 4242;  // stamps ClaimedPid at claim
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        Assert.Equal(4242, registry.GetSession("Adele")?.ClaimedPid);

        // A fresh walk would now resolve a DIFFERENT pid — resolution must ignore it.
        ProcessUtils.FindAncestorProcessOverride = (_, _) => 9999;
        try
        {
            Assert.Equal(4242, WaitCommand.ResolveHostLivenessPid(registry, "Adele"));
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
        }
    }

    [Fact]
    public async Task ResolveHostLivenessPid_FallsBackToAncestryWalk_WhenClaimedPidNull()
    {
        // Legacy sessions predate ClaimedPid — fall back to the ancestry walk so they keep working.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var sessionPath = Path.Combine(TestDir, "dydo", "agents", "Adele", ".session");
        File.WriteAllText(sessionPath,
            $$"""{"Agent":"Adele","SessionId":"sess-legacy","Claimed":"{{DateTime.UtcNow:o}}"}""");

        var registry = new AgentRegistry(TestDir);
        Assert.Null(registry.GetSession("Adele")?.ClaimedPid);

        ProcessUtils.FindAncestorProcessOverride = (_, _) => 7777;
        try
        {
            Assert.Equal(7777, WaitCommand.ResolveHostLivenessPid(registry, "Adele"));
        }
        finally
        {
            ProcessUtils.FindAncestorProcessOverride = null;
        }
    }

    #endregion

    #region Task-Priority Routing Tests

    [Fact]
    public void MessageFinder_FindMessage_ExcludesSubjectsInExcludeSet()
    {
        var inboxPath = Path.Combine(Path.GetTempPath(), "dydo-test-inbox-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(inboxPath);
        try
        {
            WriteMessageFileDirect(inboxPath, "aaa", "Brian", "claimed-subject", "Should be skipped", DateTime.UtcNow);

            var excludeSet = new HashSet<string>(["claimed-subject"], StringComparer.OrdinalIgnoreCase);
            var result = MessageFinder.FindMessage(inboxPath, null, excludeSet);

            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(inboxPath, true);
        }
    }

    [Fact]
    public async Task WaitGeneral_SkipsMessage_WhenTaskWaitExistsForSubject()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Task wait registered for "X" (regardless of registration order — re-read each poll)
        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "X", "Brian");

        // Pre-place message with the same subject
        CreateMessageFile("Adele", "Brian", "X", "task-channel message");

        // Make the parent appear dead so general wait exits after one poll without finding the message
        ProcessUtils.IsProcessRunningOverride = _ => false;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            // General wait did not return the message (excluded) — exits via parent-death path
            result.AssertExitCode(2);
            Assert.DoesNotContain("Message received", result.Stdout);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
        }
    }

    [Fact]
    public async Task WaitGeneral_FindsMessage_WhenSubjectHasNoTaskWait()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Task wait registered for "X" — message arriving with subject "Y" is NOT
        // claimed by the task wait, so the general wait must pick it up. Drop the
        // message after snapshot (#0141 semantics).
        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "X", "Brian");

        var dropped = false;
        var originalPollMs = WaitCommand.PollIntervalMs;
        WaitCommand.PollIntervalMs = 25;

        ProcessUtils.IsProcessRunningOverride = _ =>
        {
            if (!dropped)
            {
                dropped = true;
                CreateMessageFile("Adele", "Brian", "Y", "general message");
            }
            return true;
        };
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertSuccess();
            result.AssertStdoutContains("Message received from Brian");
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
            WaitCommand.PollIntervalMs = originalPollMs;
        }
    }

    [Fact]
    public async Task WaitGeneral_TaskWaitWins_WhenBothActive()
    {
        // Even when both waits are eligible, the per-poll exclusion re-read ensures the
        // task wait gets the message — a directly-filtered FindMessage returns it; the
        // general wait, with the live exclusion set, does not.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "X", "Brian");
        CreateMessageFile("Adele", "Brian", "X", "task-channel message");

        var inboxPath = Path.Combine(TestDir, "dydo", "agents", "Adele", "inbox");
        var claimed = WaitCommand.GetActiveTaskWaitSubjects(registry, "Adele");

        var generalView = MessageFinder.FindMessage(inboxPath, null, claimed);
        var taskView = MessageFinder.FindMessage(inboxPath, "X");

        Assert.Null(generalView);
        Assert.NotNull(taskView);
        Assert.Equal("X", taskView!.Subject);
    }

    [Fact]
    public async Task GetActiveTaskWaitSubjects_ReturnsFreshState_AcrossCalls()
    {
        // The general wait must re-read claimed subjects each poll cycle so task waits
        // registered after the general wait started are still excluded — closes the
        // dispatch-time race on the original startup-snapshot design.
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        Assert.Empty(WaitCommand.GetActiveTaskWaitSubjects(registry, "Adele"));

        registry.CreateWaitMarker("Adele", "first", "Brian");
        var afterFirst = WaitCommand.GetActiveTaskWaitSubjects(registry, "Adele");
        Assert.Single(afterFirst);
        Assert.Contains("first", afterFirst);

        registry.CreateWaitMarker("Adele", "second", "Charlie");
        var afterSecond = WaitCommand.GetActiveTaskWaitSubjects(registry, "Adele");
        Assert.Equal(2, afterSecond.Count);

        registry.CreateWaitMarker("Adele", "_general-wait", "Adele");
        var withSentinel = WaitCommand.GetActiveTaskWaitSubjects(registry, "Adele");
        Assert.Equal(2, withSentinel.Count);
        Assert.DoesNotContain("_general-wait", withSentinel);

        registry.RemoveWaitMarker("Adele", "first");
        var afterRemove = WaitCommand.GetActiveTaskWaitSubjects(registry, "Adele");
        Assert.Single(afterRemove);
        Assert.Contains("second", afterRemove);
    }

    #endregion

    #region Deadlock Recovery Tests

    [Fact]
    public void MessageFinder_FindMessage_ExcludesIdsInExcludeSet()
    {
        var inboxPath = Path.Combine(Path.GetTempPath(), "dydo-test-inbox-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(inboxPath);
        try
        {
            WriteMessageFileDirect(inboxPath, "deadbeef", "Brian", "subj", "stale", DateTime.UtcNow);

            var excludeIds = new HashSet<string>(["deadbeef"], StringComparer.OrdinalIgnoreCase);
            var result = MessageFinder.FindMessage(inboxPath, null, excludeSubjects: null, excludeIds: excludeIds);

            Assert.Null(result);
        }
        finally { Directory.Delete(inboxPath, true); }
    }

    #endregion

    #region Unified Delivered-State Tests (#0147)

    [Fact]
    public async Task WaitGeneral_DoesNotFire_WhenInboxFileExistsButUnreadMessagesEmpty()
    {
        // #0141 stays fixed under #0147 semantics: a stale file on disk (post-Read,
        // pre-`inbox clear`) must not fire the wait. The unified definition keeps the
        // property because the id is not in UnreadMessages, so the inclusion-set excludes it.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        CreateMessageFile("Adele", "Brian", "stale", "post-Read leftover");
        new AgentRegistry(TestDir).ClearAllUnreadMessages("Adele");

        ProcessUtils.IsProcessRunningOverride = _ => false;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertExitCode(2);
            Assert.DoesNotContain("Message received", result.Stdout);
        }
        finally { ProcessUtils.IsProcessRunningOverride = null; }
    }

    [Fact]
    public async Task WaitGeneral_FiresOnArrivalDuringActiveWait_NotJustPostRegistration()
    {
        // Forward functionality: a wait already running fires when a NEW message lands
        // via the production path (DeliverInboxMessage writes the file and updates
        // UnreadMessages). Exercises the full canonical signal end-to-end.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var dropped = false;
        var originalPollMs = WaitCommand.PollIntervalMs;
        WaitCommand.PollIntervalMs = 25;

        ProcessUtils.IsProcessRunningOverride = _ =>
        {
            if (!dropped)
            {
                dropped = true;
                MessageService.DeliverInboxMessage(
                    new AgentRegistry(TestDir), "Brian", "Adele", "hi", "fresh");
            }
            return true;
        };
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertSuccess();
            result.AssertStdoutContains("Message received from Brian");
            result.AssertStdoutContains("fresh");
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
            WaitCommand.PollIntervalMs = originalPollMs;
        }
    }

    [Fact]
    public async Task DeliverInboxMessage_AddsToUnreadMessages_EvenWhenTargetReleased()
    {
        // The unified definition holds across agent status: a message sent to a Released
        // target still enters the canonical "not yet delivered" set so a future wait fires
        // on it. Removing the prior Working-only conditional is what closes this gap.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        // Brian was never claimed — has no Working state. The message must still register.
        var id = MessageService.DeliverInboxMessage(registry, "Adele", "Brian", "hello", "subj");

        var unread = registry.GetAgentState("Brian")?.UnreadMessages ?? new();
        Assert.Contains(id, unread);
    }

    [Fact]
    public void FindMessage_IncludeIds_OnlyMatchesListedIds()
    {
        var inboxPath = Path.Combine(Path.GetTempPath(), "dydo-test-inbox-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(inboxPath);
        try
        {
            WriteMessageFileDirect(inboxPath, "abc123", "Brian", "subj-a", "body", DateTime.UtcNow);
            WriteMessageFileDirect(inboxPath, "deadbeef", "Charlie", "subj-b", "body", DateTime.UtcNow);

            var only = new HashSet<string>(["abc123"], StringComparer.OrdinalIgnoreCase);
            var result = MessageFinder.FindMessage(inboxPath, null,
                excludeSubjects: null, excludeIds: null, includeIds: only);

            Assert.NotNull(result);
            Assert.Equal("Brian", result.From);
        }
        finally { Directory.Delete(inboxPath, true); }
    }

    [Fact]
    public void FindMessage_IncludeIds_EmptySet_ReturnsNull()
    {
        var inboxPath = Path.Combine(Path.GetTempPath(), "dydo-test-inbox-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(inboxPath);
        try
        {
            WriteMessageFileDirect(inboxPath, "abc123", "Brian", "subj", "body", DateTime.UtcNow);
            var none = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = MessageFinder.FindMessage(inboxPath, null, includeIds: none);
            Assert.Null(result);
        }
        finally { Directory.Delete(inboxPath, true); }
    }

    #endregion

    #region Snapshot-At-Registration Tests (#0149)

    [Fact]
    public async Task WaitGeneral_DoesNotFireOnPreStackedUnreads_PreventsRearmFloodDeadlock()
    {
        // #0149 regression: when 3 unreads are stacked before wait registers, the
        // wait must NOT fire on them. Pre-fix, every re-arm fired immediately on
        // the next stacked unread; combined with Decision 021's MissingGeneralWait
        // gate on Read, the agent could not drain. Post-fix, wait blocks; the agent
        // drains via `dydo inbox show` + Read while this wait keeps the guard happy.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        CreateMessageFile("Adele", "Brian", "first", "msg1");
        CreateMessageFile("Adele", "Charlie", "second", "msg2");
        CreateMessageFile("Adele", "Dana", "third", "msg3");
        // CreateMessageFile already calls AddUnreadMessage — UnreadMessages = {msg1, msg2, msg3}.

        var originalPollMs = WaitCommand.PollIntervalMs;
        WaitCommand.PollIntervalMs = 25;
        // Parent dies on first liveness probe so the wait exits via parent-death (exit 2),
        // not by firing on a stacked unread.
        ProcessUtils.IsProcessRunningOverride = _ => false;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertExitCode(2);
            Assert.DoesNotContain("Message received", result.Stdout);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
            WaitCommand.PollIntervalMs = originalPollMs;
        }
    }

    [Fact]
    public async Task WaitGeneral_FiresOnPostRegistrationArrival_EvenWithPreStackedUnreads()
    {
        // The wait still does its job for genuinely-new arrivals when pre-stacked
        // unreads exist. Verifies the snapshot is correctly scoped — it does not
        // drown out the live signal.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Pre-stack two unreads — must be excluded from this wait's signal.
        CreateMessageFile("Adele", "Brian", "stale-1", "old1");
        CreateMessageFile("Adele", "Brian", "stale-2", "old2");

        var dropped = false;
        var originalPollMs = WaitCommand.PollIntervalMs;
        WaitCommand.PollIntervalMs = 25;

        ProcessUtils.IsProcessRunningOverride = _ =>
        {
            if (!dropped)
            {
                dropped = true;
                // Production-path delivery — sets Received=now and AddUnreadMessage.
                // This id is NOT in the wait's snapshot (snapshot was taken before
                // this delivery), so the wait must fire on it.
                MessageService.DeliverInboxMessage(
                    new AgentRegistry(TestDir), "Charlie", "Adele", "post-reg", "fresh");
            }
            return true;
        };
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertSuccess();
            result.AssertStdoutContains("Message received from Charlie");
            result.AssertStdoutContains("fresh");
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
            WaitCommand.PollIntervalMs = originalPollMs;
        }
    }

    [Fact]
    public async Task WaitGeneral_StaysBlockedAfterDrain_NoSpuriousFire()
    {
        // After the snapshot fix, draining the pre-stacked unread should leave the
        // wait blocking (no fire). Pre-fix, draining was impossible because Read
        // was gated on a live wait that kept dying. Post-fix, the wait's snapshot
        // excludes the pre-stacked id; wait blocks; agent reads the id; UnreadMessages
        // empties; wait remains blocking with nothing to do.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        CreateMessageFileReturningId("Adele", "Brian", "stacked", "old");
        var registry = new AgentRegistry(TestDir);

        var originalPollMs = WaitCommand.PollIntervalMs;
        WaitCommand.PollIntervalMs = 25;

        var drained = false;
        ProcessUtils.IsProcessRunningOverride = _ =>
        {
            if (!drained)
            {
                drained = true;
                // Simulate the agent draining via Read on the first liveness probe
                // (the first poll already returned null because the snapshot excluded
                // the only unread). Return true so this probe doesn't exit; the next
                // probe returns false and the wait exits via parent/claude-death.
                registry.ClearAllUnreadMessages("Adele");
                return true;
            }
            return false;
        };
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertExitCode(2);
            Assert.DoesNotContain("Message received", result.Stdout);
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
            WaitCommand.PollIntervalMs = originalPollMs;
        }
    }

    #endregion

    #region Snapshot-Divergence + Idempotency Tests (#0141)

    [Fact]
    public void MessageFinder_GetInboxMessageIds_ReturnsIdsForMessageFiles()
    {
        // Helper introduced for #0141: WaitGeneral snapshots from the inbox dir to
        // align with MessageFinder.FindMessage's source of truth. The helper extracts
        // the message-id prefix from each *-msg-*.md filename and skips anything else.
        var inboxPath = Path.Combine(Path.GetTempPath(), "dydo-test-inbox-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(inboxPath);
        try
        {
            WriteMessageFileDirect(inboxPath, "abc123", "Brian", "subj-a", "Body", DateTime.UtcNow);
            WriteMessageFileDirect(inboxPath, "deadbeef", "Charlie", "subj-b", "Body", DateTime.UtcNow);
            File.WriteAllText(Path.Combine(inboxPath, "not-a-message.md"), "ignored");
            File.WriteAllText(Path.Combine(inboxPath, "readme.txt"), "ignored");

            var ids = MessageFinder.GetInboxMessageIds(inboxPath);

            Assert.Equal(2, ids.Count);
            Assert.Contains("abc123", ids);
            Assert.Contains("deadbeef", ids);
        }
        finally { Directory.Delete(inboxPath, true); }
    }

    [Fact]
    public void MessageFinder_GetInboxMessageIds_NonexistentPath_ReturnsEmpty()
    {
        var ids = MessageFinder.GetInboxMessageIds(Path.Combine(Path.GetTempPath(), "dydo-no-such-inbox-" + Guid.NewGuid().ToString("N")[..8]));
        Assert.Empty(ids);
    }

    [Fact]
    public async Task WaitGeneral_StaysAliveWhenInboxFilesExistButStateMdEmpty()
    {
        // Bug #0141 stays fixed under #0147 semantics: a file on disk whose id is NOT in
        // UnreadMessages (post-Read state) must not fire the wait. The unified definition
        // keeps this property because the inclusion-set is sourced from UnreadMessages.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        CreateMessageFile("Adele", "Brian", "stale-1", "Old1");
        CreateMessageFile("Adele", "Brian", "stale-2", "Old2");
        // Simulate post-Read state: Read tool depletes UnreadMessages but leaves files.
        new AgentRegistry(TestDir).ClearAllUnreadMessages("Adele");

        ProcessUtils.IsProcessRunningOverride = _ => false;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            // No "Message received" — pre-existing inbox files are excluded by snapshot.
            // Wait exits via parent-death (exit 2), not by popping on a stale message.
            result.AssertExitCode(2);
            Assert.DoesNotContain("Message received", result.Stdout);
        }
        finally { ProcessUtils.IsProcessRunningOverride = null; }
    }

    [Fact]
    public async Task WaitGeneral_FiresOnGenuinelyNewArrivalAfterReadDepletedStateMd()
    {
        // Forward functionality with #0147 semantics: stale files (post-Read, not in
        // UnreadMessages) coexist with a fresh arrival (in UnreadMessages). The wait
        // must fire on the fresh one and ignore the stale ones.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        CreateMessageFile("Adele", "Brian", "stale-1", "Old1");
        CreateMessageFile("Adele", "Brian", "stale-2", "Old2");
        // Stale files simulate post-Read: file persists, UnreadMessages depleted.
        new AgentRegistry(TestDir).ClearAllUnreadMessages("Adele");

        var droppedFresh = false;
        var originalPollMs = WaitCommand.PollIntervalMs;
        WaitCommand.PollIntervalMs = 25;

        ProcessUtils.IsProcessRunningOverride = _ =>
        {
            if (!droppedFresh)
            {
                droppedFresh = true;
                CreateMessageFile("Adele", "Charlie", "fresh", "New arrival");
            }
            return true;
        };
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertSuccess();
            result.AssertStdoutContains("Message received from Charlie");
        }
        finally
        {
            ProcessUtils.IsProcessRunningOverride = null;
            WaitCommand.PollIntervalMs = originalPollMs;
        }
    }

    [Fact]
    public async Task WaitGeneral_RefusesSecondRegistrationWhilePriorIsAlive()
    {
        // Bug #0141 secondary fix: concurrent dydo wait processes used to clobber the
        // marker's PID and the first to exit removed the marker for all of them. The
        // idempotency guard refuses a second registration when a live listening marker
        // already exists. Per user override, the refusal is NONZERO + stderr so agents
        // and wrappers see a visible failure (silent Success rewards defensive
        // re-registration before every tool block).
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        // Simulate Wait #1: a live, listening general-wait marker for this test process.
        registry.CreateListeningWaitMarker("Adele", "_general-wait", "Adele", Environment.ProcessId);

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command);

        result.AssertExitCode(2);
        result.AssertStderrContains("already active");
        Assert.Contains($"PID {Environment.ProcessId}", result.Stderr);

        // Marker still has Wait #1's PID; #2 did not overwrite it.
        var markers = new AgentRegistry(TestDir).GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.Equal(Environment.ProcessId, markers[0].Pid);
        Assert.True(markers[0].Listening);
    }

    [Fact]
    public async Task WaitGeneral_PriorWaitDeadCausesNewWaitToTakeOver()
    {
        // Self-heal: if the existing marker's PID is no longer running (process killed
        // or crashed), the idempotency guard must NOT block — the new wait overwrites
        // and proceeds normally.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        const int deadPid = 999_999;
        registry.CreateListeningWaitMarker("Adele", "_general-wait", "Adele", deadPid);

        int? observedPid = null;
        bool? observedListening = null;
        var idempotencyChecked = false;

        ProcessUtils.IsProcessRunningOverride = pid =>
        {
            if (!idempotencyChecked)
            {
                // Idempotency guard probes the stale PID — report it dead so the new
                // wait proceeds to register.
                idempotencyChecked = true;
                return pid != deadPid;
            }
            // After registration, parent/claude liveness check fires — observe the
            // marker (now owned by this process) and exit the wait.
            if (observedPid == null)
            {
                var probe = new AgentRegistry(TestDir).GetWaitMarkers("Adele")
                    .FirstOrDefault(m => m.Task == "_general-wait");
                observedPid = probe?.Pid;
                observedListening = probe?.Listening;
            }
            return false;
        };
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertExitCode(2);
            Assert.Equal(Environment.ProcessId, observedPid);
            Assert.True(observedListening);
        }
        finally { ProcessUtils.IsProcessRunningOverride = null; }
    }

    #endregion

    #region Atomic Listening-Marker Tests (#0133)

    [Fact]
    public async Task CreateListeningWaitMarker_WritesListeningAndPid_InOneStep()
    {
        // Regression for #0133: WaitGeneral previously created the marker with
        // Listening=false, then issued a second write to flip it to true. The window
        // between the two writes left the MissingGeneralWait guard check observing
        // Listening=false and blocking the next tool call. The atomic create method
        // must publish Listening=true and Pid in a single file write.
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateListeningWaitMarker("Adele", "_general-wait", "Adele", 4242);

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.True(markers[0].Listening);
        Assert.Equal(4242, markers[0].Pid);
        Assert.Equal("Adele", markers[0].Target);
        Assert.Equal("_general-wait", markers[0].Task);
    }

    [Fact]
    public async Task CreateListeningWaitMarker_PreservesTargetAndSince_WhenMarkerExists()
    {
        // WaitForTask runs after the dispatcher has pre-created a marker (Listening=false).
        // Flipping to listening atomically must not lose the dispatcher-recorded Target
        // or Since fields — those drive `dydo agent list` and audit display.
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        var original = registry.GetWaitMarkers("Adele")[0];

        // Caller passes its own agent name as the target placeholder; existing Target wins.
        registry.CreateListeningWaitMarker("Adele", "auth", "Adele", 7777);

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.True(markers[0].Listening);
        Assert.Equal(7777, markers[0].Pid);
        Assert.Equal("Brian", markers[0].Target);
        Assert.Equal(original.Since, markers[0].Since);
    }

    [Fact]
    public async Task Wait_General_MarkerListeningWhenLoopStarts()
    {
        // After WaitGeneral starts, the _general-wait marker must satisfy the guard's
        // "general wait active" check (Listening=true + live Pid) by the time the
        // polling loop runs. We observe via IsProcessRunningOverride on the first poll.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        bool? observedListening = null;
        int? observedPid = null;
        var observed = false;

        ProcessUtils.IsProcessRunningOverride = _ =>
        {
            if (!observed)
            {
                observed = true;
                var probe = new AgentRegistry(TestDir);
                var general = probe.GetWaitMarkers("Adele").FirstOrDefault(m => m.Task == "_general-wait");
                observedListening = general?.Listening;
                observedPid = general?.Pid;
            }
            return false;
        };
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            await RunAsync(command);
        }
        finally { ProcessUtils.IsProcessRunningOverride = null; }

        Assert.True(observedListening, "Marker must be Listening=true by the time the polling loop runs.");
        Assert.NotNull(observedPid);
        Assert.True(observedPid > 0);
    }

    [Fact]
    public async Task Wait_Task_MarkerListeningWhenLoopStarts()
    {
        // Parity with WaitGeneral: WaitForTask must also leave the marker in
        // Listening=true with live Pid by the time its polling loop runs. The fix
        // collapses the previous read-modify-non-atomic-write into a single atomic
        // write so guard/list readers can never observe a transient Listening=false.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");

        bool? observedListening = null;
        int? observedPid = null;
        var observed = false;

        ProcessUtils.IsProcessRunningOverride = _ =>
        {
            if (!observed)
            {
                observed = true;
                var probe = new AgentRegistry(TestDir);
                var marker = probe.GetWaitMarkers("Adele").FirstOrDefault(m => m.Task == "auth");
                observedListening = marker?.Listening;
                observedPid = marker?.Pid;
            }
            return false;
        };
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            await RunAsync(command, "--task", "auth");
        }
        finally { ProcessUtils.IsProcessRunningOverride = null; }

        Assert.True(observedListening, "Task wait marker must be Listening=true by the time the polling loop runs.");
        Assert.NotNull(observedPid);
        Assert.True(observedPid > 0);
    }

    #endregion

    #region Helper Methods

    private void CreateMessageFile(string agentName, string fromAgent, string subject, string body)
    {
        CreateMessageFileReturningId(agentName, fromAgent, subject, body);
    }

    private string CreateMessageFileReturningId(string agentName, string fromAgent, string subject, string body)
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
        // Mirror the production write path: every delivered file is paired with an
        // UnreadMessages entry so WaitGeneral's inclusion-set semantics fire on it.
        new AgentRegistry(TestDir).AddUnreadMessage(agentName, id);
        return id;
    }

    private static void WriteMessageFileDirect(string inboxPath, string id, string from, string subject, string body, DateTime received)
    {
        var content = $"""
            ---
            id: {id}
            type: message
            from: {from}
            subject: {subject}
            received: {received:o}
            ---

            ## Body

            {body}
            """;
        File.WriteAllText(Path.Combine(inboxPath, $"{id}-msg-{subject}.md"), content);
    }

    #endregion
}
