namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class AgentCrudOperationsTests : IDisposable
{
    private readonly string _testDir;
    private readonly FakeConfigForCrud _configService;
    private readonly FakeFolderScaffolder _scaffolder;
    private readonly AgentCrudOperations _crud;

    public AgentCrudOperationsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-crud-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _configService = new FakeConfigForCrud(_testDir);
        _scaffolder = new FakeFolderScaffolder();
        _crud = new AgentCrudOperations(
            _testDir, _configService, _scaffolder,
            _configService.LoadConfig(_testDir),
            _ => null,
            _ => null);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private void SetupDydoJson(params string[] agents)
    {
        var config = new DydoConfig
        {
            Agents = new AgentsConfig
            {
                Pool = [..agents],
                Assignments = new Dictionary<string, List<string>> { ["tester"] = [..agents] }
            }
        };
        _configService.Config = config;
        _configService.ConfigPath = Path.Combine(_testDir, "dydo.json");
        File.WriteAllText(_configService.ConfigPath, "{}");
    }

    #region CreateAgent

    [Fact]
    public void CreateAgent_EmptyName_ReturnsFalse()
    {
        Assert.False(_crud.CreateAgent("", "tester", out var error));
        Assert.Contains("empty", error);
    }

    [Fact]
    public void CreateAgent_InvalidChars_ReturnsFalse()
    {
        Assert.False(_crud.CreateAgent("no spaces", "tester", out var error));
        Assert.Contains("letters", error);
    }

    [Fact]
    public void CreateAgent_TooLong_ReturnsFalse()
    {
        Assert.False(_crud.CreateAgent("TenLetters", "tester", out var error));
        Assert.Contains("9 characters", error);
    }

    [Fact]
    public void CreateAgent_EmptyHuman_ReturnsFalse()
    {
        Assert.False(_crud.CreateAgent("Alice", "", out var error));
        Assert.Contains("Human name", error);
    }

    [Fact]
    public void CreateAgent_NoConfig_ReturnsFalse()
    {
        _configService.Config = null;
        _configService.ConfigPath = null;

        Assert.False(_crud.CreateAgent("Alice", "tester", out var error));
        Assert.Contains("dydo.json", error);
    }

    [Fact]
    public void CreateAgent_AlreadyExists_ReturnsFalse()
    {
        SetupDydoJson("Alice");

        Assert.False(_crud.CreateAgent("Alice", "tester", out var error));
        Assert.Contains("already exists", error);
    }

    [Fact]
    public void CreateAgent_Valid_ReturnsTrue()
    {
        SetupDydoJson();

        var result = _crud.CreateAgent("Alice", "tester", out var error);

        Assert.True(result);
        Assert.Empty(error);
    }

    #endregion

    #region RenameAgent

    [Fact]
    public void RenameAgent_EmptyNewName_ReturnsFalse()
    {
        Assert.False(_crud.RenameAgent("Alice", "", out var error));
        Assert.Contains("empty", error);
    }

    [Fact]
    public void RenameAgent_InvalidChars_ReturnsFalse()
    {
        Assert.False(_crud.RenameAgent("Alice", "bad name", out var error));
        Assert.Contains("letters", error);
    }

    [Fact]
    public void RenameAgent_TooLong_ReturnsFalse()
    {
        Assert.False(_crud.RenameAgent("Alice", "TenLetters", out var error));
        Assert.Contains("9 characters", error);
    }

    [Fact]
    public void RenameAgent_NoConfig_ReturnsFalse()
    {
        _configService.Config = null;
        _configService.ConfigPath = null;

        Assert.False(_crud.RenameAgent("Alice", "Bob", out var error));
        Assert.Contains("dydo.json", error);
    }

    [Fact]
    public void RenameAgent_NotInPool_ReturnsFalse()
    {
        SetupDydoJson();

        Assert.False(_crud.RenameAgent("Alice", "Bob", out var error));
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void RenameAgent_TargetExists_ReturnsFalse()
    {
        SetupDydoJson("Alice", "Bob");

        Assert.False(_crud.RenameAgent("Alice", "Bob", out var error));
        Assert.Contains("already exists", error);
    }

    [Fact]
    public void RenameAgent_Valid_ReturnsTrue()
    {
        SetupDydoJson("Alice");
        var wsDir = Path.Combine(_configService.GetAgentsPath(_testDir), "Alice");
        Directory.CreateDirectory(wsDir);
        File.WriteAllText(Path.Combine(wsDir, "state.md"), "---\nagent: Alice\n---\n# Alice — Session State");

        var result = _crud.RenameAgent("Alice", "Bob", out var error);

        Assert.True(result);
        Assert.Empty(error);
    }

    #endregion

    #region RemoveAgent

    [Fact]
    public void RemoveAgent_NoConfig_ReturnsFalse()
    {
        _configService.Config = null;
        _configService.ConfigPath = null;

        Assert.False(_crud.RemoveAgent("Alice", out var error));
        Assert.Contains("dydo.json", error);
    }

    [Fact]
    public void RemoveAgent_NotInPool_ReturnsFalse()
    {
        SetupDydoJson();

        Assert.False(_crud.RemoveAgent("Alice", out var error));
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void RemoveAgent_Valid_ReturnsTrue()
    {
        SetupDydoJson("Alice");
        var ws = Path.Combine(_configService.GetAgentsPath(_testDir), "Alice");
        Directory.CreateDirectory(ws);

        Assert.True(_crud.RemoveAgent("Alice", out _));
        Assert.False(Directory.Exists(ws));
    }

    #endregion

    #region ReassignAgent

    [Fact]
    public void ReassignAgent_EmptyHuman_ReturnsFalse()
    {
        Assert.False(_crud.ReassignAgent("Alice", "", out var error));
        Assert.Contains("Human name", error);
    }

    [Fact]
    public void ReassignAgent_NoConfig_ReturnsFalse()
    {
        _configService.Config = null;
        _configService.ConfigPath = null;

        Assert.False(_crud.ReassignAgent("Alice", "bob", out var error));
        Assert.Contains("dydo.json", error);
    }

    [Fact]
    public void ReassignAgent_NotInPool_ReturnsFalse()
    {
        SetupDydoJson();

        Assert.False(_crud.ReassignAgent("Alice", "bob", out var error));
        Assert.Contains("does not exist", error);
    }

    [Fact]
    public void ReassignAgent_SameHuman_ReturnsFalse()
    {
        SetupDydoJson("Alice");

        Assert.False(_crud.ReassignAgent("Alice", "tester", out var error));
        Assert.Contains("already assigned", error);
    }

    [Fact]
    public void ReassignAgent_Valid_ReturnsTrue()
    {
        SetupDydoJson("Alice");
        var ws = Path.Combine(_configService.GetAgentsPath(_testDir), "Alice");
        Directory.CreateDirectory(ws);
        File.WriteAllText(Path.Combine(ws, "state.md"), "---\nassigned: tester\n---\n");

        Assert.True(_crud.ReassignAgent("Alice", "bob", out _));
    }

    #endregion

    #region Test Fakes

    private class FakeConfigForCrud : IConfigService
    {
        private readonly string _basePath;
        public DydoConfig? Config { get; set; }
        public string? ConfigPath { get; set; }

        public FakeConfigForCrud(string basePath)
        {
            _basePath = basePath;
            Config = new DydoConfig();
        }

        public string? FindConfigFile(string? startPath = null) => ConfigPath;
        public DydoConfig? LoadConfig(string? startPath = null) => Config;
        public void SaveConfig(DydoConfig config, string path) { Config = config; }
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

    private class FakeFolderScaffolder : IFolderScaffolder
    {
        public void Scaffold(string basePath) { }
        public void Scaffold(string basePath, List<string> agentNames) { }
        public void ScaffoldAgentWorkspace(string agentsPath, string agentName) =>
            Directory.CreateDirectory(Path.Combine(agentsPath, agentName));
        public void RegenerateAgentFiles(string agentsPath, string agentName, List<string>? sourcePaths = null, List<string>? testPaths = null) { }
        public void CopyBuiltInTemplates(string basePath) { }
    }

    #endregion
}
