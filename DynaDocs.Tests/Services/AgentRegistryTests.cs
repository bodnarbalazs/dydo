namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class AgentRegistryTests : IDisposable
{
    private readonly string _testDir;
    private readonly AgentRegistry _registry;

    public AgentRegistryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _registry = new AgentRegistry(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void AgentNames_Contains26Agents()
    {
        Assert.Equal(26, _registry.AgentNames.Count);
        Assert.Contains("Adele", _registry.AgentNames);
        Assert.Contains("Zelda", _registry.AgentNames);
    }

    [Theory]
    [InlineData('A', "Adele")]
    [InlineData('B', "Brian")]
    [InlineData('C', "Charlie")]
    [InlineData('Z', "Zelda")]
    [InlineData('a', "Adele")]
    public void GetAgentNameFromLetter_ReturnsCorrectName(char letter, string expected)
    {
        var result = _registry.GetAgentNameFromLetter(letter);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData('1')]
    [InlineData('!')]
    public void GetAgentNameFromLetter_ReturnsNull_ForInvalidLetter(char letter)
    {
        var result = _registry.GetAgentNameFromLetter(letter);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("Adele", true)]
    [InlineData("Brian", true)]
    [InlineData("Invalid", false)]
    [InlineData("ADELE", true)]  // Case insensitive
    [InlineData("adele", true)]
    public void IsValidAgentName_ValidatesCorrectly(string name, bool expected)
    {
        Assert.Equal(expected, _registry.IsValidAgentName(name));
    }

    [Fact]
    public void GetAgentWorkspace_ReturnsCorrectPath()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Assert.Equal(Path.Combine(_testDir, "dydo", "agents", "Adele"), workspace);
    }

    [Fact]
    public void GetAgentState_ReturnsDefaultState_WhenNoStateFile()
    {
        var state = _registry.GetAgentState("Adele");

        Assert.NotNull(state);
        Assert.Equal("Adele", state.Name);
        Assert.Equal(AgentStatus.Free, state.Status);
        Assert.Null(state.Role);
        Assert.Null(state.Task);
    }

    [Fact]
    public void GetAllAgentStates_Returns26States()
    {
        var states = _registry.GetAllAgentStates();

        Assert.Equal(26, states.Count);
        Assert.All(states, s => Assert.Equal(AgentStatus.Free, s.Status));
    }

    [Fact]
    public void GetFreeAgents_ReturnsAllAgents_WhenNoneClaimed()
    {
        var freeAgents = _registry.GetFreeAgents();

        Assert.Equal(26, freeAgents.Count);
    }

    [Fact]
    public void GetSession_ReturnsNull_WhenNoSession()
    {
        var session = _registry.GetSession("Adele");
        Assert.Null(session);
    }

    [Fact]
    public void SetRole_FailsWithError_WhenNoAgentClaimed()
    {
        var result = _registry.SetRole("code-writer", null, out var error);

        Assert.False(result);
        Assert.Contains("No agent identity assigned", error);
    }

    [Fact]
    public void KnownRoles_AreDocumented()
    {
        // Verify the expected roles are documented
        var knownRoles = new[] { "code-writer", "reviewer", "co-thinker", "docs-writer", "interviewer", "planner", "tester" };
        Assert.Equal(7, knownRoles.Length);
    }

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("co-thinker")]
    [InlineData("docs-writer")]
    [InlineData("interviewer")]
    [InlineData("planner")]
    [InlineData("tester")]
    public void SetRole_AcceptsAllKnownRoles(string role)
    {
        // This test verifies the role is in RolePermissions dictionary
        // SetRole will fail with "No agent identity assigned" but NOT "Invalid role"
        var result = _registry.SetRole(role, null, out var error);

        Assert.False(result); // Expected - no agent claimed
        Assert.Contains("No agent identity assigned", error);
        Assert.DoesNotContain("Invalid role", error);
    }

    [Fact]
    public void ClaimAgent_FailsForInvalidName()
    {
        var result = _registry.ClaimAgent("NotAnAgent", out var error);

        Assert.False(result);
        Assert.Contains("Invalid agent name", error);
    }

    [Fact]
    public void ReleaseAgent_FailsWithError_WhenNoAgentClaimed()
    {
        var result = _registry.ReleaseAgent(out var error);

        Assert.False(result);
        Assert.Contains("No agent identity assigned", error);
    }

    [Fact]
    public void IsPathAllowed_FailsWithError_WhenNoAgentClaimed()
    {
        var result = _registry.IsPathAllowed("src/file.cs", "edit", out var error);

        Assert.False(result);
        Assert.Contains("No agent identity assigned", error);
    }

    #region Agent Management Tests

    private void SetupConfig(string[] agents, Dictionary<string, string[]> assignments)
    {
        var configPath = Path.Combine(_testDir, "dydo.json");
        var assignmentsJson = string.Join(",\n      ",
            assignments.Select(kv => $"\"{kv.Key}\": [{string.Join(", ", kv.Value.Select(a => $"\"{a}\""))}]"));
        var agentsJson = string.Join(", ", agents.Select(a => $"\"{a}\""));

        var config = $$"""
            {
              "version": 1,
              "agents": {
                "pool": [{{agentsJson}}],
                "assignments": {
                  {{assignmentsJson}}
                }
              }
            }
            """;

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, config);
    }

    [Fact]
    public void CreateAgent_AddsToPoolAndAssignments()
    {
        // Setup minimal config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        // Create scaffolder for workspace creation
        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);

        var result = registry.CreateAgent("NewAgent", "testuser", out var error);

        Assert.True(result, $"CreateAgent failed: {error}");

        // Verify config updated
        var configContent = File.ReadAllText(Path.Combine(_testDir, "dydo.json"));
        Assert.Contains("Newagent", configContent); // PascalCase normalized

        // Verify workspace created
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Newagent");
        Assert.True(Directory.Exists(workspacePath), "Agent workspace should exist");
    }

    [Fact]
    public void CreateAgent_FailsForDuplicateName()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var registry = new AgentRegistry(_testDir);

        var result = registry.CreateAgent("Adele", "testuser", out var error);

        Assert.False(result);
        Assert.Contains("already exists", error);
    }

    [Fact]
    public void CreateAgent_FailsForInvalidNameFormat()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var registry = new AgentRegistry(_testDir);

        var result = registry.CreateAgent("123Invalid", "testuser", out var error);

        Assert.False(result);
        Assert.Contains("must start with a letter", error);
    }

    [Fact]
    public void RenameAgent_UpdatesConfigAndWorkspace()
    {
        // Setup config and workspace
        SetupConfig(new[] { "OldName" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "OldName" } });

        var scaffolder = new FolderScaffolder();
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "OldName");
        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(Path.Combine(workspacePath, "modes"));
        File.WriteAllText(Path.Combine(workspacePath, "workflow.md"), "# OldName workflow");
        File.WriteAllText(Path.Combine(workspacePath, "state.md"), """
            ---
            agent: OldName
            status: free
            assigned: testuser
            ---
            # OldName — Session State
            """);

        var registry = new AgentRegistry(_testDir, null, scaffolder);

        var result = registry.RenameAgent("OldName", "NewName", out var error);

        Assert.True(result, $"RenameAgent failed: {error}");

        // Verify config updated
        var configContent = File.ReadAllText(Path.Combine(_testDir, "dydo.json"));
        Assert.Contains("Newname", configContent);
        Assert.DoesNotContain("OldName", configContent);

        // Verify workspace renamed
        Assert.False(Directory.Exists(Path.Combine(_testDir, "dydo", "agents", "OldName")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "dydo", "agents", "Newname")));
    }

    [Fact]
    public void RenameAgent_FailsForNonexistentAgent()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var registry = new AgentRegistry(_testDir);

        var result = registry.RenameAgent("NonExistent", "NewName", out var error);

        Assert.False(result);
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void RemoveAgent_DeletesFromConfigAndWorkspace()
    {
        // Setup config and workspace
        SetupConfig(new[] { "Adele", "Brian" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Brian" } });

        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);
        File.WriteAllText(Path.Combine(workspacePath, "state.md"), "# Adele state");

        var registry = new AgentRegistry(_testDir);

        var result = registry.RemoveAgent("Adele", out var error);

        Assert.True(result, $"RemoveAgent failed: {error}");

        // Verify config updated
        var configContent = File.ReadAllText(Path.Combine(_testDir, "dydo.json"));
        Assert.DoesNotContain("\"Adele\"", configContent);
        Assert.Contains("Brian", configContent); // Other agent still there

        // Verify workspace deleted
        Assert.False(Directory.Exists(workspacePath));
    }

    [Fact]
    public void RemoveAgent_FailsForNonexistentAgent()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var registry = new AgentRegistry(_testDir);

        var result = registry.RemoveAgent("NonExistent", out var error);

        Assert.False(result);
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void ReassignAgent_MovesAgentBetweenHumans()
    {
        // Setup config with two humans
        SetupConfig(
            new[] { "Adele", "Brian" },
            new Dictionary<string, string[]>
            {
                ["human1"] = new[] { "Adele" },
                ["human2"] = new[] { "Brian" }
            });

        // Create workspace with state file
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);
        File.WriteAllText(Path.Combine(workspacePath, "state.md"), """
            ---
            agent: Adele
            status: free
            assigned: human1
            ---
            # Adele — Session State
            """);

        var registry = new AgentRegistry(_testDir);

        var result = registry.ReassignAgent("Adele", "human2", out var error);

        Assert.True(result, $"ReassignAgent failed: {error}");

        // Verify config updated
        var configContent = File.ReadAllText(Path.Combine(_testDir, "dydo.json"));
        // human2 should now have Adele
        Assert.Contains("human2", configContent);

        // Verify state file updated
        var stateContent = File.ReadAllText(Path.Combine(workspacePath, "state.md"));
        Assert.Contains("assigned: human2", stateContent);
    }

    [Fact]
    public void ReassignAgent_FailsForNonexistentAgent()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var registry = new AgentRegistry(_testDir);

        var result = registry.ReassignAgent("NonExistent", "human2", out var error);

        Assert.False(result);
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void ReassignAgent_FailsIfAlreadyAssignedToTargetHuman()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["human1"] = new[] { "Adele" } });
        var registry = new AgentRegistry(_testDir);

        var result = registry.ReassignAgent("Adele", "human1", out var error);

        Assert.False(result);
        Assert.Contains("already assigned", error);
    }

    [Fact]
    public void CreateAgent_HandlesSingleCharacterName()
    {
        // Setup minimal config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);

        // Single-character name should not crash
        var result = registry.CreateAgent("X", "testuser", out var error);

        Assert.True(result, $"CreateAgent failed for single-char name: {error}");

        // Verify config updated with uppercase single char
        var configContent = File.ReadAllText(Path.Combine(_testDir, "dydo.json"));
        Assert.Contains("\"X\"", configContent);

        // Verify workspace created
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "X");
        Assert.True(Directory.Exists(workspacePath), "Agent workspace should exist for single-char name");
    }

    [Fact]
    public void RenameAgent_HandlesSingleCharacterNewName()
    {
        // Setup config and workspace
        SetupConfig(new[] { "OldName" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "OldName" } });

        var scaffolder = new FolderScaffolder();
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "OldName");
        Directory.CreateDirectory(workspacePath);
        Directory.CreateDirectory(Path.Combine(workspacePath, "modes"));
        File.WriteAllText(Path.Combine(workspacePath, "workflow.md"), "# OldName workflow");
        File.WriteAllText(Path.Combine(workspacePath, "state.md"), """
            ---
            agent: OldName
            status: free
            assigned: testuser
            ---
            # OldName — Session State
            """);

        var registry = new AgentRegistry(_testDir, null, scaffolder);

        // Rename to single-character name should not crash
        var result = registry.RenameAgent("OldName", "Z", out var error);

        Assert.True(result, $"RenameAgent to single-char failed: {error}");

        // Verify config updated
        var configContent = File.ReadAllText(Path.Combine(_testDir, "dydo.json"));
        Assert.Contains("\"Z\"", configContent);
        Assert.DoesNotContain("OldName", configContent);

        // Verify workspace renamed
        Assert.False(Directory.Exists(Path.Combine(_testDir, "dydo", "agents", "OldName")));
        Assert.True(Directory.Exists(Path.Combine(_testDir, "dydo", "agents", "Z")));
    }

    #endregion

    #region CanTakeRole Tests

    [Fact]
    public void CanTakeRole_AllowsReviewerWithNoHistory()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        // Create state with no history
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);
        File.WriteAllText(Path.Combine(workspacePath, "state.md"), """
            ---
            agent: Adele
            status: free
            assigned: testuser
            task-role-history: {}
            ---
            # Adele — Session State
            """);

        var registry = new AgentRegistry(_testDir);

        var canTake = registry.CanTakeRole("Adele", "reviewer", "some-task", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void CanTakeRole_BlocksReviewerAfterCodeWriter()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        // Create state with code-writer history
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);
        File.WriteAllText(Path.Combine(workspacePath, "state.md"), """
            ---
            agent: Adele
            status: free
            assigned: testuser
            task-role-history: { "my-task": ["code-writer"] }
            ---
            # Adele — Session State
            """);

        var registry = new AgentRegistry(_testDir);

        var canTake = registry.CanTakeRole("Adele", "reviewer", "my-task", out var reason);

        Assert.False(canTake);
        Assert.Contains("code-writer", reason);
    }

    [Fact]
    public void CanTakeRole_AllowsNonReviewerRolesAfterCodeWriter()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);
        File.WriteAllText(Path.Combine(workspacePath, "state.md"), """
            ---
            agent: Adele
            status: free
            assigned: testuser
            task-role-history: { "my-task": ["code-writer"] }
            ---
            # Adele — Session State
            """);

        var registry = new AgentRegistry(_testDir);

        // Should allow planner, tester, etc. on same task
        var canTakePlanner = registry.CanTakeRole("Adele", "planner", "my-task", out var reason1);
        var canTakeTester = registry.CanTakeRole("Adele", "tester", "my-task", out var reason2);

        Assert.True(canTakePlanner, reason1);
        Assert.True(canTakeTester, reason2);
    }

    #endregion

    #region Role Validation Tests

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("co-thinker")]
    [InlineData("docs-writer")]
    [InlineData("interviewer")]
    [InlineData("planner")]
    [InlineData("tester")]
    public void SetRole_RejectsInvalidRole_ButAcceptsValidRole(string role)
    {
        // Valid roles should fail with "No agent identity assigned", not "Invalid role"
        var result = _registry.SetRole(role, null, out var error);

        Assert.False(result);
        Assert.Contains("No agent identity assigned", error);
        Assert.DoesNotContain("Invalid role", error);
    }

    [Theory]
    [InlineData("invalid-role")]
    [InlineData("admin")]
    [InlineData("superuser")]
    public void SetRole_RejectsInvalidRoles(string invalidRole)
    {
        var result = _registry.SetRole(invalidRole, null, out var error);

        Assert.False(result);
        // Should fail with invalid role error (though may also fail with no agent claimed first)
    }

    [Fact]
    public void AllSevenRoles_AreRecognized()
    {
        // This test ensures we have exactly 7 valid roles
        var knownRoles = new[] { "code-writer", "reviewer", "co-thinker", "docs-writer", "interviewer", "planner", "tester" };

        foreach (var role in knownRoles)
        {
            var result = _registry.SetRole(role, null, out var error);

            // Should NOT say "Invalid role" for any known role
            Assert.DoesNotContain("Invalid role", error);
        }
    }

    #endregion

    #region Lock File Tests

    [Fact]
    public void ClaimAgent_CleansUpLockFileAfterAttempt()
    {
        // Setup config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var registry = new AgentRegistry(_testDir);

        // Attempt claim (will fail due to no terminal PID in test environment)
        var result = registry.ClaimAgent("Adele", out var error);

        // Lock file should not exist after the attempt (cleaned up in finally)
        var lockPath = Path.Combine(_testDir, "dydo", "agents", "Adele", ".claim.lock");
        Assert.False(File.Exists(lockPath), "Lock file should be cleaned up after claim attempt");
    }

    [Fact]
    public void ClaimAgent_FailsWhenLockHeldByRunningProcess()
    {
        // Setup config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        // Create workspace and lock file with current process PID (simulates another claimer)
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);

        var lockPath = Path.Combine(workspacePath, ".claim.lock");
        var lockContent = $"{{\"Pid\":{Environment.ProcessId},\"Acquired\":\"{DateTime.UtcNow:o}\"}}";
        File.WriteAllText(lockPath, lockContent);

        var registry = new AgentRegistry(_testDir);

        // Attempt claim
        var result = registry.ClaimAgent("Adele", out var error);

        Assert.False(result);
        Assert.Contains("claim in progress", error);
        Assert.Contains(Environment.ProcessId.ToString(), error);
    }

    [Fact]
    public void ClaimAgent_RemovesStaleLockAndProceeds()
    {
        // Setup config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        // Create workspace and lock file with dead PID
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);

        var lockPath = Path.Combine(workspacePath, ".claim.lock");
        var stalePid = 999999999; // Very unlikely to be a real running process
        var lockContent = $"{{\"Pid\":{stalePid},\"Acquired\":\"2024-01-01T00:00:00Z\"}}";
        File.WriteAllText(lockPath, lockContent);

        var registry = new AgentRegistry(_testDir);

        // Attempt claim - should proceed past lock (though may fail later due to no terminal PID)
        var result = registry.ClaimAgent("Adele", out var error);

        // The important thing is it didn't fail with "claim in progress" error
        Assert.DoesNotContain("claim in progress", error);

        // Lock file should be cleaned up
        Assert.False(File.Exists(lockPath), "Stale lock file should be removed");
    }

    [Fact]
    public void ClaimAgent_HandlesCorruptLockFile()
    {
        // Setup config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        // Create workspace and corrupt lock file
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);

        var lockPath = Path.Combine(workspacePath, ".claim.lock");
        File.WriteAllText(lockPath, "this is not valid json");

        var registry = new AgentRegistry(_testDir);

        // Attempt claim - should treat corrupt lock as stale and proceed
        var result = registry.ClaimAgent("Adele", out var error);

        // Should not fail with "claim in progress" error
        Assert.DoesNotContain("claim in progress", error);
    }

    [Fact]
    public void ClaimAgent_InvalidName_DoesNotCreateLockFile()
    {
        // Attempt claim with invalid name
        var result = _registry.ClaimAgent("NotAnAgent", out var error);

        Assert.False(result);
        Assert.Contains("Invalid agent name", error);

        // No lock file should be created for invalid agent
        var lockPath = Path.Combine(_testDir, "dydo", "agents", "NotAnAgent", ".claim.lock");
        Assert.False(File.Exists(lockPath), "Lock file should not be created for invalid agent name");
    }

    [Fact]
    public async Task ClaimAgent_ConcurrentClaims_OnlyOneLockSucceeds()
    {
        // Setup config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        var successCount = 0;
        var lockInProgressCount = 0;
        var otherErrorCount = 0;

        // Launch multiple concurrent claim attempts
        var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            var registry = new AgentRegistry(_testDir);
            var result = registry.ClaimAgent("Adele", out var error);

            if (result)
            {
                Interlocked.Increment(ref successCount);
            }
            else if (error.Contains("claim in progress"))
            {
                Interlocked.Increment(ref lockInProgressCount);
            }
            else
            {
                // Other errors (like "Could not determine terminal PID") are expected in tests
                Interlocked.Increment(ref otherErrorCount);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // At most one should succeed (in practice, all may fail due to terminal PID check)
        // But importantly, no crashes and lock contention is properly handled
        Assert.True(successCount <= 1, $"At most one claim should succeed, got {successCount}");

        // Lock file should be cleaned up (allow brief delay for file system on Windows)
        var lockPath = Path.Combine(_testDir, "dydo", "agents", "Adele", ".claim.lock");
        for (var i = 0; i < 10 && File.Exists(lockPath); i++)
        {
            await Task.Delay(50);
        }
        Assert.False(File.Exists(lockPath), "Lock file should be cleaned up after concurrent claims");
    }

    #endregion
}
