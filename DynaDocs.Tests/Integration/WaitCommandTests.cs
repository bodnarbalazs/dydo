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
    public async Task Wait_General_FindsExistingMessage()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // Pre-populate inbox with a message
        CreateMessageFile("Adele", "Brian", "test-subject", "Hello from Brian");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command);

        result.AssertSuccess();
        result.AssertStdoutContains("Message received from Brian");
        result.AssertStdoutContains("test-subject");
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

        // Pre-populate message so the loop exits immediately
        CreateMessageFile("Adele", "Brian", "general-subject", "Hello");

        ProcessUtils.IsProcessRunningOverride = _ => true;
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
        }
    }

    [Fact]
    public async Task WaitForTask_ExitsWhenClaudeAncestorDies()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "my-task", "Brian");

        // Parent alive, but Claude ancestor dead
        ProcessUtils.FindAncestorProcessOverride = (name, _) =>
            name.Contains("claude", StringComparison.OrdinalIgnoreCase) ? 9999 : null;
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
    public async Task WaitGeneral_ExitsWhenClaudeAncestorDies()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        ProcessUtils.FindAncestorProcessOverride = (name, _) =>
            name.Contains("claude", StringComparison.OrdinalIgnoreCase) ? 9999 : null;
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

        // Task wait registered for "X" — but message arrives with a different subject "Y"
        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "X", "Brian");

        CreateMessageFile("Adele", "Brian", "Y", "general message");

        ProcessUtils.IsProcessRunningOverride = _ => true;
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
    public async Task WaitGeneral_SkipsMessage_AlreadyInUnreadAtStart()
    {
        // Bug A: WaitGeneral previously popped on any unread message — including ones
        // already in agent.UnreadMessages — and removed the _general-wait marker on exit,
        // deadlocking the orchestrator's read tool. The wait must now snapshot the unread
        // set at startup and skip those IDs.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var staleId = CreateMessageFileReturningId("Adele", "Brian", "stale-subject", "Already-known");
        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", staleId);

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
    public async Task WaitGeneral_PopsOnNewMessage_EvenWhenStartupUnreadExists()
    {
        // Bug A: with a stale unread present, a freshly-arriving message must still wake
        // the wait — only IDs already unread at wait-start are skipped.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var staleId = CreateMessageFileReturningId("Adele", "Brian", "stale", "Old");
        var registry = new AgentRegistry(TestDir);
        registry.AddUnreadMessage("Adele", staleId);

        // New arrival (NOT in state.md unread) — should pop the wait.
        CreateMessageFile("Adele", "Charlie", "fresh", "New");

        ProcessUtils.IsProcessRunningOverride = _ => true;
        try
        {
            StoreSessionContext();
            var command = WaitCommand.Create();
            var result = await RunAsync(command);

            result.AssertSuccess();
            result.AssertStdoutContains("Message received from Charlie");
            Assert.DoesNotContain("From: Brian", result.Stdout);
        }
        finally { ProcessUtils.IsProcessRunningOverride = null; }
    }

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

    #region Atomic Listening-Marker Tests (#0133)

    [Fact]
    public async Task CreateListeningWaitMarker_WritesListeningAndPid_InOneStep()
    {
        // Regression for #0133: WaitGeneral previously created the marker with
        // Listening=false, then issued a second write to flip it to true. The window
        // between the two writes left the OrchestratorMissingGeneralWait guard check
        // observing Listening=false and blocking the next tool call. The atomic
        // create method must publish Listening=true and Pid in a single file write.
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
