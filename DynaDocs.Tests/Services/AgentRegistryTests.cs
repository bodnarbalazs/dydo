namespace DynaDocs.Tests.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
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
        AgentRegistry.IsLauncherAliveOverride = null;
        AgentRegistry.IsSessionPidAliveOverride = null;
        ProcessUtils.FindAncestorProcessOverride = null;
        ProcessUtils.GetParentPidOverride = null;
        ProcessUtils.GetProcessNameOverride = null;
        AgentRegistry.ReleaseGitCaptureOverride = null;
        WatchdogLogger.LogPathOverride = null;
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
        // Verify the expected claimable roles are documented. planner is skill-only
        // (Decision 024) and no longer claimable via `dydo agent role`.
        var knownRoles = new[] { "code-writer", "reviewer", "co-thinker", "docs-writer", "test-writer", "orchestrator" };
        Assert.Equal(6, knownRoles.Length);
    }

    [Theory]
    [InlineData("code-writer")]
    [InlineData("reviewer")]
    [InlineData("co-thinker")]
    [InlineData("docs-writer")]
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
    public void ClaimAgent_SucceedsWithPendingSession_PublishesVerifiedSessionContext()
    {
        SetupConfig(new[] { "Adele", "Frank" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Frank" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());

        registry.StorePendingSessionId("Adele", "stale-adele", "claude");
        Assert.True(registry.ClaimAgent("Adele", out var adeleError), adeleError);

        registry.StorePendingSessionId("Frank", "fresh-frank", "codex", "gpt-5-codex");
        Assert.True(registry.ClaimAgent("Frank", out var frankError), frankError);

        var sessionId = registry.GetSessionContext();
        var current = registry.GetCurrentAgent(sessionId);

        Assert.Equal("fresh-frank", sessionId);
        Assert.NotNull(current);
        Assert.Equal("Frank", current.Name);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_PersistsPendingSessionHost()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());

        registry.StorePendingSessionId("Adele", "session-codex", "codex");

        var result = registry.ClaimAgent("Adele", out var error);

        Assert.True(result, $"ClaimAgent failed: {error}");
        var session = registry.GetSession("Adele");
        Assert.NotNull(session);
        Assert.Equal("session-codex", session.SessionId);
        Assert.Equal("codex", session.Host);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_CodexHost_StampsCodexAncestorPid()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        ProcessUtils.FindAncestorProcessOverride = (name, _) => name == "codex" ? 24680 : null;
        var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());

        registry.StorePendingSessionId("Adele", "session-codex", "codex");

        var result = registry.ClaimAgent("Adele", out var error);

        Assert.True(result, $"ClaimAgent failed: {error}");
        var session = registry.GetSession("Adele");
        Assert.NotNull(session);
        Assert.Equal("codex", session.Host);
        Assert.Equal(24680, session.ClaimedPid);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_SameSessionIdReclaim_RefreshesClaimedPid()
    {
        // #0143: after watchdog auto-resume, the resumed claude calls `dydo agent claim`
        // with the same SessionId as before the crash. HandleExistingSession's idempotent
        // short-circuit must refresh .session.ClaimedPid to the live process — otherwise
        // the watchdog's next dead-PID check fires another resume and produces duplicate
        // terminals. Identity properties (SessionId, Claimed timestamp) are preserved.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        var originalClaimed = DateTime.UtcNow.AddMinutes(-5).ToString("o");
        File.WriteAllText(Path.Combine(workspace, ".session"),
            $"{{\"Agent\":\"Adele\",\"SessionId\":\"sess-X\",\"Claimed\":\"{originalClaimed}\",\"ClaimedPid\":99999999}}");
        File.WriteAllText(Path.Combine(workspace, "state.md"), """
            ---
            agent: Adele
            status: working
            assigned: testuser
            ---
            """);

        var registry = new AgentRegistry(_testDir);
        registry.StorePendingSessionId("Adele", "sess-X");

        var result = registry.ClaimAgent("Adele", out var error);

        Assert.True(result, $"ClaimAgent failed: {error}");
        var refreshed = registry.GetSession("Adele");
        Assert.NotNull(refreshed);
        Assert.Equal("sess-X", refreshed.SessionId);                // identity preserved
        Assert.Equal(originalClaimed, refreshed.Claimed.ToString("o")); // identity preserved
        Assert.NotEqual(99999999, refreshed.ClaimedPid);            // PID refreshed
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_SameSessionIdReclaim_PublishesVerifiedSessionContext()
    {
        SetupConfig(new[] { "Adele", "Frank" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele", "Frank" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var frankWorkspace = Path.Combine(_testDir, "dydo", "agents", "Frank");
        Directory.CreateDirectory(frankWorkspace);
        File.WriteAllText(Path.Combine(frankWorkspace, ".session"),
            $"{{\"Agent\":\"Frank\",\"SessionId\":\"sess-frank\",\"Host\":\"codex\",\"Model\":\"gpt-5-codex\",\"Claimed\":\"{DateTime.UtcNow.AddMinutes(-5):o}\",\"ClaimedPid\":99999999}}");
        File.WriteAllText(Path.Combine(frankWorkspace, "state.md"), """
            ---
            agent: Frank
            status: working
            assigned: testuser
            ---
            """);
        var adeleWorkspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(adeleWorkspace);
        File.WriteAllText(Path.Combine(adeleWorkspace, ".session"),
            $"{{\"Agent\":\"Adele\",\"SessionId\":\"stale-adele\",\"Claimed\":\"{DateTime.UtcNow.AddMinutes(-5):o}\",\"ClaimedPid\":111111}}");
        File.WriteAllText(Path.Combine(adeleWorkspace, "state.md"), """
            ---
            agent: Adele
            status: working
            assigned: testuser
            ---
            """);
        var registry = new AgentRegistry(_testDir);
        registry.StoreSessionContext("stale-adele", "Adele");
        registry.StorePendingSessionId("Frank", "sess-frank", "codex", "gpt-5-codex");

        Assert.True(registry.ClaimAgent("Frank", out var error), error);

        var sessionId = registry.GetSessionContext();
        var current = registry.GetCurrentAgent(sessionId);
        Assert.Equal("sess-frank", sessionId);
        Assert.NotNull(current);
        Assert.Equal("Frank", current.Name);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_SameSessionIdReclaim_ResetsResumeAttempts()
    {
        // #0153: a same-session reclaim is a successful resume — the resume budget
        // must be cleared so the next crash episode starts fresh. Without this,
        // the cap accumulates across crashes and long-lived agents become silently
        // un-resumable.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".session"),
            $"{{\"Agent\":\"Adele\",\"SessionId\":\"sess-X\",\"Claimed\":\"{DateTime.UtcNow.AddMinutes(-5):o}\",\"ClaimedPid\":99999999}}");
        File.WriteAllText(Path.Combine(workspace, "state.md"), """
            ---
            agent: Adele
            status: working
            assigned: testuser
            resume-attempts: 2
            last-resume-launched-at: 2026-04-01T00:00:00.0000000Z
            pre-resume-pid: 12345
            ---
            """);

        var registry = new AgentRegistry(_testDir);
        registry.StorePendingSessionId("Adele", "sess-X");

        var result = registry.ClaimAgent("Adele", out var error);

        Assert.True(result, $"ClaimAgent failed: {error}");
        var state = registry.GetAgentState("Adele");
        Assert.NotNull(state);
        Assert.Equal(0, state.ResumeAttempts);
        Assert.Null(state.LastResumeLaunchedAt);
        Assert.Null(state.PreResumePid);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_FreshClaim_RegistersAnchorWithClaudeAncestor()
    {
        // #0154: every claim must register an anchor for its claude ancestor.
        // Without this, leaf agents whose dispatcher has already exited lose
        // watchdog coverage and never auto-resume.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        ProcessUtils.FindAncestorProcessOverride = (_, _) => 65432;

        var registry = new AgentRegistry(_testDir);
        registry.StorePendingSessionId("Adele", "sess-fresh");

        var result = registry.ClaimAgent("Adele", out var error);

        Assert.True(result, $"ClaimAgent failed: {error}");
        var dydoRoot = Path.Combine(_testDir, "dydo");
        var anchorPath = Path.Combine(WatchdogService.GetAnchorsDirPath(dydoRoot), "65432.anchor");
        Assert.True(File.Exists(anchorPath),
            "fresh claim must write an anchor file for the claude ancestor PID");
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }


    [Fact]
    public void ClaimAgent_SameSessionResume_EmitsResumeOutcomeSucceeded_ToWatchdogLog()
    {
        // PR3 of agent-crash-fixes: same-session reclaim that follows a watchdog resume
        // launch (LastResumeLaunchedAt non-null) emits resume_outcome=succeeded so the
        // 4-bucket categorisation gets a one-grep signal. Pairs with the auto-recovery
        // Claim audit event covered by ClaimAgent_SameSessionResume_LogsAuto_WithPredecessorSession.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".session"),
            $"{{\"Agent\":\"Adele\",\"SessionId\":\"sess-out\",\"Claimed\":\"{DateTime.UtcNow.AddMinutes(-5):o}\",\"ClaimedPid\":99999999}}");
        File.WriteAllText(Path.Combine(workspace, "state.md"), $$"""
            ---
            agent: Adele
            status: working
            assigned: testuser
            resume-attempts: 1
            last-resume-launched-at: {{DateTime.UtcNow.AddSeconds(-90):o}}
            pre-resume-pid: 12345
            ---
            """);

        var logPath = Path.Combine(_testDir, "watchdog-claim.log");
        WatchdogLogger.LogPathOverride = logPath;

        var registry = new AgentRegistry(_testDir);
        registry.StorePendingSessionId("Adele", "sess-out");
        Assert.True(registry.ClaimAgent("Adele", out var err), err);

        Assert.True(File.Exists(logPath), "ClaimAgent's same-session resume path must emit resume_outcome to watchdog.log");
        var lines = File.ReadAllLines(logPath)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(l)!)
            .ToList();
        var outcome = lines.Single(l => l["event"].GetString() == "resume_outcome");
        Assert.Equal("succeeded", outcome["outcome"].GetString());
        Assert.Equal("Adele", outcome["agent"].GetString());
        Assert.Equal("sess-out", outcome["session_id"].GetString());
        Assert.Equal(1, outcome["attempts"].GetInt32());
        Assert.Equal("same_session_reclaim", outcome["reason"].GetString());
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }


    [Fact]
    public void ReleaseAgent_ClearsResumeBookkeepingFields()
    {
        // Symmetry to ClaimAgent: release zeroes resume-attempts, last-resume-launched-at,
        // and pre-resume-pid so a fresh re-dispatch starts with a clean budget.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var registry = new AgentRegistry(_testDir);
        registry.StorePendingSessionId("Adele", "sess-rel");
        Assert.True(registry.ClaimAgent("Adele", out var claimErr), claimErr);

        // Mutate the state to non-zero resume bookkeeping, then release.
        registry.IncrementResumeAttempts("Adele", preResumePid: 4242);
        var beforeRelease = registry.GetAgentState("Adele")!;
        Assert.Equal(1, beforeRelease.ResumeAttempts);
        Assert.NotNull(beforeRelease.LastResumeLaunchedAt);
        Assert.Equal(4242, beforeRelease.PreResumePid);

        Assert.True(registry.ReleaseAgent("sess-rel", out var relErr), relErr);

        var after = registry.GetAgentState("Adele")!;
        Assert.Equal(0, after.ResumeAttempts);
        Assert.Null(after.LastResumeLaunchedAt);
        Assert.Null(after.PreResumePid);
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

    private void WriteCommitRequiredCodeWriterRole(string? message = null)
    {
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        Directory.CreateDirectory(rolesDir);
        var role = new RoleDefinition
        {
            Name = "code-writer",
            Description = "Test code writer",
            Base = true,
            WritablePaths = ["Services/**"],
            ReadOnlyPaths = [],
            TemplateFile = "mode-code-writer.template.md",
            Constraints = [new RoleConstraint { Type = "requires-commit", Message = message ?? string.Empty }]
        };
        var json = JsonSerializer.Serialize(role, DydoConfigJsonContext.Default.RoleDefinition);
        File.WriteAllText(Path.Combine(rolesDir, "code-writer.role.json"), json);
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

        // Should allow co-thinker, test-writer, etc. on same task
        var canTakeCoThinker = registry.CanTakeRole("Adele", "co-thinker", "my-task", out var reason1);
        var canTakeTester = registry.CanTakeRole("Adele", "test-writer", "my-task", out var reason2);

        Assert.True(canTakeCoThinker, reason1);
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
        Assert.Contains("Orchestrator requires prior co-thinker experience", reason);
    }

    [Fact]
    public void CanTakeRole_AllowsOrchestratorWithCoThinkerHistory()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);
        File.WriteAllText(Path.Combine(workspacePath, "state.md"), """
            ---
            agent: Adele
            status: free
            assigned: testuser
            task-role-history: { "my-task": ["co-thinker"] }
            ---
            # Adele — Session State
            """);

        var registry = new AgentRegistry(_testDir);

        var canTake = registry.CanTakeRole("Adele", "orchestrator", "my-task", out var reason);

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
    [InlineData("test-writer")]
    [InlineData("orchestrator")]
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
        // This test ensures we have exactly 6 claimable roles (planner is skill-only).
        var knownRoles = new[] { "code-writer", "reviewer", "co-thinker", "docs-writer", "test-writer", "orchestrator" };

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
    public void ReleaseAgent_PreservesAutoCloseOnDisk_ForWatchdogKill()
    {
        // Regression for the v1.3.9 auto-close regression: after release of an agent
        // dispatched with auto-close, on-disk state must be `free + auto-close: true` —
        // that is exactly the precondition the watchdog poll requires to kill claude
        // (Services/WatchdogService.cs:359, `if (!autoClose || !isFree …) return 0;`).
        // The redispatch race that motivated clearing AutoClose on release (#0121) is
        // already closed by the per-agent .claim.lock in PollAndCleanupForAgent
        // (06512de); clearing AutoClose on release additionally killed the legitimate
        // post-release kill window.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-autoclose");

        var registry = new AgentRegistry(_testDir);
        registry.SetDispatchMetadata("Adele", "abcd1234", true);

        var statePath = Path.Combine(_testDir, "dydo", "agents", "Adele", "state.md");
        var preRelease = File.ReadAllText(statePath);
        Assert.Contains("auto-close: true", preRelease);

        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        var inboxPath = Path.Combine(workspace, "inbox");
        if (Directory.Exists(inboxPath))
            Directory.Delete(inboxPath, true);

        var released = registry.ReleaseAgent("test-session-autoclose", out var error);
        Assert.True(released, $"Release should succeed: {error}");

        var postRelease = File.ReadAllText(statePath);
        Assert.Contains("auto-close: true", postRelease);
        Assert.Contains("status: free", postRelease);
    }

    [Fact]
    public void WriteStateFile_AtomicReplace_ConcurrentReaderNeverSeesPartial()
    {
        // Regression for #0125: WriteStateFile must replace state.md atomically so
        // unlocked readers (e.g. `dydo agent status`) never observe a torn write.
        SetupAgentState("Adele");
        var statePath = Path.Combine(_testDir, "dydo", "agents", "Adele", "state.md");

        var stop = false;
        var parseFailures = 0;
        var readsObserved = 0;

        var reader = new System.Threading.Thread(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                string content;
                try { content = File.ReadAllText(statePath); }
                catch { continue; }

                Interlocked.Increment(ref readsObserved);
                var fields = DynaDocs.Utils.FrontmatterParser.ParseFields(content);
                if (fields == null || !fields.ContainsKey("agent") || !fields.ContainsKey("status"))
                    Interlocked.Increment(ref parseFailures);
            }
        });
        reader.Start();

        for (var i = 0; i < 200; i++)
        {
            try { _registry.SetDispatchMetadata("Adele", $"win-{i}", i % 2 == 0); }
            catch { /* writer-side IOException acceptable; contract is reader-side integrity */ }
        }

        Volatile.Write(ref stop, true);
        reader.Join();

        Assert.True(readsObserved > 0, "Reader thread should have observed at least one read");
        Assert.Equal(0, parseFailures);
    }

    [Fact]
    public void WriteStateFile_NoTempFilesLeftBehind()
    {
        // Regression for #0125: on the success path the rename consumes the temp file,
        // so no `state.md.tmp.*` siblings should remain after a write.
        SetupAgentState("Adele");
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");

        _registry.SetDispatchMetadata("Adele", "abcd1234", true);
        Assert.Empty(Directory.GetFiles(workspace, "state.md.tmp.*"));

        // Second write — guards against a regression where the catch-block cleanup
        // accidentally fires on the success path.
        _registry.SetDispatchMetadata("Adele", "efgh5678", false);
        Assert.Empty(Directory.GetFiles(workspace, "state.md.tmp.*"));
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
        var result3 = registry.SetRole("test-session-nudge14", "docs-writer", "my-task", out var err3);
        Assert.True(result3, $"Second switch after fulfilling dispatched role should also succeed: {err3}");
    }

    #endregion

    #region Lock File Tests

    [Fact]
    public void ClaimAgent_UsesPendingSessionId()
    {
        // Renamed from ClaimAgent_FallsBackToSessionContext_WhenNoPendingSession after #0196.
        // Pre-fix the test asserted that ClaimAgent could fall back to a legacy single-line
        // .session-context when no pending-session marker existed. Post-#0196 unverifiable
        // single-line content is discarded, so the fall-back never had a truthful source.
        // The remaining production path — the guard hook stages a pending-session before
        // ClaimAgent runs — is what this test now pins.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);

        registry.StorePendingSessionId("Adele", "ctx-session-456");

        var result = registry.ClaimAgent("Adele", out var error);

        Assert.True(result, $"ClaimAgent should consume the pending session id: {error}");

        var session = registry.GetSession("Adele");
        Assert.NotNull(session);
        Assert.Equal("ctx-session-456", session.SessionId);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    [Fact]
    public void ClaimAgent_DoesNotFallbackToSharedSessionContext()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);

        registry.StorePendingSessionId("Adele", "owned-session");
        Assert.True(registry.ClaimAgent("Adele", out var claimError), claimError);

        File.WriteAllText(Path.Combine(_testDir, "dydo", "agents", ".session-context"), "owned-session\nAdele");
        File.Delete(Path.Combine(_testDir, "dydo", "agents", "Adele", ".pending-session"));

        var result = registry.ClaimAgent("Adele", out var error);

        Assert.False(result);
        Assert.Contains("No session ID available", error);
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
    public void ClaimAgent_ConcurrentClaims_OnlyOneLockSucceeds()
    {
        // Setup config
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");

        // Pre-create workspace to avoid directory creation race
        var workspacePath = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspacePath);

        var scaffolder = new FolderScaffolder();
        var registry = new AgentRegistry(_testDir, null, scaffolder);
        registry.StorePendingSessionId("Adele", "shared-session");

        var lockPath = Path.Combine(workspacePath, ".claim.lock");
        Assert.True(AgentRegistry.TryAcquireLockAtPath(lockPath, "Adele", out var lockError), lockError);
        try
        {
            var blockedRegistry = new AgentRegistry(_testDir, null, scaffolder);

            var blocked = blockedRegistry.ClaimAgent("Adele", out var blockedError);

            Assert.False(blocked);
            Assert.Contains("claim in progress", blockedError);
            Assert.True(File.Exists(Path.Combine(workspacePath, ".pending-session")),
                "A failed lock contender must not consume the pending session.");
        }
        finally
        {
            AgentRegistry.ReleaseLockAtPath(lockPath);
        }

        var claimed = registry.ClaimAgent("Adele", out var error);

        Assert.True(claimed, $"ClaimAgent failed after lock release: {error}");
        var session = registry.GetSession("Adele");
        Assert.NotNull(session);
        Assert.Equal("shared-session", session.SessionId);
        Assert.False(File.Exists(Path.Combine(workspacePath, ".pending-session")),
            "The successful claim should consume the pending session exactly once.");
        Assert.False(File.Exists(lockPath), "Lock file should be cleaned up after claim");
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

        AgentRegistry.IsLauncherAliveOverride = _ => false;
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

        AgentRegistry.IsLauncherAliveOverride = _ => false;
        var registry = new AgentRegistry(_testDir);

        var freeAgents = registry.GetFreeAgents();

        Assert.Contains(freeAgents, a => a.Name == "Adele");
    }

    [Fact]
    public void GetFreeAgents_IncludesStaleDispatchedAgent()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });

        // Write a dispatched state with an old timestamp (stale) and a dead launcher.
        // Stale-dispatch reclaim should bridge this back to a re-dispatchable (free) state.
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

        AgentRegistry.IsLauncherAliveOverride = _ => false;
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

        // Simulate what release does: clear role/task, but NOT windowId or AutoClose.
        // (The integration test Release_PreservesAutoCloseOnDisk_ForWatchdogKill covers
        // the full flow.)
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
        // GetDydoRoot resolves to _testDir/dydo/ when no dydo.json exists
        var wtDir = Path.Combine(_testDir, "dydo", "_system", ".local", "worktrees", "test-wt-id");
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

    // Post-#0196 the .session-context legacy single-line shape is discarded. Tests that used
    // to seed a sessionId by calling registry.StoreSessionContext("foo") in isolation must
    // instead publish the verified two-line shape — sessionId + agent name backed by a real
    // .session file. This helper builds both halves at once for tests where the test author
    // doesn't otherwise care which agent owns the seed (e.g. ClaimAuto nudge tests that only
    // need *some* sessionId for the marker filename).
    private void SeedVerifiedSessionContext(string agentName, string sessionId, string human = "testuser")
    {
        // #0250: the .session-context file fallback now requires caller ownership. Stamp a
        // ClaimedPid and pin the host-ancestor lookup to it so the test caller owns the seeded
        // session (as a live agent's own terminal would).
        const int seedPid = 313131;
        ProcessUtils.FindAncestorProcessOverride = (_, _) => seedPid;
        var workspace = Path.Combine(_testDir, "dydo", "agents", agentName);
        Directory.CreateDirectory(workspace);
        var session = new AgentSession { Agent = agentName, SessionId = sessionId, Claimed = DateTime.UtcNow, ClaimedPid = seedPid };
        File.WriteAllText(Path.Combine(workspace, ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
        var contextPath = Path.Combine(_testDir, "dydo", "agents", ".session-context");
        Directory.CreateDirectory(Path.GetDirectoryName(contextPath)!);
        File.WriteAllText(contextPath, $"{sessionId}\n{agentName}");
    }

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
            SeedVerifiedSessionContext("Brian", "test-session-cn1");

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
            SeedVerifiedSessionContext("Brian", "test-session-cn2");
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
            SeedVerifiedSessionContext("Charlie", "test-session-cn4");

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
            // Seed the .session/.session-context on the agent the test subsequently claims,
            // so GetCurrentAgent(sessionId) won't resolve to a different agent and trip the
            // mismatch check inside ClaimAgent.
            SeedVerifiedSessionContext("Adele", "test-session-cn5");
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

    [Fact]
    public void ReleaseAgent_WorktreeBranchNotAheadOfBase_Fails()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele" } });
            WriteCommitRequiredCodeWriterRole("Commit the worktree changes before releasing.");

            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "sess-commit");
            Assert.True(registry.ClaimAgent("Adele", out var claimError), claimError);
            Assert.True(registry.SetRole("sess-commit", "code-writer", "test-task", out var roleError), roleError);

            var workspace = registry.GetAgentWorkspace("Adele");
            File.WriteAllText(Path.Combine(workspace, ".worktree"), "test-worktree");
            File.WriteAllText(Path.Combine(workspace, ".worktree-path"), Path.Combine(_testDir, "worktree"));
            File.WriteAllText(Path.Combine(workspace, ".worktree-base"), "main");
            AgentRegistry.ReleaseGitCaptureOverride = (_, _) => (0, "0\n");

            Assert.False(registry.ReleaseAgent("sess-commit", out var error));
            Assert.Equal("Commit the worktree changes before releasing.", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ReleaseAgent_WorktreeBranchAheadOfBase_Succeeds()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele" } });
            WriteCommitRequiredCodeWriterRole();

            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "sess-committed");
            Assert.True(registry.ClaimAgent("Adele", out var claimError), claimError);
            Assert.True(registry.SetRole("sess-committed", "code-writer", "test-task", out var roleError), roleError);

            var workspace = registry.GetAgentWorkspace("Adele");
            File.WriteAllText(Path.Combine(workspace, ".worktree"), "test-worktree");
            File.WriteAllText(Path.Combine(workspace, ".worktree-path"), Path.Combine(_testDir, "worktree"));
            File.WriteAllText(Path.Combine(workspace, ".worktree-base"), "main");
            AgentRegistry.ReleaseGitCaptureOverride = (_, _) => (0, "1\n");

            Assert.True(registry.ReleaseAgent("sess-committed", out var error), error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Fact]
    public void ReleaseAgent_WorktreeBranchNotAheadOfBase_UsesDefaultMessageWhenConstraintMessageEmpty()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele" } });
            WriteCommitRequiredCodeWriterRole();

            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "sess-default-message");
            Assert.True(registry.ClaimAgent("Adele", out var claimError), claimError);
            Assert.True(registry.SetRole("sess-default-message", "code-writer", "test-task", out var roleError), roleError);

            var workspace = registry.GetAgentWorkspace("Adele");
            File.WriteAllText(Path.Combine(workspace, ".worktree"), "test-worktree");
            File.WriteAllText(Path.Combine(workspace, ".worktree-path"), Path.Combine(_testDir, "worktree"));
            File.WriteAllText(Path.Combine(workspace, ".worktree-base"), "main");
            AgentRegistry.ReleaseGitCaptureOverride = (_, _) => (0, "0\n");

            Assert.False(registry.ReleaseAgent("sess-default-message", out var error));
            var worktreePath = Path.Combine(_testDir, "worktree");
            Assert.Equal($"You have uncommitted work in {worktreePath} (branch is not ahead of main). Run: git add -A && git commit -m '<message>' before releasing.", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    [Theory]
    [InlineData(128, "fatal: bad revision")]
    [InlineData(0, "garbage")]
    public void ReleaseAgent_WorktreeGitCheckFailsClosed(int exitCode, string stdout)
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]>
                { ["testuser"] = new[] { "Adele" } });
            WriteCommitRequiredCodeWriterRole();

            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "sess-git-failure");
            Assert.True(registry.ClaimAgent("Adele", out var claimError), claimError);
            Assert.True(registry.SetRole("sess-git-failure", "code-writer", "test-task", out var roleError), roleError);

            var workspace = registry.GetAgentWorkspace("Adele");
            File.WriteAllText(Path.Combine(workspace, ".worktree"), "test-worktree");
            File.WriteAllText(Path.Combine(workspace, ".worktree-path"), Path.Combine(_testDir, "worktree"));
            File.WriteAllText(Path.Combine(workspace, ".worktree-base"), "main");
            AgentRegistry.ReleaseGitCaptureOverride = (_, _) => (exitCode, stdout);

            Assert.False(registry.ReleaseAgent("sess-git-failure", out var error));
            Assert.Contains("Could not verify commits", error);
            Assert.Contains("git check failed", error);
        }
        finally { Environment.SetEnvironmentVariable("DYDO_HUMAN", null); }
    }

    #endregion

    #region DYDO_AGENT Env Var Tests

    // Closes #0189. The two original tests (GetSessionContext_PrefersDydoAgentEnvVar_OverFile and
    // GetCurrentAgent_PrefersDydoAgentEnvVar_OverHintFile) encoded the F1 hijack bug as the
    // intended contract: DYDO_AGENT was trusted unconditionally. Post-F1 the env var is only
    // honored when the caller actually owns the named agent (PID/claude-ancestor match against
    // .session.ClaimedPid). These rewrites pin the new contract.

    [Fact]
    public void GetSessionContext_DydoAgentEnvVar_OnlyTrustedWhenCallerOwnsAgent()
    {
        const int adelePid = 424242;
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        // Inject the claude ancestor before claim so ResolveClaimedPid stamps .session.ClaimedPid
        // with adelePid, and so the post-claim IsOwnedByCaller check finds the same value.
        ProcessUtils.FindAncestorProcessOverride = (_, _) => adelePid;
        try
        {
            var scaffolder = new FolderScaffolder();
            var registry = new AgentRegistry(_testDir, null, scaffolder);

            registry.StorePendingSessionId("Adele", "agent-session-222");
            registry.ClaimAgent("Adele", out _);

            Environment.SetEnvironmentVariable("DYDO_AGENT", "Adele");

            var sessionId = registry.GetSessionContext();
            var agentSession = registry.GetSession("Adele");
            Assert.NotNull(agentSession);
            Assert.Equal(adelePid, agentSession.ClaimedPid);
            Assert.Equal(agentSession.SessionId, sessionId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
            Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        }
    }

    [Fact]
    public void GetSessionContext_DydoAgentEnvVar_RejectedWhenCallerDoesNotOwnAgent()
    {
        const int charliePid = 131313;
        const int zeldaPid = 424242;
        SetupConfig(new[] { "Charlie", "Zelda" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Charlie", "Zelda" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            var scaffolder = new FolderScaffolder();
            var registry = new AgentRegistry(_testDir, null, scaffolder);

            ProcessUtils.FindAncestorProcessOverride = (_, _) => charliePid;
            registry.StorePendingSessionId("Charlie", "session-charlie");
            registry.ClaimAgent("Charlie", out _);

            ProcessUtils.FindAncestorProcessOverride = (_, _) => zeldaPid;
            registry.StorePendingSessionId("Zelda", "session-zelda");
            registry.ClaimAgent("Zelda", out _);

            // The "live" caller is Zelda's claude tab. DYDO_AGENT was set to Charlie by some
            // upstream shell. Pre-F1 GetSessionContext would have returned Charlie's session id.
            Environment.SetEnvironmentVariable("DYDO_AGENT", "Charlie");

            // Publish Zelda's verified context (two-line format), so the file fall-through has
            // something truthful to return.
            var zeldaSession = registry.GetSession("Zelda");
            var charlieSession = registry.GetSession("Charlie");
            Assert.NotNull(zeldaSession);
            Assert.NotNull(charlieSession);
            registry.StoreSessionContext(zeldaSession.SessionId, "Zelda");

            var sessionId = registry.GetSessionContext();
            Assert.NotEqual(charlieSession.SessionId, sessionId);
            Assert.Equal(zeldaSession.SessionId, sessionId);
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
        // After #0196 the verified two-line format is the only accepted shape; this test pins
        // that the DYDO_AGENT-unset path reads it correctly.
        Environment.SetEnvironmentVariable("DYDO_AGENT", null);

        const int adelePid = 555555;
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        ProcessUtils.FindAncestorProcessOverride = (_, _) => adelePid;
        try
        {
            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "agent-session-adele");
            registry.ClaimAgent("Adele", out _);
            var adeleSession = registry.GetSession("Adele");
            Assert.NotNull(adeleSession);
            registry.StoreSessionContext(adeleSession.SessionId, "Adele");

            var sessionId = registry.GetSessionContext();
            Assert.Equal(adeleSession.SessionId, sessionId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
        }
    }

    [Fact]
    public void GetSessionContext_DydoAgentEnvVar_InvalidAgentName_FallsThroughToFile()
    {
        // #0208: GetSessionContext must validate the DYDO_AGENT name before probing
        // GetSession, matching its sibling TryResolveCurrentAgentFromEnvVar. Here the env
        // var names an out-of-pool agent ("Bogus") whose crafted .session would otherwise
        // clear IsOwnedByCaller — without the IsValidAgentName guard GetSessionContext
        // would return Bogus's id. With the guard it falls through to the verified file.
        const int zeldaPid = 424242;
        SetupConfig(new[] { "Zelda" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Zelda" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        ProcessUtils.FindAncestorProcessOverride = (_, _) => zeldaPid;
        try
        {
            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Zelda", "session-zelda");
            registry.ClaimAgent("Zelda", out _);
            var zeldaSession = registry.GetSession("Zelda");
            Assert.NotNull(zeldaSession);
            registry.StoreSessionContext(zeldaSession.SessionId, "Zelda");

            // Craft a .session for an agent not in the pool; ClaimedPid = zeldaPid so it
            // would clear IsOwnedByCaller under the test's ancestor override.
            var bogusWorkspace = Path.Combine(_testDir, "dydo", "agents", "Bogus");
            Directory.CreateDirectory(bogusWorkspace);
            var bogusSession = new AgentSession
            {
                Agent = "Bogus",
                SessionId = "session-bogus",
                Claimed = DateTime.UtcNow,
                ClaimedPid = zeldaPid
            };
            File.WriteAllText(Path.Combine(bogusWorkspace, ".session"),
                JsonSerializer.Serialize(bogusSession, DydoDefaultJsonContext.Default.AgentSession));

            Environment.SetEnvironmentVariable("DYDO_AGENT", "Bogus");

            var sessionId = registry.GetSessionContext();
            Assert.Equal(zeldaSession.SessionId, sessionId);
            Assert.NotEqual("session-bogus", sessionId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
            Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        }
    }

    [Fact]
    public void GetSessionContext_DydoAgentEnvVar_NearestHostWins_InterposedForeignHostResolvesNull()
    {
        // #0256: the env fast-path must apply nearest-host-wins, not descendant-only ownership.
        // DYDO_AGENT=Adele is inherited by an inner foreign-vendor worker that descends from
        // Adele's claimed codex host but sits under a claude host of its own. Descendant
        // ownership passes; nearest-host-wins must refuse → the env branch falls through, and the
        // file fallback (also nearest-host) resolves null. The single-ancestor override the other
        // env tests inject cannot express this interposed shape — hence the multi-ancestor chain.
        const int adeleHostPid = 500010;
        const int claudeMidPid = 500011;
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        ProcessUtils.FindAncestorProcessOverride = (_, _) => adeleHostPid;  // stamps ClaimedPid at claim
        try
        {
            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());
            registry.StorePendingSessionId("Adele", "session-adele", "codex");
            registry.ClaimAgent("Adele", out _);
            var adeleSession = registry.GetSession("Adele");
            Assert.NotNull(adeleSession);
            Assert.Equal(adeleHostPid, adeleSession.ClaimedPid);
            registry.StoreSessionContext(adeleSession.SessionId, "Adele");

            Environment.SetEnvironmentVariable("DYDO_AGENT", "Adele");

            // Interposed foreign host: this process → claude host → Adele's claimed codex host.
            // FindAncestorProcessOverride MUST be null or NoForeignHostNearerThanClaimedHost
            // short-circuits on the injected single ancestor.
            ProcessUtils.FindAncestorProcessOverride = null;
            ProcessUtils.GetParentPidOverride = pid =>
                pid == Environment.ProcessId ? claudeMidPid :
                pid == claudeMidPid ? adeleHostPid : null;
            ProcessUtils.GetProcessNameOverride = pid => pid == claudeMidPid ? "claude" : "bash";

            Assert.Null(registry.GetSessionContext());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
            Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        }
    }

    [Fact]
    public void GetCurrentAgent_DydoAgentEnvVar_OnlyTrustedWhenCallerOwnsAgent()
    {
        const int adelePid = 424242;
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        ProcessUtils.FindAncestorProcessOverride = (_, _) => adelePid;
        try
        {
            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());

            registry.StorePendingSessionId("Adele", "session-adele");
            registry.ClaimAgent("Adele", out _);

            var adeleSession = registry.GetSession("Adele");
            Assert.NotNull(adeleSession);

            Environment.SetEnvironmentVariable("DYDO_AGENT", "Adele");

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

    [Fact]
    public void GetCurrentAgent_DydoAgentEnvVar_RejectedWhenCallerDoesNotOwnAgent()
    {
        // Defense-in-depth contract pin: env-path's IsOwnedByCaller gate must fire here too.
        // Observationally the slow-scan still resolves to the same agent (sessionId is unique),
        // so the rejection is not visible in the return value alone. We assert behavior is sane
        // (correct resolution) and pin the gate against accidental removal — a regression that
        // re-trusts the env path would surface in IdentityHijackRoleSetTests, paired with this.
        const int charliePid = 131313;
        const int zeldaPid = 424242;
        SetupConfig(new[] { "Charlie", "Zelda" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Charlie", "Zelda" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        try
        {
            var registry = new AgentRegistry(_testDir, null, new FolderScaffolder());

            ProcessUtils.FindAncestorProcessOverride = (_, _) => charliePid;
            registry.StorePendingSessionId("Charlie", "session-charlie");
            registry.ClaimAgent("Charlie", out _);

            ProcessUtils.FindAncestorProcessOverride = (_, _) => zeldaPid;
            registry.StorePendingSessionId("Zelda", "session-zelda");
            registry.ClaimAgent("Zelda", out _);

            var charlieSession = registry.GetSession("Charlie");
            var zeldaSession = registry.GetSession("Zelda");
            Assert.NotNull(charlieSession);
            Assert.NotNull(zeldaSession);

            // Caller is Zelda's claude tab (override returns zeldaPid). DYDO_AGENT inherited
            // "Charlie" from upstream. Call GetCurrentAgent with Zelda's truthful session id —
            // the env path sees sid mismatch (Charlie's session != Zelda's sid) and skips
            // independently of ownership; the hint/scan path returns Zelda correctly.
            Environment.SetEnvironmentVariable("DYDO_AGENT", "Charlie");

            var result = registry.GetCurrentAgent(zeldaSession.SessionId);
            Assert.NotNull(result);
            Assert.Equal("Zelda", result.Name);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
            Environment.SetEnvironmentVariable("DYDO_AGENT", null);
        }
    }

    [Fact]
    public void IsOwnedByCaller_NullClaimedPid_ReturnsFalse()
    {
        var session = new AgentSession { Agent = "Adele", SessionId = "s", ClaimedPid = null };
        Assert.False(AgentRegistry.IsOwnedByCaller(session));
    }

    [Fact]
    public void IsOwnedByCaller_MatchesCurrentProcessId_ReturnsTrue()
    {
        var session = new AgentSession { Agent = "Adele", SessionId = "s", ClaimedPid = Environment.ProcessId };
        Assert.True(AgentRegistry.IsOwnedByCaller(session));
    }

    [Fact]
    public void IsOwnedByCaller_MatchesClaudeAncestor_ReturnsTrue()
    {
        const int ancestor = 909090;
        ProcessUtils.FindAncestorProcessOverride = (_, _) => ancestor;
        var session = new AgentSession { Agent = "Adele", SessionId = "s", ClaimedPid = ancestor };
        Assert.True(AgentRegistry.IsOwnedByCaller(session));
    }

    [Fact]
    public void IsOwnedByCaller_CodexSession_MatchesCodexAncestor_ReturnsTrue()
    {
        const int ancestor = 909091;
        ProcessUtils.FindAncestorProcessOverride = (name, _) => name == "codex" ? ancestor : null;
        var session = new AgentSession { Agent = "Adele", SessionId = "s", Host = "codex", ClaimedPid = ancestor };
        Assert.True(AgentRegistry.IsOwnedByCaller(session));
    }

    [Fact]
    public void IsOwnedByCaller_CodexSession_IgnoresClaudeAncestor_ReturnsFalse()
    {
        const int ancestor = 909092;
        ProcessUtils.FindAncestorProcessOverride = (name, _) => name == "claude" ? ancestor : null;
        var session = new AgentSession { Agent = "Adele", SessionId = "s", Host = "codex", ClaimedPid = ancestor };
        Assert.False(AgentRegistry.IsOwnedByCaller(session));
    }

    [Fact]
    public void IsOwnedByCaller_MatchesParentShellFallback_ReturnsTrue()
    {
        const int parentShellPid = 818181;
        ProcessUtils.FindAncestorProcessOverride = (_, _) => null;
        ProcessUtils.GetParentPidOverride = pid => pid == Environment.ProcessId ? parentShellPid : null;
        var session = new AgentSession { Agent = "Adele", SessionId = "s", ClaimedPid = parentShellPid };
        Assert.True(AgentRegistry.IsOwnedByCaller(session));
    }

    [Fact]
    public void IsOwnedByCaller_NoMatch_ReturnsFalse()
    {
        ProcessUtils.FindAncestorProcessOverride = (_, _) => 111;
        var session = new AgentSession { Agent = "Adele", SessionId = "s", ClaimedPid = 222 };
        Assert.False(AgentRegistry.IsOwnedByCaller(session));
    }

    [Fact]
    public void IsOwnedByCaller_NoClaudeAncestor_ReturnsFalse()
    {
        // Caller isn't this process and there is no claude ancestor — covers the
        // claude.HasValue == false short-circuit on line 932.
        ProcessUtils.FindAncestorProcessOverride = (_, _) => null;
        var session = new AgentSession { Agent = "Adele", SessionId = "s", ClaimedPid = 222 };
        Assert.False(AgentRegistry.IsOwnedByCaller(session));
    }

    #endregion

    #region Session-Context File Fallback Ownership (#0250)

    // Closes #0250: the shared .session-context fallback used to resolve ambient identity to
    // the last active agent for ANY hookless caller (a foreign-vendor CLI session, an
    // MCP-spawned agent, a script), letting it mutate that agent's state. GetSessionContext's
    // file path now enforces caller ownership with nearest-host-wins; foreign processes get
    // null ambient identity — the truthful "human/unknown terminal" answer.

    private void WriteClaimedSession(string name, string sessionId, string host, int? claimedPid)
    {
        var workspace = Path.Combine(_testDir, "dydo", "agents", name);
        Directory.CreateDirectory(workspace);
        var session = new AgentSession
        {
            Agent = name,
            SessionId = sessionId,
            Host = host,
            Claimed = DateTime.UtcNow,
            ClaimedPid = claimedPid
        };
        File.WriteAllText(Path.Combine(workspace, ".session"),
            JsonSerializer.Serialize(session, DydoDefaultJsonContext.Default.AgentSession));
        File.WriteAllText(Path.Combine(workspace, "state.md"), $"""
            ---
            agent: {name}
            role: null
            task: null
            status: working
            assigned: testuser
            ---
            """);
    }

    [Fact]
    public void GetSessionContext_FileFallback_RefusedWhenCallerNotDescendantOfClaimedPid()
    {
        // The 0250 repro: a foreign process (no agent-host ancestry, not a descendant of the
        // claimed host) reads a valid context file. Ambient identity must resolve to null.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        WriteClaimedSession("Adele", "sid-adele", "claude", claimedPid: 500001);
        _registry.StoreSessionContext("sid-adele", "Adele");

        ProcessUtils.FindAncestorProcessOverride = (_, _) => null;   // no claude/codex host above
        ProcessUtils.GetParentPidOverride = _ => null;              // and not a descendant of 500001

        Assert.Null(_registry.GetSessionContext());
    }

    [Fact]
    public void GetSessionContext_FileFallback_AllowedForDescendantOfClaimedPid()
    {
        // The main legitimate consumer: a claude session's own `dydo` subprocess is a
        // descendant of the claimed host PID → ownership passes, identity resolves.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        const int claudeHostPid = 500002;
        WriteClaimedSession("Adele", "sid-adele", "claude", claimedPid: claudeHostPid);
        _registry.StoreSessionContext("sid-adele", "Adele");

        ProcessUtils.FindAncestorProcessOverride = (_, _) => null;
        // Caller's direct parent IS the claimed host; the parent itself is a plain shell.
        ProcessUtils.GetParentPidOverride = pid => pid == Environment.ProcessId ? claudeHostPid : null;
        ProcessUtils.GetProcessNameOverride = _ => "bash";

        Assert.Equal("sid-adele", _registry.GetSessionContext());
    }

    [Fact]
    public void GetSessionContext_FileFallback_NearestHostWins_ForeignHostBetweenCallerAndClaimedHost()
    {
        // An inner codex host spawned under an outer claude-claimed session. The codex worker
        // descends from the claude host (raw descendant ownership would pass), but the nearest
        // agent host above it is codex, not the claimed claude host — it is a worker, not the
        // agent. Ambient identity must resolve to null.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        const int claudeHostPid = 500003;
        const int codexHostPid = 500004;
        WriteClaimedSession("Adele", "sid-adele", "claude", claimedPid: claudeHostPid);
        _registry.StoreSessionContext("sid-adele", "Adele");

        // Ancestry: this process → codex host → claude host.
        ProcessUtils.GetParentPidOverride = pid =>
            pid == Environment.ProcessId ? codexHostPid :
            pid == codexHostPid ? claudeHostPid : null;
        ProcessUtils.GetProcessNameOverride = pid =>
            pid == codexHostPid ? "codex" :
            pid == claudeHostPid ? "claude" : null;

        Assert.Null(_registry.GetSessionContext());
    }

    [Fact]
    public void GetSessionContext_FileFallback_StaleClaimedPid_ResolvesNull()
    {
        // Resumed-session guard (#0207) rewrites ClaimedPid on the first guarded call, but a
        // CLI subprocess that runs before that refresh sees a dead pre-resume PID. It must get
        // null (no identity) rather than a wrong agent.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        WriteClaimedSession("Adele", "sid-adele", "claude", claimedPid: 999999);
        _registry.StoreSessionContext("sid-adele", "Adele");

        ProcessUtils.FindAncestorProcessOverride = (_, _) => null;
        ProcessUtils.GetParentPidOverride = pid => pid == Environment.ProcessId ? 424242 : null;
        ProcessUtils.GetProcessNameOverride = _ => "pwsh";

        Assert.Null(_registry.GetSessionContext());
    }

    [Fact]
    public void SetRole_NoAmbientIdentityForForeignProcess_ErrorsCleanlyWithoutMutating()
    {
        // Mirrors AgentLifecycleHandlers.ExecuteRole: sessionId = GetSessionContext();
        // SetRole(sessionId, ...). For a foreign process GetSessionContext is null, so SetRole
        // refuses with an actionable message and Adele's state.md is untouched.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        WriteClaimedSession("Adele", "sid-adele", "claude", claimedPid: 500005);
        _registry.StoreSessionContext("sid-adele", "Adele");

        ProcessUtils.FindAncestorProcessOverride = (_, _) => null;
        ProcessUtils.GetParentPidOverride = _ => null;

        var sessionId = _registry.GetSessionContext();
        Assert.Null(sessionId);
        Assert.Null(_registry.GetCurrentAgent(sessionId));

        var ok = _registry.SetRole(sessionId, "co-thinker", "some-task", out var error);
        Assert.False(ok);
        Assert.Contains("claim", error, StringComparison.OrdinalIgnoreCase);

        var adeleState = File.ReadAllText(Path.Combine(_testDir, "dydo", "agents", "Adele", "state.md"));
        Assert.DoesNotContain("role: co-thinker", adeleState);
    }

    [Fact]
    public void GetSessionContext_HumanTerminal_NullIdentityLeavesOwnedAgentUnclaimed()
    {
        // DR-036: a plain human terminal has no agent identity. Null resolution is the truthful
        // answer, and GetCurrentOwnedAgent stays null so human-only commands are not attributed
        // to (nor able to mutate) the claiming agent.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        WriteClaimedSession("Adele", "sid-adele", "claude", claimedPid: 500006);
        _registry.StoreSessionContext("sid-adele", "Adele");

        ProcessUtils.FindAncestorProcessOverride = (_, _) => null;
        ProcessUtils.GetParentPidOverride = _ => null;

        var sessionId = _registry.GetSessionContext();
        Assert.Null(sessionId);
        Assert.Null(_registry.GetCurrentOwnedAgent(sessionId));
    }

    #endregion

    #region DispatchedByRole Persistence Tests

    private void CreateInboxItemWithFromRole(string agentName, string task, string role, string from, string fromRole)
    {
        var inboxPath = Path.Combine(_testDir, "dydo", "agents", agentName, "inbox");
        Directory.CreateDirectory(inboxPath);
        var sanitizedTask = task.Replace(':', '-').Replace('<', '-').Replace('>', '-');
        File.WriteAllText(Path.Combine(inboxPath, $"abcd1234-{sanitizedTask}.md"), $"""
            ---
            id: abcd1234
            from: {from}
            from_role: {fromRole}
            role: {role}
            task: {task}
            received: 2026-01-01T00:00:00Z
            origin: {from}
            ---

            # {role.ToUpperInvariant()} Request: {task}
            """);
    }

    [Fact]
    public void SetRole_PersistsDispatchedByRole_InStateFile()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-dbr1");
        CreateInboxItemWithFromRole("Adele", "fix-bug", "reviewer", "Brian", "code-writer");

        var registry = new AgentRegistry(_testDir);
        var result = registry.SetRole("test-session-dbr1", "reviewer", "fix-bug", out var error);

        Assert.True(result, $"SetRole failed: {error}");

        // Read state file directly from disk to verify dispatched-by-role is persisted
        var statePath = Path.Combine(_testDir, "dydo", "agents", "Adele", "state.md");
        var stateContent = File.ReadAllText(statePath);
        Assert.Contains("dispatched-by-role: code-writer", stateContent);
        Assert.Contains("dispatched-by: Brian", stateContent);
    }

    [Fact]
    public void SetRole_DispatchedByRole_SurvivesReload()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-dbr2");
        CreateInboxItemWithFromRole("Adele", "fix-bug", "reviewer", "Brian", "code-writer");

        var registry = new AgentRegistry(_testDir);
        registry.SetRole("test-session-dbr2", "reviewer", "fix-bug", out _);

        // Reload registry from disk (simulates new process)
        var registry2 = new AgentRegistry(_testDir);
        var state = registry2.GetAgentState("Adele");

        Assert.Equal("code-writer", state?.DispatchedByRole);
        Assert.Equal("Brian", state?.DispatchedBy);
    }

    [Fact]
    public void SetRole_WithoutFromRole_PersistsNullDispatchedByRole()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        CreateSessionFile("Adele", "test-session-dbr3");
        CreateInboxItem("Adele", "fix-bug", "reviewer");

        var registry = new AgentRegistry(_testDir);
        registry.SetRole("test-session-dbr3", "reviewer", "fix-bug", out _);

        var statePath = Path.Combine(_testDir, "dydo", "agents", "Adele", "state.md");
        var stateContent = File.ReadAllText(statePath);
        Assert.Contains("dispatched-by-role: null", stateContent);
    }

    #endregion

    #region IncrementResumeAttempts (Decision 022)

    [Fact]
    public void IncrementResumeAttempts_ReturnsNewValue_AndPersists()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "state.md"), """
            ---
            agent: Adele
            status: working
            assigned: testuser
            resume-attempts: 1
            ---
            """);
        var registry = new AgentRegistry(_testDir);

        var newCount = registry.IncrementResumeAttempts("Adele");

        Assert.Equal(2, newCount);
        var persisted = registry.GetAgentState("Adele");
        Assert.Equal(2, persisted!.ResumeAttempts);
    }

    [Fact]
    public async Task IncrementResumeAttempts_ConcurrentCalls_ProduceExactCount()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "state.md"), """
            ---
            agent: Adele
            status: working
            assigned: testuser
            resume-attempts: 0
            ---
            """);
        var registry = new AgentRegistry(_testDir);

        const int iterationsPerThread = 5;

        // Spin until increment succeeds. -1 = lock contention; any exception thrown
        // before the registry's WriteStateFile commits is transient (Windows File.Move
        // races with AV / indexers under heavy parallel load). The contract being tested
        // is that EVERY successful increment counts exactly once, even under concurrent
        // callers.
        void IncrementOnce()
        {
            while (true)
            {
                try
                {
                    if (registry.IncrementResumeAttempts("Adele") >= 0) return;
                }
                catch
                {
                    // transient — retry. WriteStateFile's atomic temp+move means a thrown
                    // exception means the increment did not persist; the next read will
                    // see the prior value.
                }
                Thread.Sleep(1);
            }
        }

        var t1 = Task.Run(() => { for (var i = 0; i < iterationsPerThread; i++) IncrementOnce(); });
        var t2 = Task.Run(() => { for (var i = 0; i < iterationsPerThread; i++) IncrementOnce(); });
        await Task.WhenAll(t1, t2);

        Assert.Equal(2 * iterationsPerThread, registry.GetAgentState("Adele")!.ResumeAttempts);
    }

    [Fact]
    public void IncrementResumeAttempts_ParseLegacyState_TreatsMissingFieldAsZero()
    {
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        // Legacy state.md with no resume-attempts line.
        File.WriteAllText(Path.Combine(workspace, "state.md"), """
            ---
            agent: Adele
            status: working
            assigned: testuser
            ---
            """);
        var registry = new AgentRegistry(_testDir);

        var newCount = registry.IncrementResumeAttempts("Adele");

        Assert.Equal(1, newCount);
        var content = File.ReadAllText(Path.Combine(workspace, "state.md"));
        Assert.Contains("resume-attempts: 1", content);
    }

    [Fact]
    public void IncrementResumeAttempts_PersistsLaunchedPid_RoundTrips()
    {
        // #0173: launched-pid is what IsBadSessionFailFast reads to distinguish
        // "still rehydrating" from "genuinely failed". Round-trip must work both
        // ways: increment writes it, parser reads it back.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "state.md"), """
            ---
            agent: Adele
            status: working
            assigned: testuser
            resume-attempts: 0
            ---
            """);
        var registry = new AgentRegistry(_testDir);

        registry.IncrementResumeAttempts("Adele", preResumePid: 1111, launchedPid: 2222);

        var content = File.ReadAllText(Path.Combine(workspace, "state.md"));
        Assert.Contains("launched-pid: 2222", content);
        var state = registry.GetAgentState("Adele")!;
        Assert.Equal(2222, state.LaunchedPid);
    }

    [Fact]
    public void RecordResumeLaunch_PersistsLaunchedPid_WithoutBumpingCounter()
    {
        // The watchdog calls this AFTER IncrementResumeAttempts has already bumped
        // the counter — RecordResumeLaunch must update only the launched-pid field
        // and leave resume-attempts untouched.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "state.md"), """
            ---
            agent: Adele
            status: working
            assigned: testuser
            resume-attempts: 2
            ---
            """);
        var registry = new AgentRegistry(_testDir);

        var ok = registry.RecordResumeLaunch("Adele", 9999);

        Assert.True(ok);
        var state = registry.GetAgentState("Adele")!;
        Assert.Equal(9999, state.LaunchedPid);
        Assert.Equal(2, state.ResumeAttempts);
    }

    [Fact]
    public void ResetResumeBookkeeping_ClearsLaunchedPid_OnSameSessionReclaim()
    {
        // Symmetry to ClaimAgent_SameSessionIdReclaim_ResetsResumeAttempts: the
        // same-session reclaim path zeroes the resume budget. launched-pid must
        // be cleared alongside resume-attempts/last-resume-launched-at/pre-resume-pid
        // or a stale value would poison the next crash episode's IsBadSessionFailFast.
        SetupConfig(new[] { "Adele" }, new Dictionary<string, string[]> { ["testuser"] = new[] { "Adele" } });
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var workspace = Path.Combine(_testDir, "dydo", "agents", "Adele");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".session"),
            $"{{\"Agent\":\"Adele\",\"SessionId\":\"sess-X\",\"Claimed\":\"{DateTime.UtcNow.AddMinutes(-5):o}\",\"ClaimedPid\":99999999}}");
        File.WriteAllText(Path.Combine(workspace, "state.md"), """
            ---
            agent: Adele
            status: working
            assigned: testuser
            resume-attempts: 2
            last-resume-launched-at: 2026-04-01T00:00:00.0000000Z
            pre-resume-pid: 12345
            launched-pid: 67890
            ---
            """);

        var registry = new AgentRegistry(_testDir);
        registry.StorePendingSessionId("Adele", "sess-X");

        Assert.True(registry.ClaimAgent("Adele", out var error), error);
        var state = registry.GetAgentState("Adele")!;
        Assert.Null(state.LaunchedPid);
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);
    }

    #endregion
}
