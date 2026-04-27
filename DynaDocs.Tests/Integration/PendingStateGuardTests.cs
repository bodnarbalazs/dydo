namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;

[Collection("Integration")]
public class PendingStateGuardTests : IntegrationTestBase
{
    #region WaitMarker Model Tests

    [Fact]
    public async Task WaitMarker_Serialization_IncludesListeningAndPid()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Use AgentRegistry to create and read — it uses the internal JSON context
        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.UpdateWaitMarkerListening("Adele", "auth", 12345);

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.True(markers[0].Listening);
        Assert.Equal(12345, markers[0].Pid);
    }

    [Fact]
    public async Task WaitMarker_BackwardCompat_DefaultsFalseNull()
    {
        await InitProjectAsync("none", "testuser", 3);

        // Write a JSON without listening/pid to simulate legacy marker
        var waitingDir = Path.Combine(TestDir, "dydo", "agents", "Adele", ".waiting");
        Directory.CreateDirectory(waitingDir);
        var json = """{"target":"Brian","task":"auth","since":"2026-01-01T00:00:00Z"}""";
        File.WriteAllText(Path.Combine(waitingDir, "auth.json"), json);

        var registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.False(markers[0].Listening);
        Assert.Null(markers[0].Pid);
    }

    [Fact]
    public async Task WaitMarker_RoundTrip_WithListeningAndPid()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.UpdateWaitMarkerListening("Adele", "auth", 42);

        // Re-instantiate to force fresh read from disk
        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.True(markers[0].Listening);
        Assert.Equal(42, markers[0].Pid);
        Assert.Equal("Brian", markers[0].Target);
        Assert.Equal("auth", markers[0].Task);
    }

    #endregion

    #region AgentRegistry Tests

    [Fact]
    public async Task UpdateWaitMarkerListening_SetsListeningAndPid()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");

        var result = registry.UpdateWaitMarkerListening("Adele", "auth", 12345);
        Assert.True(result);

        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.True(markers[0].Listening);
        Assert.Equal(12345, markers[0].Pid);
    }

    [Fact]
    public async Task UpdateWaitMarkerListening_NonexistentMarker_ReturnsFalse()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        var result = registry.UpdateWaitMarkerListening("Adele", "nonexistent", 12345);
        Assert.False(result);
    }

    [Fact]
    public async Task GetNonListeningWaitMarkers_ReturnsOnlyNonListening()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.CreateWaitMarker("Adele", "login", "Charlie");
        registry.UpdateWaitMarkerListening("Adele", "auth", 12345);

        var nonListening = registry.GetNonListeningWaitMarkers("Adele");
        Assert.Single(nonListening);
        Assert.Equal("login", nonListening[0].Task);
    }

    [Fact]
    public async Task GetNonListeningWaitMarkers_AllListening_ReturnsEmpty()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.UpdateWaitMarkerListening("Adele", "auth", 12345);

        var nonListening = registry.GetNonListeningWaitMarkers("Adele");
        Assert.Empty(nonListening);
    }

    [Fact]
    public async Task GetNonListeningWaitMarkers_NoMarkers_ReturnsEmpty()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        var nonListening = registry.GetNonListeningWaitMarkers("Adele");
        Assert.Empty(nonListening);
    }

    [Fact]
    public async Task ResetWaitMarkerListening_FlipsToFalse()
    {
        await InitProjectAsync("none", "testuser", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");
        registry.UpdateWaitMarkerListening("Adele", "auth", 12345);

        // Verify it's listening
        var markers = registry.GetWaitMarkers("Adele");
        Assert.True(markers[0].Listening);

        // Reset
        registry.ResetWaitMarkerListening("Adele", "auth");

        markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.False(markers[0].Listening);
        Assert.Null(markers[0].Pid);
    }

    #endregion

    #region Guard Pending-State Enforcement — Non-Bash Tools

    [Fact]
    public async Task Guard_BlocksWrite_WhenPendingWaitMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        // Create a non-listening wait marker
        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        var result = await GuardAsync("write", "Commands/SomeFile.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED: Register waits before continuing");
        result.AssertStderrContains("sub-task");
    }

    [Fact]
    public async Task Guard_BlocksEdit_WhenPendingWaitMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        var result = await GuardAsync("edit", "Commands/SomeFile.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED: Register waits before continuing");
    }

    [Fact]
    public async Task Guard_BlocksRead_WhenPendingWaitMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        var result = await GuardAsync("read", "Commands/SomeFile.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED: Register waits before continuing");
    }

    [Fact]
    public async Task Guard_BlocksGlob_WhenPendingWaitMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        var json = $$"""
            {
                "session_id": "{{TestSessionId}}",
                "tool_name": "Glob",
                "tool_input": {
                    "pattern": "**/*.cs"
                }
            }
            """;
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED: Register waits before continuing");
    }

    [Fact]
    public async Task Guard_BlocksGrep_WhenPendingWaitMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        var json = $$"""
            {
                "session_id": "{{TestSessionId}}",
                "tool_name": "Grep",
                "tool_input": {
                    "pattern": "test"
                }
            }
            """;
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED: Register waits before continuing");
    }

    [Fact]
    public async Task Guard_AllowsAllTools_AfterMarkersAreListening()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        // Make it listening
        registry.UpdateWaitMarkerListening("Adele", "sub-task", Environment.ProcessId);

        var result = await GuardAsync("read", "Commands/SomeFile.cs");
        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_ErrorMessage_ListsPendingTaskNames()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "task-one", "Brian");
        registry.CreateWaitMarker("Adele", "task-two", "Charlie");

        var result = await GuardAsync("write", "Commands/SomeFile.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("task-one");
        result.AssertStderrContains("task-two");
    }

    #endregion

    #region Guard Pending-State Enforcement — Bash Commands

    [Fact]
    public async Task Guard_BlocksBashCommand_WhenPendingWaitMarkers()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        var json = $$"""
            {
                "session_id": "{{TestSessionId}}",
                "tool_name": "Bash",
                "tool_input": {
                    "command": "ls -la"
                }
            }
            """;
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED: Register waits before continuing");
    }

    [Fact]
    public async Task Guard_AllowsDydoDispatch_DuringPendingState()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        var json = $$"""
            {
                "session_id": "{{TestSessionId}}",
                "tool_name": "Bash",
                "tool_input": {
                    "command": "dydo dispatch --wait --role reviewer --task new-task --brief test --no-launch"
                }
            }
            """;
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_AllowsDydoWait_DuringPendingState()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        var json = $$"""
            {
                "session_id": "{{TestSessionId}}",
                "tool_name": "Bash",
                "tool_input": {
                    "command": "dydo wait --task sub-task",
                    "run_in_background": true
                }
            }
            """;
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_AllowsDydoWaitCancel_DuringPendingState()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        var json = $$"""
            {
                "session_id": "{{TestSessionId}}",
                "tool_name": "Bash",
                "tool_input": {
                    "command": "dydo wait --task sub-task --cancel"
                }
            }
            """;
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_BlocksOtherDydoCommand_DuringPendingState()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        var json = $$"""
            {
                "session_id": "{{TestSessionId}}",
                "tool_name": "Bash",
                "tool_input": {
                    "command": "dydo whoami"
                }
            }
            """;
        var result = await GuardWithStdinAsync(json);

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED: Register waits before continuing");
    }

    #endregion

    #region Guard Self-Healing

    [Fact]
    public async Task Guard_SelfHeals_ListeningMarkerWithDeadPid()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");
        // Set listening with a PID that doesn't exist
        registry.UpdateWaitMarkerListening("Adele", "sub-task", 999999);

        // Guard should self-heal: detect dead PID, flip to non-listening, then block
        var result = await GuardAsync("read", "Commands/SomeFile.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED: Register waits before continuing");

        // Verify the marker was flipped
        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.False(markers[0].Listening);
        Assert.Null(markers[0].Pid);
    }

    [Fact]
    public async Task Guard_DoesNotFlip_ListeningMarkerWithAlivePid()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");
        // Use current process PID (alive)
        registry.UpdateWaitMarkerListening("Adele", "sub-task", Environment.ProcessId);

        // Guard should NOT flip — marker is listening with alive PID
        var result = await GuardAsync("read", "Commands/SomeFile.cs");

        result.AssertSuccess();

        // Verify the marker is still listening
        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.True(markers[0].Listening);
        Assert.Equal(Environment.ProcessId, markers[0].Pid);
    }

    [Fact]
    public async Task Guard_DoesNotFlip_NonListeningMarker()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        // Marker is non-listening by default — guard should block but not try to "heal" it
        var result = await GuardAsync("read", "Commands/SomeFile.cs");

        result.AssertExitCode(2);

        // Marker should still be non-listening (unchanged)
        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.False(markers[0].Listening);
        Assert.Null(markers[0].Pid);
    }

    [Fact]
    public async Task Guard_SelfHeals_ListeningMarkerWithNullPid()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        // Manually create a marker with listening=true but pid=null (legacy)
        var waitingDir = Path.Combine(TestDir, "dydo", "agents", "Adele", ".waiting");
        Directory.CreateDirectory(waitingDir);
        var markerJson = """{"target":"Brian","task":"sub-task","since":"2026-01-01T00:00:00Z","listening":true}""";
        File.WriteAllText(Path.Combine(waitingDir, "sub-task.json"), markerJson);

        // Guard should detect null PID on listening marker and flip to non-listening
        var result = await GuardAsync("read", "Commands/SomeFile.cs");

        result.AssertExitCode(2);
        result.AssertStderrContains("BLOCKED: Register waits before continuing");

        // Verify marker was flipped
        var registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Single(markers);
        Assert.False(markers[0].Listening);
    }

    #endregion

    #region WaitCommand Listening Integration

    [Fact]
    public async Task WaitCommand_SetsMarkerToListening()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");

        // Create a message so wait returns immediately
        CreateMessageFile("Adele", "Brian", "auth", "Done.");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command, "--task", "auth");

        result.AssertSuccess();

        // Marker should have been cleaned up on message receipt,
        // but the listening flag was set before the polling loop
    }

    [Fact]
    public async Task WaitCommand_WithoutMatchingMarker_StillWorks()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        // No wait marker exists — but message is pre-placed
        CreateMessageFile("Adele", "Brian", "auth", "Done.");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command, "--task", "auth");

        result.AssertSuccess();
    }

    [Fact]
    public async Task WaitCommand_Cancel_SpecificTask_RemovesMarkerEntirely()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "auth", "Brian");

        StoreSessionContext();
        var command = WaitCommand.Create();
        var result = await RunAsync(command, "--task", "auth", "--cancel");

        result.AssertSuccess();

        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Empty(markers);
    }

    [Fact]
    public async Task WaitCommand_Cancel_AllTasks_RemovesAllMarkers()
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

        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.Empty(markers);
    }

    #endregion

    #region Pending Enforcement Lifecycle

    [Fact]
    public async Task PendingEnforcement_CancelDuringPending_FreesAgent()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        // Dispatch with --wait creates a non-listening marker
        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        // Blocked
        var blocked = await GuardAsync("read", "Commands/SomeFile.cs");
        blocked.AssertExitCode(2);

        // Cancel the wait
        StoreSessionContext();
        var waitCmd = WaitCommand.Create();
        await RunAsync(waitCmd, "--task", "sub-task", "--cancel");

        // Now allowed
        var allowed = await GuardAsync("read", "Commands/SomeFile.cs");
        allowed.AssertSuccess();
    }

    #endregion

    #region Orchestrator General-Wait Enforcement

    [Fact]
    public async Task Guard_BlocksOrchestrator_WithTaskWait_ButNoGeneralWait()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "my-task", "co-thinker");
        await SetRoleAsync("orchestrator", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");
        registry.UpdateWaitMarkerListening("Adele", "sub-task", Environment.ProcessId);

        var result = await GuardAsync("read", "dydo/index.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("Orchestrator must keep a general wait active");
        result.AssertStderrContains("dydo wait");
    }

    [Fact]
    public async Task Guard_AllowsOrchestrator_WhenGeneralWaitListening()
    {
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "my-task", "co-thinker");
        await SetRoleAsync("orchestrator", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");
        registry.UpdateWaitMarkerListening("Adele", "sub-task", Environment.ProcessId);
        registry.CreateWaitMarker("Adele", "_general-wait", "Adele");
        registry.UpdateWaitMarkerListening("Adele", "_general-wait", Environment.ProcessId);

        var result = await GuardAsync("read", "dydo/index.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_BlocksOrchestrator_WhenGeneralWaitHasDeadPid()
    {
        // Force-killed general wait leaves a stale marker. Self-heal removes it; the
        // orchestrator-general-wait check then fires until a fresh wait is started.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "my-task", "co-thinker");
        await SetRoleAsync("orchestrator", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");
        registry.UpdateWaitMarkerListening("Adele", "sub-task", Environment.ProcessId);
        registry.CreateWaitMarker("Adele", "_general-wait", "Adele");
        registry.UpdateWaitMarkerListening("Adele", "_general-wait", 999999);

        var result = await GuardAsync("read", "dydo/index.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("Orchestrator must keep a general wait active");

        // Self-heal deleted the stale general-wait marker
        registry = new AgentRegistry(TestDir);
        var markers = registry.GetWaitMarkers("Adele");
        Assert.DoesNotContain(markers, m => m.Task == "_general-wait");
    }

    [Fact]
    public async Task Guard_AllowsOrchestrator_WhenNoTaskWaitsYet()
    {
        // Onboarding path: orchestrator has set role but hasn't dispatched anything yet.
        // The general-wait check is gated on having at least one task wait, so reads
        // during onboarding are not blocked.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "my-task", "co-thinker");
        await SetRoleAsync("orchestrator", "my-task");
        await ReadMustReadsAsync();

        var result = await GuardAsync("read", "dydo/index.md");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_DoesNotBlockNonOrchestrator_OnMissingGeneralWait()
    {
        // Code-writer with all task waits listening → existing check passes; new check
        // does not apply because role is not orchestrator.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");
        registry.UpdateWaitMarkerListening("Adele", "sub-task", Environment.ProcessId);

        var result = await GuardAsync("read", "Commands/SomeFile.cs");

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_AllowsDydoWaitGeneral_DuringMissingGeneralWaitBlock()
    {
        // The bash bypass for dydo wait must work so the orchestrator can start
        // the general wait that the new check requires.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "my-task", "co-thinker");
        await SetRoleAsync("orchestrator", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");
        registry.UpdateWaitMarkerListening("Adele", "sub-task", Environment.ProcessId);

        var json = $$"""
            {
                "session_id": "{{TestSessionId}}",
                "tool_name": "Bash",
                "tool_input": {
                    "command": "dydo wait",
                    "run_in_background": true
                }
            }
            """;
        var result = await GuardWithStdinAsync(json);

        result.AssertSuccess();
    }

    [Fact]
    public async Task Guard_BlocksWithBothMessages_WhenTaskWaitNonListeningAndNoGeneralWait()
    {
        // Both signals fire on the same call: pending task waits AND missing general wait.
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "my-task", "co-thinker");
        await SetRoleAsync("orchestrator", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");

        var result = await GuardAsync("read", "dydo/index.md");

        result.AssertExitCode(2);
        result.AssertStderrContains("Register waits before continuing");
        result.AssertStderrContains("sub-task");
        result.AssertStderrContains("Orchestrator must keep a general wait active");
    }

    [Fact]
    public async Task Guard_GeneralWaitMarker_NotIncluded_InPendingTaskList()
    {
        // The "_general-wait" sentinel should never appear in the task-list error message,
        // even when its underlying marker is non-listening (e.g. self-healed dead PID).
        await InitProjectAsync("none", "testuser", 3);
        await ClaimAgentAsync("Adele");
        SetTaskRoleHistory("Adele", "my-task", "co-thinker");
        await SetRoleAsync("orchestrator", "my-task");
        await ReadMustReadsAsync();

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "sub-task", "Brian");
        registry.UpdateWaitMarkerListening("Adele", "sub-task", Environment.ProcessId);
        // Stale general-wait marker with dead PID
        registry.CreateWaitMarker("Adele", "_general-wait", "Adele");
        registry.UpdateWaitMarkerListening("Adele", "_general-wait", 999999);

        var result = await GuardAsync("read", "dydo/index.md");

        result.AssertExitCode(2);
        Assert.DoesNotContain("_general-wait", result.Stderr);
    }

    #endregion

    #region Helper Methods

    private void SetTaskRoleHistory(string agentName, string task, string role)
    {
        var statePath = Path.Combine(TestDir, "dydo", "agents", agentName, "state.md");
        if (!File.Exists(statePath)) return;

        var content = File.ReadAllText(statePath);
        content = System.Text.RegularExpressions.Regex.Replace(
            content,
            @"^task-role-history:.*$",
            $"task-role-history: {{ \"{task}\": [\"{role}\"] }}",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        File.WriteAllText(statePath, content);
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

    #endregion
}
