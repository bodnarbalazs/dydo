namespace DynaDocs.Tests.Integration;

using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>
/// Integration tests for agent lifecycle commands:
/// whoami, claim, release, status, list, role.
/// </summary>
[Collection("Integration")]
public class AgentLifecycleTests : IntegrationTestBase
{
    #region Whoami

    [Fact]
    public async Task Whoami_NoAgent_ShowsHumanAndAvailable()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await WhoamiAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("balazs");
        // Should show available agents
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Whoami_WithAgent_ShowsIdentity()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await WhoamiAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Whoami_WithRoleSet_ShowsRole()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        var result = await WhoamiAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("code-writer");
    }

    #endregion

    #region Claim

    [Fact]
    public async Task Claim_Auto_ClaimsFirstFree()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ClaimAgentAsync("auto");

        result.AssertSuccess();
        // Should claim first available (Adele)
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Claim_ByName_ClaimsSpecific()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ClaimAgentAsync("Brian");

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");
    }

    [Fact]
    public async Task Claim_WrongHuman_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Try to claim an agent assigned to a different human
        // First, add another human with their own agents
        await JoinProjectAsync("none", "alice", 2);

        // Set human to balazs and try to claim alice's agent
        SetHuman("balazs");
        // Store pending session (simulates guard hook) - must do this before claim
        StorePendingSession("Dexter");
        var command = AgentCommand.Create();
        var result = await RunAsync(command, "claim", "Dexter"); // Dexter is alice's agent

        result.AssertExitCode(2);
        result.AssertStderrContains("assigned to human");
    }

    [Fact]
    public async Task Claim_AlreadyClaimed_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Try to claim another agent while one is already claimed
        var result = await ClaimAgentAsync("Brian");

        result.AssertExitCode(2);
        result.AssertStderrContains("already");
    }

    [Fact]
    public async Task Claim_NonExistent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ClaimAgentAsync("NotARealAgent");

        result.AssertExitCode(2);
        result.AssertStderrContains("Invalid agent name");
    }

    [Fact]
    public async Task Claim_ByLetter_IsCaseInsensitive()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Claim by first letter (lowercase should work same as uppercase)
        var result = await ClaimAgentAsync("a"); // Should match "Adele"

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Claim_ByLetter_Uppercase_Works()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ClaimAgentAsync("B"); // Should match "Brian"

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");
    }

    [Fact]
    public async Task Claim_ByLetter_NoMatch_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        // No agent starts with 'z'
        var result = await ClaimAgentAsync("z");

        result.AssertExitCode(2);
        result.AssertStderrContains("Invalid agent name");
    }

    [Fact]
    public async Task Claim_AfterRelease_Succeeds()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await ReleaseAgentAsync();

        // Re-claim same agent (uses session context fallback since no new pending session)
        var result = await ClaimAgentAsync("Adele");

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Claim_AfterMultipleReleases_Succeeds()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Cycle 1: claim and release
        await ClaimAgentAsync("Adele");
        await ReleaseAgentAsync();

        // Cycle 2: re-claim and release
        await ClaimAgentAsync("Adele");
        await ReleaseAgentAsync();

        // Cycle 3: re-claim again
        var result = await ClaimAgentAsync("Adele");

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
    }

    #endregion

    #region Release

    [Fact]
    public async Task Release_FreesAgent()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await ReleaseAgentAsync();

        result.AssertSuccess();

        // Agent should now be free
        var listResult = await ListAgentsAsync(freeOnly: true);
        listResult.AssertStdoutContains("Adele");
    }

    [Fact]
    public async Task Release_NoAgent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ReleaseAgentAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("No agent");
    }

    [Fact]
    public async Task Release_ClearsRole()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");
        await ReleaseAgentAsync();

        // Claim again and verify role is cleared
        await ClaimAgentAsync("Adele");
        var status = await AgentStatusAsync();

        status.AssertSuccess();
        // Should not have the old role
        Assert.DoesNotContain("code-writer", status.Stdout);
    }

    #endregion

    #region Status

    [Fact]
    public async Task Status_ShowsCurrentState()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "implement-feature");

        var result = await AgentStatusAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
        result.AssertStdoutContains("code-writer");
        result.AssertStdoutContains("implement-feature");
    }

    [Fact]
    public async Task Status_ByName_ShowsSpecificAgent()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await AgentStatusAsync("Brian");

        result.AssertSuccess();
        result.AssertStdoutContains("Brian");
    }

    [Fact]
    public async Task Status_NoAgent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await AgentStatusAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("No agent");
    }

    #endregion

    #region List

    [Fact]
    public async Task List_ShowsAllAgents()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ListAgentsAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
        result.AssertStdoutContains("Brian");
        result.AssertStdoutContains("Charlie");
    }

    [Fact]
    public async Task List_Free_ShowsOnlyFree()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await ListAgentsAsync(freeOnly: true);

        result.AssertSuccess();
        // Adele is claimed, so should not be in free list
        Assert.DoesNotContain("Adele", result.Stdout);
        result.AssertStdoutContains("Brian");
        result.AssertStdoutContains("Charlie");
    }

    [Fact]
    public async Task List_ShowsHumanAssignments()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 2);

        var result = await ListAgentsAsync(all: true);

        result.AssertSuccess();
        result.AssertStdoutContains("balazs");
        result.AssertStdoutContains("alice");
    }

    [Fact]
    public async Task List_Default_ShowsOnlyCurrentHumanAgents()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 2);

        SetHuman("balazs");
        var result = await ListAgentsAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
        result.AssertStdoutContains("Brian");
        result.AssertStdoutContains("Charlie");
        // Alice's agents should not appear
        Assert.DoesNotContain("Dexter", result.Stdout);
        Assert.DoesNotContain("Emma", result.Stdout);
    }

    [Fact]
    public async Task List_All_ShowsAllAgents()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 2);

        SetHuman("balazs");
        var result = await ListAgentsAsync(all: true);

        result.AssertSuccess();
        // Both humans' agents should appear
        result.AssertStdoutContains("Adele");
        result.AssertStdoutContains("Dexter");
    }

    [Fact]
    public async Task List_Default_ShowsTaskColumn()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");

        var result = await ListAgentsAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Task");
        result.AssertStdoutContains("my-task");
    }

    [Fact]
    public async Task List_Default_NoHumanSet_ShowsError()
    {
        await InitProjectAsync("none", "balazs", 3);

        ClearHuman();
        var result = await ListAgentsAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("No human identity set");
    }

    [Fact]
    public async Task List_Default_WaitingForColumn_AppearsBeforeTask()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ListAgentsAsync();

        result.AssertSuccess();
        var header = result.Stdout.Split('\n').First(l => l.Contains("Agent"));
        var waitIdx = header.IndexOf("Waiting For");
        var taskIdx = header.IndexOf("Task");
        Assert.True(waitIdx >= 0, "Header missing 'Waiting For' column");
        Assert.True(waitIdx < taskIdx, "'Waiting For' column should appear before 'Task'");
    }

    [Fact]
    public async Task List_Default_ShowsWaitTargetOnCorrectRow()
    {
        await InitProjectAsync("none", "balazs", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "my-task", "Brian");

        var result = await ListAgentsAsync();

        result.AssertSuccess();
        var adeleLine = result.Stdout.Split('\n').First(l => l.Contains("Adele"));
        Assert.Contains("Brian", adeleLine);

        // Brian's own row should NOT show Brian as a wait target
        var brianLine = result.Stdout.Split('\n').First(l => l.StartsWith("Brian"));
        Assert.DoesNotContain("Brian", brianLine.Substring("Brian".Length));
    }

    [Fact]
    public async Task List_Default_NoWaitMarkers_WaitColumnShowsDash()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "my-task");

        var result = await ListAgentsAsync();

        result.AssertSuccess();
        // Brian has a role of "-" and no wait markers.
        // Extract the Waiting For column value from Brian's line using header offsets.
        var header = result.Stdout.Split('\n').First(l => l.Contains("Agent"));
        var waitCol = header.IndexOf("Waiting For");
        var taskCol = header.IndexOf("Task");
        var brianLine = result.Stdout.Split('\n').First(l => l.StartsWith("Brian"));
        var waitValue = brianLine.Substring(waitCol, taskCol - waitCol).Trim();
        Assert.Equal("-", waitValue);
    }

    [Fact]
    public async Task List_Default_MultipleWaitTargets_ShowsCommaSeparated()
    {
        await InitProjectAsync("none", "balazs", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "task-a", "Brian");
        registry.CreateWaitMarker("Adele", "task-b", "Charlie");

        var result = await ListAgentsAsync();

        result.AssertSuccess();
        var adeleLine = result.Stdout.Split('\n').First(l => l.Contains("Adele"));
        Assert.Contains("Brian", adeleLine);
        Assert.Contains("Charlie", adeleLine);
        Assert.Contains(",", adeleLine);
    }

    [Fact]
    public async Task List_All_WaitingForColumn_AppearsBeforeRole()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ListAgentsAsync(all: true);

        result.AssertSuccess();
        var header = result.Stdout.Split('\n').First(l => l.Contains("Agent"));
        var waitIdx = header.IndexOf("Waiting For");
        var roleIdx = header.IndexOf("Role");
        Assert.True(waitIdx >= 0, "Header missing 'Waiting For' column");
        Assert.True(waitIdx < roleIdx, "'Waiting For' column should appear before 'Role'");
    }

    [Fact]
    public async Task List_All_ShowsWaitTargetOnCorrectRow()
    {
        await InitProjectAsync("none", "balazs", 3);

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Brian", "build-api", "Adele");

        var result = await ListAgentsAsync(all: true);

        result.AssertSuccess();
        var brianLine = result.Stdout.Split('\n').First(l => l.StartsWith("Brian"));
        Assert.Contains("Adele", brianLine);

        // Adele's row should not show Adele as a wait target
        var adeleLine = result.Stdout.Split('\n').First(l => l.StartsWith("Adele"));
        Assert.DoesNotContain("Adele", adeleLine.Substring("Adele".Length));
    }

    [Fact]
    public async Task List_Free_ShowsWaitingForColumn()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ListAgentsAsync(freeOnly: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Waiting For");
    }

    [Fact]
    public async Task List_AgentWithInbox_ShowsAsterisk()
    {
        await InitProjectAsync("none", "balazs", 3);
        PlantInboxItem("Adele", "test-task");

        var result = await ListAgentsAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele*");
    }

    [Fact]
    public async Task List_AgentWithoutInbox_NoAsterisk()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await ListAgentsAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
        Assert.DoesNotContain("Adele*", result.Stdout);
    }

    [Fact]
    public async Task List_All_AgentWithInbox_ShowsAsterisk()
    {
        await InitProjectAsync("none", "balazs", 3);
        PlantInboxItem("Brian", "test-task");

        var result = await ListAgentsAsync(all: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Brian*");
    }

    [Fact]
    public async Task List_9CharNameWithInbox_AsteriskFitsColumn()
    {
        await InitProjectAsync("none", "balazs", 3);
        await JoinProjectAsync("none", "alice", 0);
        await AgentNewAsync("Alejandro", "alice");
        PlantInboxItem("Alejandro", "test-task");

        var result = await ListAgentsAsync(all: true);

        result.AssertSuccess();
        result.AssertStdoutContains("Alejandro*");
    }

    private void PlantInboxItem(string agentName, string task, string from = "Test")
    {
        var inboxDir = Path.Combine("dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(Path.Combine(TestDir, inboxDir));
        WriteFile(Path.Combine(inboxDir, $"deadbeef-{task}.md"), $"""
            ---
            id: deadbeef
            from: {from}
            role: code-writer
            task: {task}
            received: 2026-01-01T00:00:00Z
            ---
            # CODE-WRITER Request: {task}
            ## Brief
            Test brief
            """);
    }

    private async Task<CommandResult> AgentNewAsync(string name, string human)
    {
        var command = AgentCommand.Create();
        return await RunAsync(command, "new", name, human);
    }

    #endregion

    #region Role

    [Fact]
    public async Task Role_SetsPermissions()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await SetRoleAsync("code-writer");

        result.AssertSuccess();

        // Verify role is set
        var status = await AgentStatusAsync();
        status.AssertStdoutContains("code-writer");
    }

    [Fact]
    public async Task Role_WithTask_SetsBoth()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await SetRoleAsync("code-writer", "implement-auth");

        result.AssertSuccess();

        var status = await AgentStatusAsync();
        status.AssertStdoutContains("code-writer");
        status.AssertStdoutContains("implement-auth");
    }

    [Fact]
    public async Task Role_Invalid_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await SetRoleAsync("invalid-role-name");

        result.AssertExitCode(2);
        result.AssertStderrContains("Invalid role");
    }

    [Fact]
    public async Task Role_NoAgent_Fails()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await SetRoleAsync("code-writer");

        result.AssertExitCode(2);
        result.AssertStderrContains("No agent");
    }

    [Fact]
    public async Task Role_CanSwitchRoles()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer");

        var result = await SetRoleAsync("reviewer");

        result.AssertSuccess();

        var status = await AgentStatusAsync();
        status.AssertStdoutContains("reviewer");
        Assert.DoesNotContain("code-writer", status.Stdout);
    }

    #endregion

    #region Claim Regenerates Files

    [Fact]
    public async Task Claim_RegeneratesModeFiles()
    {
        await InitProjectAsync("none", "balazs", 3);

        // Modify the workflow.md file to verify it gets regenerated
        var workflowPath = Path.Combine(TestDir, "dydo/agents/Adele/workflow.md");
        Directory.CreateDirectory(Path.GetDirectoryName(workflowPath)!);
        File.WriteAllText(workflowPath, "# Modified content that should be overwritten");

        // Claim the agent
        var result = await ClaimAgentAsync("Adele");
        result.AssertSuccess();

        // Verify the workflow file was regenerated (should contain template content)
        var content = File.ReadAllText(workflowPath);
        Assert.Contains("Adele", content);
        Assert.DoesNotContain("Modified content that should be overwritten", content);
    }

    #endregion

    #region Release Inbox Checks

    [Fact]
    public async Task Release_BlocksOnUnprocessedInbox()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Create an unprocessed inbox item
        var inboxPath = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        Directory.CreateDirectory(inboxPath);
        File.WriteAllText(Path.Combine(inboxPath, "test-item.md"), """
            ---
            id: test123
            from: Brian
            role: reviewer
            task: test-task
            received: 2024-01-01T00:00:00Z
            ---
            # Test
            """);

        // Try to release - should fail
        var result = await ReleaseAgentAsync();

        result.AssertExitCode(2);
        result.AssertStderrContains("Cannot release");
        result.AssertStderrContains("unprocessed inbox");
    }

    [Fact]
    public async Task Release_AllowsAfterInboxCleared()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Create an inbox item
        var inboxPath = Path.Combine(TestDir, "dydo/agents/Adele/inbox");
        Directory.CreateDirectory(inboxPath);
        File.WriteAllText(Path.Combine(inboxPath, "test-item.md"), """
            ---
            id: test123
            from: Brian
            role: reviewer
            task: test-task
            received: 2024-01-01T00:00:00Z
            ---
            # Test
            """);

        // Clear the inbox
        var clearResult = await InboxClearAsync(all: true);
        clearResult.AssertSuccess();

        // Now release should work
        var result = await ReleaseAgentAsync();
        result.AssertSuccess();
    }

    // Release_PrunesArchiveToTen removed — inbox archive pruning on release
    // is superseded by workspace-level archiving on claim (ArchiveWorkspace + PruneArchive).

    [Fact]
    public async Task Release_IgnoresArchiveWhenCheckingInbox()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Create archive folder with items (these should not block release)
        var archivePath = Path.Combine(TestDir, "dydo/agents/Adele/archive/inbox");
        Directory.CreateDirectory(archivePath);
        File.WriteAllText(Path.Combine(archivePath, "archived-item.md"), "# Archived");

        // Release should succeed (archived items don't count as unprocessed)
        var result = await ReleaseAgentAsync();
        result.AssertSuccess();
    }

    [Fact]
    public async Task Release_RemovesModeFiles()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Modes exist after claim
        var modesPath = Path.Combine(TestDir, "dydo/agents/Adele/modes");
        Assert.True(Directory.Exists(modesPath));

        var result = await ReleaseAgentAsync();
        result.AssertSuccess();

        // Modes should be removed after release
        Assert.False(Directory.Exists(modesPath), "Modes folder should be removed after release");
    }

    [Fact]
    public async Task Release_WithAutoCloseState_PreservesAutoCloseForWatchdog()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Set auto-close in state file (simulating what DispatchCommand does)
        var registry = new AgentRegistry(TestDir);
        registry.SetDispatchMetadata("Adele", "abcd1234", true);

        var result = await ReleaseAgentAsync();
        result.AssertSuccess();

        // Auto-close and window-id should survive release for the watchdog
        var statePath = Path.Combine(TestDir, "dydo/agents/Adele/state.md");
        var stateContent = File.ReadAllText(statePath);
        Assert.Contains("auto-close: true", stateContent);
        Assert.Contains("window-id: abcd1234", stateContent);
        Assert.Contains("status: free", stateContent);
    }

    [Fact]
    public async Task Release_WithoutAutoClose_StateShowsFalse()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await ReleaseAgentAsync();
        result.AssertSuccess();

        var statePath = Path.Combine(TestDir, "dydo/agents/Adele/state.md");
        var stateContent = File.ReadAllText(statePath);
        Assert.Contains("auto-close: false", stateContent);
    }

    [Fact]
    public async Task Release_WithoutAutoCloseMarker_NoAutoCloseMessage()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await ReleaseAgentAsync();
        result.AssertSuccess();

        Assert.DoesNotContain("Auto-close:", result.Stdout);
    }

    private async Task<CommandResult> InboxClearAsync(bool all = false, string? id = null)
    {
        StoreSessionContext();
        var command = InboxCommand.Create();
        var args = new List<string> { "clear" };
        if (all) args.Add("--all");
        if (id != null) { args.Add("--id"); args.Add(id); }
        return await RunAsync(command, args.ToArray());
    }

    #endregion

    #region Task File Auto-Creation

    [Fact]
    public async Task Role_WithTask_AutoCreatesTaskFile()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        await SetRoleAsync("code-writer", "jwt-auth");

        // Verify task file was created with correct frontmatter
        AssertFileExists("dydo/project/tasks/jwt-auth.md");
        AssertFileContains("dydo/project/tasks/jwt-auth.md", "name: jwt-auth");
        AssertFileContains("dydo/project/tasks/jwt-auth.md", "status: pending");
        AssertFileContains("dydo/project/tasks/jwt-auth.md", "assigned: Adele");
    }

    [Fact]
    public async Task Role_WithTask_AutoCreatesTaskFileWithArea()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        await SetRoleAsync("code-writer", "area-test");

        AssertFileContains("dydo/project/tasks/area-test.md", "area: general");
    }

    [Fact]
    public async Task Role_WithTask_ShowsTaskFilePath()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        var result = await SetRoleAsync("code-writer", "jwt-auth");

        result.AssertSuccess();
        result.AssertStdoutContains("Task file:");
    }

    [Fact]
    public async Task Status_WithTask_ShowsTaskFilePath()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "jwt-auth");

        var result = await AgentStatusAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Task file:");
    }

    [Fact]
    public async Task Whoami_WithTask_ShowsTaskFilePath()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "jwt-auth");

        var result = await WhoamiAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Task file:");
    }

    [Fact]
    public async Task Role_WithExistingTask_DoesNotOverwrite()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Pre-create the task file with custom content
        var customContent = "# My custom task\nDo not overwrite!";
        WriteFile("dydo/project/tasks/existing-task.md", customContent);

        await SetRoleAsync("code-writer", "existing-task");

        // Verify original content is preserved
        var content = ReadFile("dydo/project/tasks/existing-task.md");
        Assert.Equal(customContent, content);
    }

    #endregion

    #region Must-Read Enforcement

    [Fact]
    public async Task SetRole_PopulatesUnreadMustReads()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        // Templates already have must-read: true on about.md, architecture.md, coding-standards.md

        await SetRoleAsync("code-writer", "test-task");

        // Verify state has unread must-reads
        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);
        Assert.True(state.UnreadMustReads.Count > 0, "Should have unread must-reads after SetRole");

        // The mode file itself should always be in must-reads
        Assert.Contains(state.UnreadMustReads, p => p.Contains("modes/code-writer.md"));
        // Must-read tagged files should also be present
        Assert.Contains(state.UnreadMustReads, p => p.Contains("about.md"));
    }

    [Fact]
    public async Task SetRole_FiltersAlreadyReadFromAudit()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");

        // Read the mode file via guard (which logs audit)
        var modeFilePath = "dydo/agents/Adele/modes/code-writer.md";
        await GuardAsync("read", modeFilePath);

        // Now set role — the mode file should not be in unread list since we read it
        await SetRoleAsync("code-writer", "test-task");

        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);

        // Mode file should NOT be in unread list (was already read)
        Assert.DoesNotContain(state.UnreadMustReads,
            p => p.Equals(modeFilePath, StringComparison.OrdinalIgnoreCase));

        // But other must-reads should still be there
        Assert.Contains(state.UnreadMustReads, p => p.Contains("about.md"));
    }

    [Fact]
    public async Task Release_ClearsUnreadMustReads()
    {
        await InitProjectAsync("none", "balazs", 3);
        await ClaimAgentAsync("Adele");
        await SetRoleAsync("code-writer", "test-task");

        // Verify must-reads were populated
        var registry = new AgentRegistry(TestDir);
        var state = registry.GetCurrentAgent(TestSessionId);
        Assert.NotNull(state);
        Assert.True(state.UnreadMustReads.Count > 0);

        await ReadMustReadsAsync();
        await ReleaseAgentAsync();

        // After release, state should have empty must-reads
        var stateAfter = registry.GetAgentState("Adele");
        Assert.NotNull(stateAfter);
        Assert.Empty(stateAfter.UnreadMustReads);
    }

    #endregion

    #region Tree

    [Fact]
    public async Task Tree_NoActiveAgents_PrintsMessage()
    {
        await InitProjectAsync("none", "balazs", 3);

        var result = await TreeAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("No active agents.");
    }

    [Fact]
    public async Task Tree_SingleRootAgent()
    {
        await InitProjectAsync("none", "balazs", 3);
        WriteAgentState("Adele", "working", "code-writer", "my-task");

        var result = await TreeAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
        result.AssertStdoutContains("[code-writer]");
        result.AssertStdoutContains("my-task");
    }

    [Fact]
    public async Task Tree_ParentChildRelationship()
    {
        await InitProjectAsync("none", "balazs", 3);
        WriteAgentState("Adele", "working", "orchestrator", "release-task");
        WriteAgentState("Brian", "working", "code-writer", "release-subtask", dispatchedBy: "Adele");

        var result = await TreeAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
        result.AssertStdoutContains("Brian");
        var lines = result.Stdout.Split('\n');
        var adeleLine = Array.FindIndex(lines, l => l.Contains("Adele"));
        var brianLine = Array.FindIndex(lines, l => l.Contains("Brian"));
        Assert.True(brianLine > adeleLine, "Brian should appear after Adele in tree output");
    }

    [Fact]
    public async Task Tree_BranchingTree()
    {
        await InitProjectAsync("none", "balazs", 3);
        WriteAgentState("Adele", "working", "orchestrator", "release");
        WriteAgentState("Brian", "working", "code-writer", "feature-a", dispatchedBy: "Adele");
        WriteAgentState("Charlie", "working", "reviewer", "feature-b", dispatchedBy: "Adele");

        var result = await TreeAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
        result.AssertStdoutContains("Brian");
        result.AssertStdoutContains("Charlie");
    }

    [Fact]
    public async Task Tree_AgentWithWaitMarker()
    {
        await InitProjectAsync("none", "balazs", 3);
        WriteAgentState("Adele", "working", "orchestrator", "my-task");

        var registry = new AgentRegistry(TestDir);
        registry.CreateWaitMarker("Adele", "my-task", "Brian");

        var result = await TreeAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("waiting");
        result.AssertStdoutContains("Brian");
    }

    [Fact]
    public async Task Tree_MultipleRoots()
    {
        await InitProjectAsync("none", "balazs", 3);
        WriteAgentState("Adele", "working", "code-writer", "task-a");
        WriteAgentState("Brian", "working", "reviewer", "task-b");

        var result = await TreeAsync();

        result.AssertSuccess();
        result.AssertStdoutContains("Adele");
        result.AssertStdoutContains("Brian");
        result.AssertStdoutContains("[code-writer]");
        result.AssertStdoutContains("[reviewer]");
    }

    [Fact]
    public async Task Tree_LinearChain()
    {
        await InitProjectAsync("none", "balazs", 3);
        WriteAgentState("Adele", "working", "orchestrator", "release");
        WriteAgentState("Brian", "working", "code-writer", "impl", dispatchedBy: "Adele");
        WriteAgentState("Charlie", "working", "reviewer", "impl-review", dispatchedBy: "Brian");

        var result = await TreeAsync();

        result.AssertSuccess();
        var lines = result.Stdout.Split('\n');
        var adeleLine = Array.FindIndex(lines, l => l.Contains("Adele"));
        var brianLine = Array.FindIndex(lines, l => l.Contains("Brian"));
        var charlieLine = Array.FindIndex(lines, l => l.Contains("Charlie"));
        Assert.True(brianLine > adeleLine, "Brian should appear after Adele");
        Assert.True(charlieLine > brianLine, "Charlie should appear after Brian");
    }

    private async Task<CommandResult> TreeAsync()
    {
        var command = AgentCommand.Create();
        return await RunAsync(command, "tree");
    }

    private void WriteAgentState(string name, string status, string role, string task, string? dispatchedBy = null)
    {
        var statePath = Path.Combine("dydo", "agents", name, "state.md");
        var emptyObj = "{}";
        WriteFile(statePath, $"""
            ---
            agent: {name}
            role: {role}
            task: {task}
            status: {status}
            assigned: balazs
            dispatched-by: {dispatchedBy ?? "null"}
            started: 2026-01-01T00:00:00Z
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {emptyObj}
            ---
            # {name} — Session State
            """);
    }

    #endregion
}
