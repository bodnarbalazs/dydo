namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;

public class CheckAgentValidatorTests : IDisposable
{
    private readonly string _testDir;
    private readonly FakeConfigServiceForCAV _configService;

    public CheckAgentValidatorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-cav-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _configService = new FakeConfigServiceForCAV(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private DydoConfig CreateConfig(params string[] agents)
    {
        return new DydoConfig
        {
            Agents = new AgentsConfig
            {
                Pool = [..agents],
                Assignments = new Dictionary<string, List<string>> { ["tester"] = [..agents] }
            }
        };
    }

    [Fact]
    public void Validate_NoAgents_ReturnsEmpty()
    {
        var config = CreateConfig();
        var registry = new FakeAgentRegistryForCAV();

        var warnings = CheckAgentValidator.Validate(config, _configService, registry);

        Assert.Empty(warnings);
    }

    [Fact]
    public void Validate_WorkspaceMissing_NonFreeAgent_ReportsWarning()
    {
        var config = CreateConfig("Alice");
        var registry = new FakeAgentRegistryForCAV();
        registry.States["Alice"] = new AgentState
        {
            Name = "Alice",
            Status = AgentStatus.Working
        };
        // No workspace dir created

        var warnings = CheckAgentValidator.Validate(config, _configService, registry);

        Assert.Contains(warnings, w => w.Contains("workspace missing"));
    }

    [Fact]
    public void Validate_WorkspaceMissing_FreeAgent_NoWarning()
    {
        var config = CreateConfig("Alice");
        var registry = new FakeAgentRegistryForCAV();
        registry.States["Alice"] = new AgentState
        {
            Name = "Alice",
            Status = AgentStatus.Free
        };

        var warnings = CheckAgentValidator.Validate(config, _configService, registry);

        Assert.DoesNotContain(warnings, w => w.Contains("workspace missing"));
    }

    [Fact]
    public void Validate_AssignmentMismatch_ReportsWarning()
    {
        var config = CreateConfig("Alice");
        config.Agents.Assignments = new Dictionary<string, List<string>> { ["bob"] = ["Alice"] };

        var ws = Path.Combine(_testDir, "dydo", "agents", "Alice");
        Directory.CreateDirectory(ws);
        var registry = new FakeAgentRegistryForCAV();
        registry.Workspaces["Alice"] = ws;
        registry.States["Alice"] = new AgentState
        {
            Name = "Alice",
            AssignedHuman = "charlie"
        };

        var warnings = CheckAgentValidator.Validate(config, _configService, registry);

        Assert.Contains(warnings, w => w.Contains("charlie") && w.Contains("bob"));
    }

    [Fact]
    public void Validate_StaleSession_ReportsWarning()
    {
        var config = CreateConfig("Alice");
        var ws = Path.Combine(_testDir, "dydo", "agents", "Alice");
        Directory.CreateDirectory(ws);
        var registry = new FakeAgentRegistryForCAV();
        registry.Workspaces["Alice"] = ws;
        registry.States["Alice"] = new AgentState { Name = "Alice" };
        registry.Sessions["Alice"] = new AgentSession
        {
            Agent = "Alice",
            SessionId = "s1",
            Claimed = DateTime.UtcNow.AddHours(-48)
        };

        var warnings = CheckAgentValidator.Validate(config, _configService, registry);

        Assert.Contains(warnings, w => w.Contains("stale session"));
    }

    [Fact]
    public void Validate_FreshSession_NoStaleWarning()
    {
        var config = CreateConfig("Alice");
        var ws = Path.Combine(_testDir, "dydo", "agents", "Alice");
        Directory.CreateDirectory(ws);
        var registry = new FakeAgentRegistryForCAV();
        registry.Workspaces["Alice"] = ws;
        registry.States["Alice"] = new AgentState { Name = "Alice" };
        registry.Sessions["Alice"] = new AgentSession
        {
            Agent = "Alice",
            SessionId = "s1",
            Claimed = DateTime.UtcNow.AddMinutes(-30)
        };

        var warnings = CheckAgentValidator.Validate(config, _configService, registry);

        Assert.DoesNotContain(warnings, w => w.Contains("stale"));
    }

    [Fact]
    public void Validate_OrphanedWorkspace_ReportsWarning()
    {
        var config = CreateConfig("Alice");
        var agentsDir = Path.Combine(_testDir, "dydo", "agents");
        Directory.CreateDirectory(Path.Combine(agentsDir, "Alice"));
        Directory.CreateDirectory(Path.Combine(agentsDir, "Orphan"));

        var registry = new FakeAgentRegistryForCAV();
        var ws = Path.Combine(agentsDir, "Alice");
        registry.Workspaces["Alice"] = ws;
        registry.States["Alice"] = new AgentState { Name = "Alice" };

        var warnings = CheckAgentValidator.Validate(config, _configService, registry);

        Assert.Contains(warnings, w => w.Contains("Orphan") && w.Contains("not in agent pool"));
    }

    [Fact]
    public void Validate_DotPrefixedFolder_Ignored()
    {
        var config = CreateConfig("Alice");
        var agentsDir = Path.Combine(_testDir, "dydo", "agents");
        Directory.CreateDirectory(Path.Combine(agentsDir, "Alice"));
        Directory.CreateDirectory(Path.Combine(agentsDir, ".hidden"));

        var registry = new FakeAgentRegistryForCAV();
        registry.Workspaces["Alice"] = Path.Combine(agentsDir, "Alice");
        registry.States["Alice"] = new AgentState { Name = "Alice" };

        var warnings = CheckAgentValidator.Validate(config, _configService, registry);

        Assert.DoesNotContain(warnings, w => w.Contains(".hidden"));
    }

    #region Test Fakes

    private class FakeConfigServiceForCAV : IConfigService
    {
        private readonly string _basePath;
        public FakeConfigServiceForCAV(string basePath) => _basePath = basePath;
        public string? FindConfigFile(string? startPath = null) => null;
        public DydoConfig? LoadConfig(string? startPath = null) => null;
        public void SaveConfig(DydoConfig config, string path) { }
        public string? GetHumanFromEnv() => "tester";
        public string? GetProjectRoot(string? startPath = null) => _basePath;
        public string GetDydoRoot(string? startPath = null) => Path.Combine(_basePath, "dydo");
        public string GetAgentsPath(string? startPath = null) => Path.Combine(_basePath, "dydo", "agents");
        public string GetDocsPath(string? startPath = null) => "";
        public string GetTasksPath(string? startPath = null) => "";
        public string GetAuditPath(string? startPath = null) => "";
        public string GetChangelogPath(string? startPath = null) => "";
        public string GetIssuesPath(string? startPath = null) => "";
        public (bool CanClaim, string? Error) ValidateAgentClaim(string agentName, string? humanName, DydoConfig? config) => (true, null);
    }

    private class FakeAgentRegistryForCAV : IAgentRegistry
    {
        public Dictionary<string, AgentState> States { get; } = new();
        public Dictionary<string, string> Workspaces { get; } = new();
        public Dictionary<string, AgentSession> Sessions { get; } = new();

        public IReadOnlyList<string> AgentNames => States.Keys.ToList();
        public string WorkspacePath => "";
        public DydoConfig? Config => null;

        public string GetAgentWorkspace(string agentName) =>
            Workspaces.GetValueOrDefault(agentName, Path.Combine(Path.GetTempPath(), "nonexistent-" + agentName));

        public AgentState? GetAgentState(string agentName) => States.GetValueOrDefault(agentName);
        public AgentSession? GetSession(string agentName) => Sessions.GetValueOrDefault(agentName);

        // Stubs for unused methods
        public bool HasPendingInbox(string agentName) => false;
        public bool ReserveAgent(string agentName, out string error) { error = ""; return false; }
        public bool ClaimAgent(string agentName, out string error) { error = ""; return false; }
        public bool ClaimAuto(out string claimedAgent, out string error) { claimedAgent = ""; error = ""; return false; }
        public bool ReleaseAgent(string? sessionId, out string error) { error = ""; return false; }
        public bool SetRole(string? sessionId, string role, string? task, out string error) { error = ""; return false; }
        public bool CanTakeRole(string agentName, string role, string task, out string reason) { reason = ""; return true; }
        public List<AgentState> GetAllAgentStates() => States.Values.ToList();
        public List<AgentState> GetFreeAgents() => [];
        public List<AgentState> GetFreeAgentsForHuman(string human) => [];
        public AgentState? GetCurrentAgent(string? sessionId) => null;
        public bool IsPathAllowed(string? sessionId, string path, string action, out string error) { error = ""; return true; }
        public bool IsValidAgentName(string name) => true;
        public string? GetAgentNameFromLetter(char letter) => null;
        public string? GetCurrentHuman() => "tester";
        public string? GetHumanForAgent(string agentName) => "tester";
        public List<string> GetAgentsForHuman(string human) => [];
        public bool CreateAgent(string name, string human, out string error) { error = ""; return false; }
        public bool RenameAgent(string oldName, string newName, out string error) { error = ""; return false; }
        public bool RemoveAgent(string name, out string error) { error = ""; return false; }
        public bool ReassignAgent(string name, string newHuman, out string error) { error = ""; return false; }
        public void MarkMustReadComplete(string? sessionId, string relativePath) { }
        public void AddUnreadMessage(string agentName, string messageId) { }
        public void MarkMessageRead(string? sessionId, string messageId) { }
        public void ClearAllUnreadMessages(string agentName) { }
        public void StorePendingSessionId(string agentName, string sessionId) { }
        public string? GetPendingSessionId(string agentName) => null;
        public void CreateWaitMarker(string agentName, string task, string targetAgent) { }
        public List<WaitMarker> GetWaitMarkers(string agentName) => [];
        public bool RemoveWaitMarker(string agentName, string task) => false;
        public void ClearAllWaitMarkers(string agentName) { }
        public bool UpdateWaitMarkerListening(string agentName, string task, int pid) => false;
        public void ResetWaitMarkerListening(string agentName, string task) { }
        public List<WaitMarker> GetNonListeningWaitMarkers(string agentName) => [];
        public RoleDefinition? GetRoleDefinition(string roleName) => null;
        public void CreateDispatchMarker(string agentName, string task, string targetRole, string dispatchedTo) { }
        public bool HasDispatchMarker(string agentName, string task, string targetRole) => false;
        public void ClearAllDispatchMarkers(string agentName) { }
        public string? GetSessionContext() => null;
        public void StoreSessionContext(string sessionId) { }
    }

    #endregion
}
