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
        var result = _registry.SetRole(null, "code-writer", null, out var error);

        Assert.False(result);
        Assert.Contains("No agent identity assigned", error);
    }

    [Fact]
    public void KnownRoles_AreDocumented()
    {
        // Verify the expected roles are documented
        var knownRoles = new[] { "code-writer", "reviewer", "co-thinker", "docs-writer", "planner", "test-writer" };
        Assert.Equal(6, knownRoles.Length);
    }

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("co-thinker")]
    [InlineData("docs-writer")]
    [InlineData("planner")]
    [InlineData("test-writer")]
    public void SetRole_AcceptsAllKnownRoles(string role)
    {
        // This test verifies the role is in RolePermissions dictionary
        // SetRole will fail with "No agent identity assigned" but NOT "Invalid role"
        var result = _registry.SetRole(null, role, null, out var error);

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
    public void ClaimAgent_FailsWithoutPendingSession()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var registry = new AgentRegistry(_testDir);

        var result = registry.ClaimAgent("Adele", out var error);

        Assert.False(result);
        Assert.Contains("session ID", error, StringComparison.OrdinalIgnoreCase);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_SucceedsWithPendingSession()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);

        // Store pending session (simulates guard interception)
        registry.StorePendingSessionId("Adele", "test-session-123");

        var result = registry.ClaimAgent("Adele", out var error);

        Assert.True(result, $"ClaimAgent failed: {error}");

        // Verify session file created with session_id
        var session = registry.GetSession("Adele");
        Assert.NotNull(session);
        Assert.Equal("test-session-123", session.SessionId);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void GetCurrentAgent_ReturnsNull_WhenNoSessionId()
    {
        Assert.Null(_registry.GetCurrentAgent(null));
        Assert.Null(_registry.GetCurrentAgent(""));
    }

    [Fact]
    public void GetCurrentAgent_FindsAgent_WithMatchingSessionId()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-456");

        var registry = new AgentRegistry(_testDir);
        var result = registry.GetCurrentAgent("test-session-456");

        Assert.NotNull(result);
        Assert.Equal("Adele", result.Name);
    }

    [Fact]
    public void GetCurrentAgent_ReturnsNull_WhenSessionIdDoesNotMatch()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "session-aaa");

        var registry = new AgentRegistry(_testDir);
        Assert.Null(registry.GetCurrentAgent("session-bbb"));
    }

    private void CreateSessionFile(string agentName, string sessionId)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);

        var sessionJson = $$"""
            {"Agent":"{{agentName}}","SessionId":"{{sessionId}}","Claimed":"{{DateTime.UtcNow:o}}"}
            """;
        File.WriteAllText(Path.Combine(workspace, ".session"), sessionJson);

        // Also create state file
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: {{agentName}}
            status: working
            assigned: testuser
            ---
            """);
    }

    [Fact]
    public void ReleaseAgent_FailsWithError_WhenNoAgentClaimed()
    {
        var result = _registry.ReleaseAgent(null, out var error);

        Assert.False(result);
        Assert.Contains("No agent identity assigned", error);
    }

    [Fact]
    public void IsPathAllowed_FailsWithError_WhenNoAgentClaimed()
    {
        var result = _registry.IsPathAllowed(null, "src/file.cs", "edit", out var error);

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

        // Should allow planner, test-writer, etc. on same task
        var canTakePlanner = registry.CanTakeRole("Adele", "planner", "my-task", out var reason1);
        var canTakeTester = registry.CanTakeRole("Adele", "test-writer", "my-task", out var reason2);

        Assert.True(canTakePlanner, reason1);
        Assert.True(canTakeTester, reason2);
    }

    [Fact]
    public void CanTakeRole_BlocksOrchestratorWithoutPlannerHistory()
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

        var canTake = registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.False(canTake);
        Assert.Contains("Orchestrator requires prior co-thinker or planner experience", reason);
    }

    [Fact]
    public void CanTakeRole_AllowsOrchestratorWithPlannerHistory()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);
        File.WriteAllText(Path.Combine(workspacePath, "state.md"), """
            ---
            agent: Adele
            status: free
            assigned: testuser
            task-role-history: { "my-task": ["planner"] }
            ---
            # Adele — Session State
            """);

        var registry = new AgentRegistry(_testDir);

        var canTake = registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    [Fact]
    public void CanTakeRole_BlocksJudgeWhenThreeAlreadyActive()
    {
        SetupConfig(
            new[] { "Adele", "Brian", "Claire", "David" },
            new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Brian", "Claire", "David" } });

        // Create 3 active judges on the same task
        foreach (var name in new[] { "Adele", "Brian", "Claire" })
        {
            var workspace = Path.Combine(_testDir, "dydo", "agents", name);
            Directory.CreateDirectory(workspace);
            File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
                ---
                agent: {{name}}
                status: working
                assigned: testuser
                role: judge
                task: dispute-1
                task-role-history: {}
                ---
                # {{name}} — Session State
                """);
        }

        // David wants to become the 4th judge
        var davidWorkspace = Path.Combine(_testDir, "dydo", "agents", "David");
        Directory.CreateDirectory(davidWorkspace);
        File.WriteAllText(Path.Combine(davidWorkspace, "state.md"), """
            ---
            agent: David
            status: free
            assigned: testuser
            task-role-history: {}
            ---
            # David — Session State
            """);

        var registry = new AgentRegistry(_testDir);

        var canTake = registry.CanTakeRole("David", "judge", "dispute-1", out var reason);

        Assert.False(canTake);
        Assert.Contains("Maximum 3 judges", reason);
    }

    [Fact]
    public void CanTakeRole_AllowsJudgeWhenFewerThanThreeActive()
    {
        SetupConfig(
            new[] { "Adele", "Brian", "Claire" },
            new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Brian", "Claire" } });

        // Only 1 active judge
        var adeleWorkspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(adeleWorkspace);
        File.WriteAllText(Path.Combine(adeleWorkspace, "state.md"), """
            ---
            agent: Adele
            status: working
            assigned: testuser
            role: judge
            task: dispute-1
            task-role-history: {}
            ---
            # Adele — Session State
            """);

        var clairePath = Path.Combine(_testDir, "dydo", "agents", "Claire");
        Directory.CreateDirectory(clairePath);
        File.WriteAllText(Path.Combine(clairePath, "state.md"), """
            ---
            agent: Claire
            status: free
            assigned: testuser
            task-role-history: {}
            ---
            # Claire — Session State
            """);

        var registry = new AgentRegistry(_testDir);

        var canTake = registry.CanTakeRole("Claire", "judge", "dispute-1", out var reason);

        Assert.True(canTake);
        Assert.Empty(reason);
    }

    #endregion

    #region Task File Auto-Creation Tests

    [Fact]
    public void SetRole_WithTask_CreatesTaskFile()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-task");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-task", "code-writer", "jwt-auth", out var error);

        Assert.True(result, $"SetRole failed: {error}");

        // Verify task file was created
        var taskFilePath = Path.Combine(_testDir, "dydo", "project", "tasks", "jwt-auth.md");
        Assert.True(File.Exists(taskFilePath), "Task file should be created");

        var content = File.ReadAllText(taskFilePath);
        Assert.Contains("name: jwt-auth", content);
        Assert.Contains("status: pending", content);
        Assert.Contains("assigned: Adele", content);
        Assert.Contains("# Task: jwt-auth", content);
        Assert.Contains("(No description)", content);
    }

    [Fact]
    public void SetRole_WithTask_DoesNotOverwriteExistingTaskFile()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-task2");

        // Pre-create the task file with custom content
        var tasksDir = Path.Combine(_testDir, "dydo", "project", "tasks");
        Directory.CreateDirectory(tasksDir);
        var taskFilePath = Path.Combine(tasksDir, "existing-task.md");
        var originalContent = "# My custom task content\nDo not overwrite me!";
        File.WriteAllText(taskFilePath, originalContent);

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-task2", "code-writer", "existing-task", out var error);

        Assert.True(result, $"SetRole failed: {error}");

        // Verify original content is preserved
        var content = File.ReadAllText(taskFilePath);
        Assert.Equal(originalContent, content);
    }

    [Fact]
    public void SetRole_WithoutTask_DoesNotCreateTaskFile()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-notask");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-notask", "code-writer", null, out var error);

        Assert.True(result, $"SetRole failed: {error}");

        // Verify tasks directory was not created
        var tasksDir = Path.Combine(_testDir, "dydo", "project", "tasks");
        Assert.False(Directory.Exists(tasksDir), "Tasks directory should not be created when no task is specified");
    }

    [Fact]
    public void SetRole_WithTask_SucceedsEvenWhenTaskFileCreationFails()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-badfn");

        var registry = new AgentRegistry(_testDir);

        // Use invalid filename characters so File.WriteAllText throws
        var result = registry.SetRole("test-session-badfn", "code-writer", "bad:task<>name", out var error);

        // SetRole should still succeed — task file creation is non-blocking
        Assert.True(result, $"SetRole should succeed even when task file creation fails: {error}");

        // Verify the role was actually set
        var state = registry.GetAgentState("Adele");
        Assert.Equal("code-writer", state?.Role);
        Assert.Equal("bad:task<>name", state?.Task);
    }

    #endregion

    #region Role Validation Tests

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("co-thinker")]
    [InlineData("docs-writer")]
    [InlineData("planner")]
    [InlineData("test-writer")]
    public void SetRole_RejectsInvalidRole_ButAcceptsValidRole(string role)
    {
        // Valid roles should fail with "No agent identity assigned", not "Invalid role"
        var result = _registry.SetRole(null, role, null, out var error);

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
        var result = _registry.SetRole(null, invalidRole, null, out var error);

        Assert.False(result);
        // Should fail with invalid role error (though may also fail with no agent claimed first)
    }

    [Fact]
    public void AllSixRoles_AreRecognized()
    {
        // This test ensures we have exactly 6 valid roles
        var knownRoles = new[] { "code-writer", "reviewer", "co-thinker", "docs-writer", "planner", "test-writer" };

        foreach (var role in knownRoles)
        {
            var result = _registry.SetRole(null, role, null, out var error);

            // Should NOT say "Invalid role" for any known role
            Assert.DoesNotContain("Invalid role", error);
        }
    }

    #endregion

    #region Dispatch Role Guardrail Tests

    private void CreateInboxItem(string agentName, string task, string role, string from = "Brian")
    {
        var inboxPath = Path.Combine(_testDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);
        var sanitizedTask = task.Replace(':', '-').Replace('<', '-').Replace('>', '-');
        File.WriteAllText(Path.Combine(inboxPath, $"abcd1234-{sanitizedTask}.md"), $"""
            ---
            id: abcd1234
            from: {from}
            role: {role}
            task: {task}
            received: 2026-01-01T00:00:00Z
            origin: {from}
            ---

            # {role.ToUpperInvariant()} Request: {task}
            """);
    }

    [Fact]
    public void SetRole_WithDifferentInboxRole_FailsOnFirstAttempt()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge1");
        CreateInboxItem("Adele", "my-task", "reviewer");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-nudge1", "co-thinker", "my-task", out var error);

        Assert.False(result);
        Assert.Contains("dispatched as", error);
        Assert.Contains("reviewer", error);
    }

    [Fact]
    public void SetRole_WithDifferentInboxRole_SucceedsOnRetry()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge2");
        CreateInboxItem("Adele", "my-task", "reviewer");

        var registry = new AgentRegistry(_testDir);

        // First attempt — fails with nudge
        registry.SetRole("test-session-nudge2", "co-thinker", "my-task", out _);

        // Second attempt — succeeds
        var result = registry.SetRole("test-session-nudge2", "co-thinker", "my-task", out var error);

        Assert.True(result, $"SetRole should succeed on retry: {error}");
    }

    [Fact]
    public void SetRole_WithMatchingInboxRole_Succeeds()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge3");
        CreateInboxItem("Adele", "my-task", "code-writer");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-nudge3", "code-writer", "my-task", out var error);

        Assert.True(result, $"SetRole should succeed when roles match: {error}");
    }

    [Fact]
    public void SetRole_WithNoInbox_Succeeds()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge4");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-nudge4", "code-writer", "my-task", out var error);

        Assert.True(result, $"SetRole should succeed with no inbox: {error}");
    }

    [Fact]
    public void SetRole_WithInboxButNoTask_Succeeds()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge5");
        CreateInboxItem("Adele", "my-task", "reviewer");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-nudge5", "code-writer", null, out var error);

        Assert.True(result, $"SetRole should succeed when task is null: {error}");
    }

    [Fact]
    public void SetRole_CaseInsensitiveRoleComparison()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge6");
        CreateInboxItem("Adele", "my-task", "Reviewer");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-nudge6", "reviewer", "my-task", out var error);

        Assert.True(result, $"SetRole should succeed with case-insensitive match: {error}");
    }

    [Fact]
    public void SetRole_InboxForDifferentTask_NoNudge()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge7");
        CreateInboxItem("Adele", "other-task", "reviewer");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-nudge7", "co-thinker", "my-task", out var error);

        Assert.True(result, $"SetRole should succeed when inbox is for different task: {error}");
    }

    [Fact]
    public void SetRole_WithMalformedInboxFile_Succeeds()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge8");

        // Create inbox file without YAML frontmatter
        var inboxPath = Path.Combine(_testDir, "dydo", "agents", "Adele", "inbox");
        Directory.CreateDirectory(inboxPath);
        File.WriteAllText(Path.Combine(inboxPath, "abcd1234-my-task.md"), "No frontmatter here");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-nudge8", "co-thinker", "my-task", out var error);

        Assert.True(result, $"SetRole should succeed with malformed inbox: {error}");
    }

    [Fact]
    public void SetRole_WithInboxMissingRoleField_Succeeds()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge9");

        var inboxPath = Path.Combine(_testDir, "dydo", "agents", "Adele", "inbox");
        Directory.CreateDirectory(inboxPath);
        File.WriteAllText(Path.Combine(inboxPath, "abcd1234-my-task.md"), """
            ---
            id: abcd1234
            from: Brian
            task: my-task
            ---

            # Request
            """);

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-nudge9", "co-thinker", "my-task", out var error);

        Assert.True(result, $"SetRole should succeed when inbox has no role field: {error}");
    }

    [Fact]
    public void SetRole_RoleMismatchNudge_MustFail_NotSucceedWithWarning()
    {
        // Guards against the anti-pattern of returning success + warning side-channel.
        // Role mismatch on first attempt MUST be a hard failure (return false, non-empty error).
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge10");
        CreateInboxItem("Adele", "my-task", "reviewer");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-nudge10", "co-thinker", "my-task", out var error);

        Assert.False(result);
        Assert.False(string.IsNullOrEmpty(error), "Error must be non-empty on role mismatch failure");

        // Verify the role was NOT applied
        var state = registry.GetAgentState("Adele");
        Assert.NotEqual("co-thinker", state?.Role);
    }

    [Fact]
    public void SetRole_NudgeDoesNotBlockMatchingRole()
    {
        // After a nudge failure, setting the dispatched role should work without retry
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge11");
        CreateInboxItem("Adele", "my-task", "reviewer");

        var registry = new AgentRegistry(_testDir);

        // First: try wrong role — fails
        registry.SetRole("test-session-nudge11", "co-thinker", "my-task", out _);

        // Then: try the dispatched role — should succeed immediately
        var result = registry.SetRole("test-session-nudge11", "reviewer", "my-task", out var error);

        Assert.True(result, $"Setting the dispatched role should always succeed: {error}");

        // Stale marker should be cleaned up
        var markerPath = Path.Combine(_testDir, "dydo", "agents", "Adele", ".role-nudge-my-task");
        Assert.False(File.Exists(markerPath), "Stale nudge marker should be deleted when matching role is set");
    }

    [Fact]
    public void ReleaseAgent_CleansUpNudgeMarkers()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge12");
        CreateInboxItem("Adele", "my-task", "reviewer");

        var registry = new AgentRegistry(_testDir);

        // Trigger nudge to create marker
        registry.SetRole("test-session-nudge12", "co-thinker", "my-task", out _);

        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        var markerPath = Path.Combine(workspace, ".role-nudge-my-task");
        Assert.True(File.Exists(markerPath), "Marker should exist after nudge");

        // Clear inbox so release doesn't block
        Directory.Delete(Path.Combine(workspace, "inbox"), true);

        registry.ReleaseAgent("test-session-nudge12", out var error);

        Assert.False(File.Exists(markerPath), $"Nudge marker should be deleted on release: {error}");
    }

    [Fact]
    public void SetRole_StaleMarkerDoesNotBypassNudge_AfterRelease()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge13");
        CreateInboxItem("Adele", "my-task", "reviewer");

        var registry = new AgentRegistry(_testDir);

        // Trigger nudge to create marker
        registry.SetRole("test-session-nudge13", "co-thinker", "my-task", out _);

        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");

        // Clear inbox and release
        Directory.Delete(Path.Combine(workspace, "inbox"), true);
        registry.ReleaseAgent("test-session-nudge13", out _);

        // Re-claim and re-dispatch
        CreateSessionFile("Adele", "test-session-nudge13b");
        CreateInboxItem("Adele", "my-task", "reviewer");
        registry = new AgentRegistry(_testDir);

        // Should nudge again — marker was cleaned on release
        var result = registry.SetRole("test-session-nudge13b", "co-thinker", "my-task", out var error);

        Assert.False(result, "Stale marker should not bypass nudge after release");
        Assert.Contains("dispatched as", error);
    }

    [Fact]
    public void SetRole_SkipsNudge_WhenAgentAlreadyFulfilledDispatchedRole()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-nudge14");
        CreateInboxItem("Adele", "my-task", "reviewer");

        var registry = new AgentRegistry(_testDir);

        // First: claim the dispatched role — succeeds
        var result1 = registry.SetRole("test-session-nudge14", "reviewer", "my-task", out var err1);
        Assert.True(result1, $"Setting dispatched role should succeed: {err1}");

        // Now switch to a different role — should succeed without nudge
        var result2 = registry.SetRole("test-session-nudge14", "code-writer", "my-task", out var err2);
        Assert.True(result2, $"Switching after fulfilling dispatched role should succeed without nudge: {err2}");

        // Switch again — should still succeed (TaskRoleHistory persists, not just current role)
        var result3 = registry.SetRole("test-session-nudge14", "planner", "my-task", out var err3);
        Assert.True(result3, $"Second switch after fulfilling dispatched role should also succeed: {err3}");
    }

    [Fact]
    public void IsPathAllowed_NudgesToPlannerMode_WhenWritingToClaudePlans()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-plans");

        var registry = new AgentRegistry(_testDir);
        registry.SetRole("test-session-plans", "reviewer", "some-task", out _);

        var result = registry.IsPathAllowed("test-session-plans", ".claude/plans/toasty-stirring-allen.md", "write", out var error);

        Assert.False(result);
        Assert.Contains("planner mode", error);
        Assert.Contains("workspace", error);
        Assert.DoesNotContain("Reviewer role can only edit own workspace", error);
    }

    #endregion

    #region Lock File Tests

    [Fact]
    public void ClaimAgent_FallsBackToSessionContext_WhenNoPendingSession()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);

        // Store session context (but NOT pending session) — simulates re-claim after release
        registry.StoreSessionContext("ctx-session-456");

        var result = registry.ClaimAgent("Adele", out var error);

        Assert.True(result, $"ClaimAgent should fall back to session context: {error}");

        var session = registry.GetSession("Adele");
        Assert.NotNull(session);
        Assert.Equal("ctx-session-456", session.SessionId);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_CleansUpLockFileAfterAttempt()
    {
        // Setup config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var registry = new AgentRegistry(_testDir);

        // Attempt claim (will fail due to no pending session)
        var result = registry.ClaimAgent("Adele", out var error);

        // Lock file should not exist after the attempt (cleaned up in finally)
        var lockPath = Path.Combine(_testDir, "dydo", "agents", "Adele", ".claim.lock");
        Assert.False(File.Exists(lockPath), "Lock file should be cleaned up after claim attempt");
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_FailsWhenLockHeldByRunningProcess()
    {
        // Setup config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");

        // Create workspace and lock file with current process PID (simulates another claimer)
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);

        var lockPath = Path.Combine(workspacePath, ".claim.lock");
        var lockContent = $"{{\"Pid\":{Environment.ProcessId},\"Acquired\":\"{DateTime.UtcNow:o}\"}}";
        File.WriteAllText(lockPath, lockContent);

        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);

        // Store pending session
        registry.StorePendingSessionId("Adele", "test-session");

        // Attempt claim
        var result = registry.ClaimAgent("Adele", out var error);

        Assert.False(result);
        Assert.Contains("claim in progress", error);
        Assert.Contains(Environment.ProcessId.ToString(), error);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_RemovesStaleLockAndProceeds()
    {
        // Setup config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");

        // Create workspace and lock file with dead PID
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);

        var lockPath = Path.Combine(workspacePath, ".claim.lock");
        var stalePid = 999999999; // Very unlikely to be a real running process
        var lockContent = $"{{\"Pid\":{stalePid},\"Acquired\":\"2024-01-01T00:00:00Z\"}}";
        File.WriteAllText(lockPath, lockContent);

        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);

        // Store pending session
        registry.StorePendingSessionId("Adele", "test-session");

        // Attempt claim - should proceed past lock and succeed
        var result = registry.ClaimAgent("Adele", out var error);

        // Should succeed since we have pending session and stale lock is removed
        Assert.True(result, $"ClaimAgent failed: {error}");

        // Lock file should be cleaned up
        Assert.False(File.Exists(lockPath), "Stale lock file should be removed");
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_HandlesCorruptLockFile()
    {
        // Setup config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");

        // Create workspace and corrupt lock file
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);

        var lockPath = Path.Combine(workspacePath, ".claim.lock");
        File.WriteAllText(lockPath, "this is not valid json");

        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);

        // Store pending session
        registry.StorePendingSessionId("Adele", "test-session");

        // Attempt claim - should treat corrupt lock as stale and proceed
        var result = registry.ClaimAgent("Adele", out var error);

        // Should succeed since corrupt lock is treated as stale
        Assert.True(result, $"ClaimAgent failed: {error}");
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
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
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");

        var successCount = 0;
        var lockInProgressCount = 0;
        var otherErrorCount = 0;

        // Pre-create workspace to avoid directory creation race
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);

        // Store a single pending session (in real usage, guard runs before each claim)
        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);
        registry.StorePendingSessionId("Adele", "shared-session");

        // Launch multiple concurrent claim attempts (simulates multiple processes trying to claim)
        var tasks = Enumerable.Range(0, 5).Select(i => Task.Run(() =>
        {
            var taskRegistry = new AgentRegistry(_testDir, null, scaffolder);

            // Note: All tasks use same pending session (race condition on who reads it first)
            var result = taskRegistry.ClaimAgent("Adele", out var error);

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
                Interlocked.Increment(ref otherErrorCount);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // At most one should succeed (the one that reads pending session first and acquires lock)
        Assert.True(successCount <= 1, $"At most one claim should succeed, got {successCount}");

        // Lock file should be cleaned up (allow brief delay for file system on Windows)
        var lockPath = Path.Combine(_testDir, "dydo", "agents", "Adele", ".claim.lock");
        for (var i = 0; i < 10 && File.Exists(lockPath); i++)
        {
            await Task.Delay(50);
        }
        Assert.False(File.Exists(lockPath), "Lock file should be cleaned up after concurrent claims");
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    #endregion

    #region Must-Read State Persistence

    [Fact]
    public void ParseStateFile_ParsesUnreadMustReads()
    {
        // Write a state file with unread-must-reads
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);
        var statePath = Path.Combine(workspace, "state.md");
        File.WriteAllText(statePath, """
            ---
            agent: Adele
            role: code-writer
            task: test-task
            status: working
            assigned: testuser
            started: null
            writable-paths: ["src/**"]
            readonly-paths: ["dydo/**"]
            unread-must-reads: ["dydo/understand/about.md", "dydo/understand/architecture.md"]
            task-role-history: {}
            ---

            # Adele — Session State
            """);

        var state = _registry.GetAgentState("Adele");

        Assert.NotNull(state);
        Assert.Equal(2, state.UnreadMustReads.Count);
        Assert.Contains("dydo/understand/about.md", state.UnreadMustReads);
        Assert.Contains("dydo/understand/architecture.md", state.UnreadMustReads);
    }

    [Fact]
    public void WriteStateFile_PersistsUnreadMustReads()
    {
        // Test round-trip: MarkMustReadComplete triggers WriteStateFile, then verify output
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        // Create session file
        var sessionPath = Path.Combine(workspace, ".session");
        File.WriteAllText(sessionPath, """{"Agent":"Adele","SessionId":"test-session","Claimed":"2025-01-01T00:00:00Z"}""");

        // Create initial state with must-reads
        var statePath = Path.Combine(workspace, "state.md");
        File.WriteAllText(statePath, """
            ---
            agent: Adele
            role: code-writer
            task: null
            status: working
            assigned: testuser
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: ["dydo/understand/about.md", "dydo/guides/coding-standards.md"]
            task-role-history: {}
            ---

            # Adele — Session State
            """);

        // Trigger WriteStateFile by marking one must-read complete
        _registry.MarkMustReadComplete("test-session", "dydo/understand/about.md");

        // Verify the written file contains the updated must-reads YAML
        var writtenContent = File.ReadAllText(statePath);
        Assert.Contains("unread-must-reads:", writtenContent);
        Assert.Contains("coding-standards.md", writtenContent);
        Assert.DoesNotContain("about.md", writtenContent);
    }

    [Fact]
    public void ParseStateFile_EmptyUnreadMustReads_ParsesAsEmptyList()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);
        var statePath = Path.Combine(workspace, "state.md");
        File.WriteAllText(statePath, """
            ---
            agent: Adele
            role: code-writer
            task: null
            status: working
            assigned: testuser
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            task-role-history: {}
            ---

            # Adele — Session State
            """);

        var state = _registry.GetAgentState("Adele");

        Assert.NotNull(state);
        Assert.Empty(state.UnreadMustReads);
    }

    [Fact]
    public void MarkMustReadComplete_RemovesFromList()
    {
        // Set up agent with must-reads
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        // Create session file
        var sessionPath = Path.Combine(workspace, ".session");
        File.WriteAllText(sessionPath, """{"Agent":"Adele","SessionId":"test-session","Claimed":"2025-01-01T00:00:00Z"}""");

        // Create state file with must-reads
        var statePath = Path.Combine(workspace, "state.md");
        File.WriteAllText(statePath, """
            ---
            agent: Adele
            role: code-writer
            task: null
            status: working
            assigned: testuser
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: ["dydo/understand/about.md", "dydo/understand/architecture.md"]
            task-role-history: {}
            ---

            # Adele — Session State
            """);

        // Mark one as read
        _registry.MarkMustReadComplete("test-session", "dydo/understand/about.md");

        // Verify it was removed
        var state = _registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Single(state.UnreadMustReads);
        Assert.Contains("dydo/understand/architecture.md", state.UnreadMustReads);
    }

    [Fact]
    public void MarkMustReadComplete_CaseInsensitive()
    {
        // Set up agent with must-reads
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        // Create session file
        var sessionPath = Path.Combine(workspace, ".session");
        File.WriteAllText(sessionPath, """{"Agent":"Adele","SessionId":"test-session","Claimed":"2025-01-01T00:00:00Z"}""");

        // Create state file with must-reads
        var statePath = Path.Combine(workspace, "state.md");
        File.WriteAllText(statePath, """
            ---
            agent: Adele
            role: code-writer
            task: null
            status: working
            assigned: testuser
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: ["dydo/understand/About.md"]
            task-role-history: {}
            ---

            # Adele — Session State
            """);

        // Mark with different case
        _registry.MarkMustReadComplete("test-session", "dydo/understand/about.md");

        // Verify it was removed despite case difference
        var state = _registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Empty(state.UnreadMustReads);
    }

    #endregion

    #region ReserveAgent Tests

    [Fact]
    public void ReserveAgent_FreeAgent_SetsDispatched()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var registry = new AgentRegistry(_testDir);

        var result = registry.ReserveAgent("Adele", out var error);

        Assert.True(result, $"ReserveAgent failed: {error}");

        var state = registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Equal(AgentStatus.Dispatched, state.Status);
        Assert.NotNull(state.Since);
    }

    [Fact]
    public void ReserveAgent_AlreadyDispatched_Fails()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var registry = new AgentRegistry(_testDir);

        // First reservation succeeds
        var result1 = registry.ReserveAgent("Adele", out var error1);
        Assert.True(result1, $"First ReserveAgent failed: {error1}");

        // Second reservation fails (freshly dispatched, not stale)
        var result2 = registry.ReserveAgent("Adele", out var error2);
        Assert.False(result2);
        Assert.Contains("not free", error2);
    }

    [Fact]
    public void ReserveAgent_StaleDispatch_Succeeds()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        // Write a dispatched state with old timestamp (stale)
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        var staleTime = DateTime.UtcNow.AddMinutes(-5);
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: Adele
            status: dispatched
            assigned: testuser
            started: {{staleTime:o}}
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            task-role-history: {}
            ---
            # Adele — Session State
            """);

        var registry = new AgentRegistry(_testDir);

        var result = registry.ReserveAgent("Adele", out var error);

        Assert.True(result, $"ReserveAgent should succeed on stale dispatch: {error}");

        var state = registry.GetAgentState("Adele");
        Assert.Equal(AgentStatus.Dispatched, state!.Status);
        // Since should be refreshed to now, not the old stale time
        Assert.True((DateTime.UtcNow - state.Since!.Value).TotalSeconds < 10);
    }

    [Fact]
    public void ReserveAgent_WorkingAgent_Fails()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        // Write a working state
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "state.md"), """
            ---
            agent: Adele
            status: working
            assigned: testuser
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            task-role-history: {}
            ---
            # Adele — Session State
            """);

        var registry = new AgentRegistry(_testDir);

        var result = registry.ReserveAgent("Adele", out var error);

        Assert.False(result);
        Assert.Contains("not free", error);
    }

    [Fact]
    public async Task ReserveAgent_ConcurrentReservations_OnlyOneSucceeds()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        // Pre-create workspace to avoid directory creation race
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);

        var successCount = 0;

        var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            var registry = new AgentRegistry(_testDir);
            if (registry.ReserveAgent("Adele", out string _))
                Interlocked.Increment(ref successCount);
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, successCount);
    }

    [Fact]
    public void ClaimAgent_DispatchedAgent_Succeeds()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);

        // Reserve first (sets Dispatched)
        var reserved = registry.ReserveAgent("Adele", out var reserveError);
        Assert.True(reserved, $"ReserveAgent failed: {reserveError}");

        // Then claim (should succeed on Dispatched agent)
        registry.StorePendingSessionId("Adele", "test-session-dispatch");
        var claimed = registry.ClaimAgent("Adele", out var claimError);

        Assert.True(claimed, $"ClaimAgent on dispatched agent failed: {claimError}");

        var state = registry.GetAgentState("Adele");
        Assert.Equal(AgentStatus.Working, state!.Status);

        var session = registry.GetSession("Adele");
        Assert.NotNull(session);
        Assert.Equal("test-session-dispatch", session.SessionId);

        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void GetFreeAgents_ExcludesFreshDispatched()
    {
        SetupConfig(new[] { "Adele", "Brian" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Brian" } });
        var registry = new AgentRegistry(_testDir);

        // Reserve Adele
        registry.ReserveAgent("Adele", out _);

        var freeAgents = registry.GetFreeAgents();

        Assert.DoesNotContain(freeAgents, a => a.Name == "Adele");
        Assert.Contains(freeAgents, a => a.Name == "Brian");
    }

    [Fact]
    public void GetFreeAgents_IncludesStaleDispatched()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        // Write a dispatched state with old timestamp (stale)
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        var staleTime = DateTime.UtcNow.AddMinutes(-5);
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: Adele
            status: dispatched
            assigned: testuser
            started: {{staleTime:o}}
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            task-role-history: {}
            ---
            # Adele — Session State
            """);

        var registry = new AgentRegistry(_testDir);

        var freeAgents = registry.GetFreeAgents();

        Assert.Contains(freeAgents, a => a.Name == "Adele");
    }

    #endregion

    #region DispatchedBy Persistence

    [Fact]
    public void DispatchedBy_RoundTrips_ThroughStateFile()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");

        try
        {
            var scaffolder = new FolderScaffolder();
            var registry = new AgentRegistry(_testDir, null, scaffolder);
            registry.StorePendingSessionId("Adele", "test-session-db");
            registry.ClaimAgent("Adele", out _);
            registry.StoreSessionContext("test-session-db");

            // Plant inbox item with from field
            var inboxPath = Path.Combine(_testDir, "dydo", "agents", "Adele", "inbox");
            Directory.CreateDirectory(inboxPath);
            File.WriteAllText(Path.Combine(inboxPath, "deadbeef-my-task.md"), """
                ---
                id: deadbeef
                from: Brian
                role: code-writer
                task: my-task
                received: 2026-01-01T00:00:00Z
                ---
                # CODE-WRITER Request: my-task
                ## Brief
                Test brief
                """);

            registry.SetRole("test-session-db", "code-writer", "my-task", out _);

            var state = registry.GetAgentState("Adele");
            Assert.Equal("Brian", state?.DispatchedBy);

            // Verify roundtrip: create a fresh registry and re-read the state
            var registry2 = new AgentRegistry(_testDir);
            var state2 = registry2.GetAgentState("Adele");
            Assert.Equal("Brian", state2?.DispatchedBy);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
        }
    }

    [Fact]
    public void DispatchedBy_NullWhenNoInboxItem()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");

        try
        {
            var scaffolder = new FolderScaffolder();
            var registry = new AgentRegistry(_testDir, null, scaffolder);
            registry.StorePendingSessionId("Adele", "test-session-db2");
            registry.ClaimAgent("Adele", out _);
            registry.StoreSessionContext("test-session-db2");

            registry.SetRole("test-session-db2", "code-writer", "my-task", out _);

            var state = registry.GetAgentState("Adele");
            Assert.Null(state?.DispatchedBy);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
        }
    }

    #endregion

    #region Template Regeneration Path

    [Fact]
    public void ClaimAgent_RegeneratesTemplates_IntoDydoRoot()
    {
        // Setup config and pending session
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");

        try
        {
            var scaffolder = new FolderScaffolder();
            var registry = new AgentRegistry(_testDir, null, scaffolder);
            registry.StorePendingSessionId("Adele", "test-session-tpl");

            // Delete a template to force regeneration during claim
            var templateDir = Path.Combine(_testDir, "dydo", "_system", "templates");
            Directory.CreateDirectory(templateDir);
            var templateFile = Path.Combine(templateDir, "agent-workflow.template.md");
            if (File.Exists(templateFile))
                File.Delete(templateFile);

            var result = registry.ClaimAgent("Adele", out var error);
            Assert.True(result, $"ClaimAgent failed: {error}");

            // Template should be regenerated inside dydo/_system/templates/
            Assert.True(File.Exists(templateFile),
                "Template should be regenerated at dydo/_system/templates/agent-workflow.template.md");

            // Template should NOT be written at project root
            var wrongPath = Path.Combine(_testDir, "_system", "templates", "agent-workflow.template.md");
            Assert.False(File.Exists(wrongPath),
                "Template should NOT be regenerated at {projectRoot}/_system/templates/");
        }
        finally
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
        }
    }

    #endregion

    #region Dispatch Metadata Tests

    [Fact]
    public void SetDispatchMetadata_WritesWindowIdAndAutoClose()
    {
        SetupAgentState("Adele");

        _registry.SetDispatchMetadata("Adele", "abcd1234", true);

        var state = _registry.GetAgentState("Adele")!;
        Assert.Equal("abcd1234", state.WindowId);
        Assert.True(state.AutoClose);
    }

    [Fact]
    public void SetDispatchMetadata_RoundTrips_NullWindowId()
    {
        SetupAgentState("Adele");

        _registry.SetDispatchMetadata("Adele", null, false);

        var state = _registry.GetAgentState("Adele")!;
        Assert.Null(state.WindowId);
        Assert.False(state.AutoClose);
    }

    [Fact]
    public void SetDispatchMetadata_PersistsAcrossStateUpdates()
    {
        SetupAgentState("Adele");
        _registry.SetDispatchMetadata("Adele", "abcd1234", true);

        // Verify the metadata persists when read back
        var state = _registry.GetAgentState("Adele")!;
        Assert.Equal("abcd1234", state.WindowId);
        Assert.True(state.AutoClose);

        // Simulate what release does: clear role/task but NOT windowId/autoClose
        // (The integration test Release_WithAutoCloseState_PreservesAutoCloseForWatchdog
        // covers the full release flow)
        state.Status = AgentStatus.Free;
        state.Role = null;
        state.Task = null;

        // Re-read from disk to confirm persistence
        var reread = _registry.GetAgentState("Adele")!;
        Assert.Equal("abcd1234", reread.WindowId);
        Assert.True(reread.AutoClose);
    }

    private void SetupAgentState(string agentName)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: {{agentName}}
            role: null
            task: null
            status: free
            assigned: testuser
            dispatched-by: null
            window-id: null
            auto-close: false
            started: null
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            unread-messages: []
            task-role-history: {}
            ---
            """);
    }

    private string ClaimAgent(string agentName)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        var sessionId = Guid.NewGuid().ToString();
        File.WriteAllText(Path.Combine(workspace, ".session"), sessionId);
        // Mark as working so release can proceed
        _registry.SetRole(sessionId, "code-writer", "test-task", out _);
        return sessionId;
    }

    #endregion

    #region Worktree Helpers

    [Fact]
    public void GetWorktreeId_ReturnsId_WhenMarkerExists()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".worktree"), "Adele-20260313120000\n");

        Assert.Equal("Adele-20260313120000", _registry.GetWorktreeId("Adele"));
    }

    [Fact]
    public void GetWorktreeId_ReturnsNull_WhenNoMarker()
    {
        var workspace = _registry.GetAgentWorkspace("Adele");
        Directory.CreateDirectory(workspace);

        Assert.Null(_registry.GetWorktreeId("Adele"));
    }

    [Fact]
    public void IsWorktreeStale_ReturnsTrue_WhenDirectoryMissing()
    {
        Assert.True(_registry.IsWorktreeStale("nonexistent-id"));
    }

    [Fact]
    public void IsWorktreeStale_ReturnsFalse_WhenDirectoryExists()
    {
        var wtDir = Path.Combine(_testDir, "_system", ".local", "worktrees", "test-wt-id");
        Directory.CreateDirectory(wtDir);

        Assert.False(_registry.IsWorktreeStale("test-wt-id"));
    }

    [Theory]
    [InlineData("Frank-20260313124733", "Frank-0313")]
    [InlineData("Adele-20260101000000", "Adele-0101")]
    [InlineData("Grace-20261231235959", "Grace-1231")]
    public void TruncateWorktreeId_ExtractsMonthDay(string input, string expected)
    {
        Assert.Equal(expected, AgentRegistry.TruncateWorktreeId(input));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("no-dash")]
    [InlineData("x-123")]
    public void TruncateWorktreeId_ReturnsOriginal_WhenCannotParse(string input)
    {
        Assert.Equal(input, AgentRegistry.TruncateWorktreeId(input));
    }

    #endregion

    #region Claim Auto Nudge Tests

    private void CreateDispatchedState(string agentName, string human = "testuser")
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: {{agentName}}
            status: dispatched
            assigned: {{human}}
            started: {{DateTime.UtcNow:o}}
            writable-paths: []
            readonly-paths: []
            unread-must-reads: []
            task-role-history: {}
            ---
            # {{agentName}} — Session State
            """);
    }

    [Fact]
    public void ClaimAuto_WithDispatchedAgent_FailsOnFirstAttempt()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele", "Brian" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Brian" } });
            CreateDispatchedState("Adele");
            CreateInboxItem("Adele", "my-task", "code-writer");

            var registry = new AgentRegistry(_testDir);
            registry.StoreSessionContext("test-session-cn1");

            var result = registry.ClaimAuto(out _, out var error);

            Assert.False(result);
            Assert.Contains("dispatched agents waiting", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ClaimAuto_WithDispatchedAgent_SucceedsOnRetry()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele", "Brian" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Brian" } });
            CreateDispatchedState("Adele");
            CreateInboxItem("Adele", "my-task", "code-writer");

            var registry = new AgentRegistry(_testDir);
            registry.StoreSessionContext("test-session-cn2");
            registry.StorePendingSessionId("Brian", "test-session-cn2");

            // First call fails with nudge
            registry.ClaimAuto(out _, out _);

            // Second call succeeds
            var result = registry.ClaimAuto(out var claimed, out var error);

            Assert.True(result, $"ClaimAuto should succeed on retry: {error}");
            Assert.Equal("Brian", claimed);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ClaimAuto_WithNoDispatchedAgents_SucceedsWithoutNudge()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

            var registry = new AgentRegistry(_testDir);
            registry.StoreSessionContext("test-session-cn3");
            registry.StorePendingSessionId("Adele", "test-session-cn3");

            var result = registry.ClaimAuto(out var claimed, out var error);

            Assert.True(result, $"ClaimAuto should succeed without dispatched agents: {error}");
            Assert.Equal("Adele", claimed);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ClaimAuto_WithMultipleDispatchedAgents_StillNudges()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele", "Brian", "Charlie" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele", "Brian", "Charlie" } });
            CreateDispatchedState("Adele");
            CreateInboxItem("Adele", "task-a", "code-writer");
            CreateDispatchedState("Brian");
            CreateInboxItem("Brian", "task-b", "reviewer");

            var registry = new AgentRegistry(_testDir);
            registry.StoreSessionContext("test-session-cn4");

            var result = registry.ClaimAuto(out _, out var error);

            Assert.False(result);
            Assert.Contains("dispatched agents waiting", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ClaimAuto_NudgeMarkerCleanedOnClaimByName()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele", "Brian" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Brian" } });
            CreateDispatchedState("Adele");
            CreateInboxItem("Adele", "my-task", "code-writer");

            var registry = new AgentRegistry(_testDir);
            registry.StoreSessionContext("test-session-cn5");
            registry.StorePendingSessionId("Adele", "test-session-cn5");

            // Trigger nudge
            registry.ClaimAuto(out _, out _);

            var agentsPath = Path.Combine(_testDir, "dydo", "agents");
            var markerPath = Path.Combine(agentsPath, ".claim-nudge-test-session-cn5");
            Assert.True(File.Exists(markerPath), "Marker should exist after nudge");

            // Claim by name — marker should be cleaned
            var result = registry.ClaimAgent("Adele", out var error);

            Assert.True(result, $"ClaimAgent should succeed: {error}");
            Assert.False(File.Exists(markerPath), "Marker should be deleted after claim by name");
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ClaimAuto_DispatchedAgentWithNoInbox_NoNudge()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele", "Brian" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Brian" } });
            CreateDispatchedState("Adele");
            // No inbox item for Adele

            var registry = new AgentRegistry(_testDir);
            registry.StoreSessionContext("test-session-cn6");
            registry.StorePendingSessionId("Brian", "test-session-cn6");

            var result = registry.ClaimAuto(out var claimed, out var error);

            Assert.True(result, $"ClaimAuto should succeed when dispatched agent has no inbox: {error}");
            Assert.Equal("Brian", claimed);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ClaimAuto_NoSessionContext_NoNudge()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele", "Brian" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Brian" } });
            CreateDispatchedState("Adele");
            CreateInboxItem("Adele", "my-task", "code-writer");

            var registry = new AgentRegistry(_testDir);
            // No StoreSessionContext — no session ID available
            registry.StorePendingSessionId("Brian", "fallback-session");

            var result = registry.ClaimAuto(out var claimed, out var error);

            Assert.True(result, $"ClaimAuto should succeed without session context: {error}");
            Assert.Equal("Brian", claimed);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    #endregion

    #region CRAP Coverage — Uncovered Error Paths

    [Fact]
    public void ReserveAgent_InvalidName_Fails()
    {
        var result = _registry.ReserveAgent("Invalid", out var error);
        Assert.False(result);
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void ReserveAgent_NullState_Fails()
    {
        // Agent exists but state returns null — covered by the "not found" path
        // (actually GetAgentState returns a default, so this tests "not free" path)
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var registry = new AgentRegistry(_testDir);

        // Put Adele in working status
        CreateSessionFile("Adele", "test-sess-reserve1");
        registry = new AgentRegistry(_testDir);

        var result = registry.ReserveAgent("Adele", out var error);
        Assert.False(result);
        Assert.Contains("not free", error);
    }

    [Fact]
    public void ClaimAgent_SessionAlreadyHasDifferentAgent_Fails()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele", "Brian" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele", "Brian" } });

            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "shared-session");
            registry.ClaimAgent("Adele", out _);

            // Try to claim Brian with same session
            registry.StorePendingSessionId("Brian", "shared-session");
            var result = registry.ClaimAgent("Brian", out var error);

            Assert.False(result);
            Assert.Contains("already has agent Adele", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ClaimAgent_AlreadyClaimedByOtherSession_ShowsClaimableAgents()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele", "Brian" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele", "Brian" } });

            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "session-1");
            registry.ClaimAgent("Adele", out _);

            // Try claiming Adele from a different session
            registry.StorePendingSessionId("Adele", "session-2");
            var result = registry.ClaimAgent("Adele", out var error);

            Assert.False(result);
            Assert.Contains("already claimed by another session", error);
            Assert.Contains("Claimable agents", error);
            Assert.Contains("Brian", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ClaimAuto_NoAgentsAssigned_Fails()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "nobody");
        try
        {
            SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele" } });

            var registry = new AgentRegistry(_testDir);
            var result = registry.ClaimAuto(out _, out var error);

            Assert.False(result);
            Assert.Contains("No agents assigned to human", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ClaimAuto_AllAgentsBusy_ShowsStatuses()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele" } });

            // Claim Adele so no free agents remain
            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "sess-busy");
            registry.ClaimAgent("Adele", out _);

            // Try ClaimAuto from a different session
            registry.StoreSessionContext("sess-busy2");
            var result = registry.ClaimAuto(out _, out var error);

            Assert.False(result);
            Assert.Contains("No free agents", error);
            Assert.Contains("Adele (working)", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void SetRole_RoleNudge_DispatchedAsDifferentRole()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]>
            { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-rn1");
        CreateInboxItem("Adele", "my-task", "reviewer");

        var registry = new AgentRegistry(_testDir);

        // First attempt should nudge
        var result = registry.SetRole("test-session-rn1", "co-thinker", "my-task", out var error);
        Assert.False(result);
        Assert.Contains("dispatched as 'reviewer'", error);

        // Second attempt should succeed (marker exists from first try)
        result = registry.SetRole("test-session-rn1", "co-thinker", "my-task", out error);
        Assert.True(result, $"Should succeed on retry: {error}");
    }

    [Fact]
    public void ReleaseAgent_WithWaitMarkers_Fails()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele" } });

            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "sess-wait");
            registry.ClaimAgent("Adele", out _);
            registry.SetRole("sess-wait", "code-writer", "test-task", out _);

            // Create a wait marker
            registry.CreateWaitMarker("Adele", "test-task", "Brian");

            var result = registry.ReleaseAgent("sess-wait", out var error);
            Assert.False(result);
            Assert.Contains("waiting for response", error);
            Assert.Contains("test-task", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ReleaseAgent_WithReplyPendingMarkers_Fails()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele" } });

            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "sess-reply");
            registry.ClaimAgent("Adele", out _);
            registry.SetRole("sess-reply", "code-writer", "test-task", out _);

            // Create a reply-pending marker
            registry.CreateReplyPendingMarker("Adele", "test-task", "Brian");

            var result = registry.ReleaseAgent("sess-reply", out var error);
            Assert.False(result);
            Assert.Contains("pending reply", error);
            Assert.Contains("Brian", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ReleaseAgent_WithNeedsMerge_Fails()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele" } });

            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "sess-merge");
            registry.ClaimAgent("Adele", out _);
            registry.SetRole("sess-merge", "code-writer", "test-task", out _);

            // Create .needs-merge marker
            var workspace = registry.GetAgentWorkspace("Adele");
            File.WriteAllText(Path.Combine(workspace, ".needs-merge"), "test-task");

            var result = registry.ReleaseAgent("sess-merge", out var error);
            Assert.False(result);
            Assert.Contains("merge not dispatched", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    #endregion

    #region DYDO_AGENT Env Var Tests

    [Fact]
    public void GetSessionContext_PrefersDydoAgentEnvVar_OverFile()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            var scaffolder = new FolderScaffolder();
            var registry = new AgentRegistry(_testDir, null, scaffolder);

            // Store a session context file with one session ID
            registry.StoreSessionContext("file-session-111");

            // Claim agent (creates .session with a different session ID)
            registry.StoreSessionContext("agent-session-222");
            registry.ClaimAgent("Adele", out _);

            // Now set DYDO_AGENT env var
            Environment.SetEnvironmentVariable("DYDO_AGENT", "Adele");

            // GetSessionContext should return the session ID from the agent's .session file, not the file
            var sessionId = registry.GetSessionContext();
            var agentSession = registry.GetSession("Adele");
            Assert.NotNull(agentSession);
            Assert.Equal(agentSession.SessionId, sessionId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
            Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        }
    }

    [Fact]
    public void GetSessionContext_FallsBackToFile_WhenDydoAgentNotSet()
    {
        Environment.SetEnvironmentVariable("DYDO_AGENT", null);

        var registry = new AgentRegistry(_testDir);
        registry.StoreSessionContext("fallback-session-333");

        var sessionId = registry.GetSessionContext();
        Assert.Equal("fallback-session-333", sessionId);
    }

    [Fact]
    public void GetCurrentAgent_PrefersDydoAgentEnvVar_OverHintFile()
    {
        SetupConfig(new[] { "Adele", "Brian" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Brian" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            var scaffolder = new FolderScaffolder();
            var registry = new AgentRegistry(_testDir, null, scaffolder);

            // Claim Adele and Brian with known session IDs
            registry.StoreSessionContext("session-adele");
            registry.ClaimAgent("Adele", out _);

            registry.StoreSessionContext("session-brian");
            registry.ClaimAgent("Brian", out _);

            var adeleSession = registry.GetSession("Adele");
            Assert.NotNull(adeleSession);

            // Write hint file pointing to Brian
            var hintPath = Path.Combine(_testDir, "dydo", "_system", ".local", ".session-agent");
            Directory.CreateDirectory(Path.GetDirectoryName(hintPath)!);
            File.WriteAllText(hintPath, "Brian");

            // Set DYDO_AGENT to Adele
            Environment.SetEnvironmentVariable("DYDO_AGENT", "Adele");

            // GetCurrentAgent should return Adele (from env var), not Brian (from hint file)
            var result = registry.GetCurrentAgent(adeleSession.SessionId);
            Assert.NotNull(result);
            Assert.Equal("Adele", result.Name);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
            Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        }
    }

    #endregion
}
