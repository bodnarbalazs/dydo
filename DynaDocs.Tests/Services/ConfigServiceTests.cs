namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class ConfigServiceTests : IDisposable
{
    private readonly string _testDir;

    public ConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-config-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);

        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
                "version": 1,
                "structure": { "root": "dydo" },
                "agents": { "pool": [], "assignments": {} }
            }
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void GetChangelogPath_ReturnsCorrectPath()
    {
        var service = new ConfigService();

        var result = service.GetChangelogPath(_testDir);

        Assert.EndsWith(Path.Combine("project", "changelog"), result);
    }

    [Fact]
    public void FindConfigFile_ReturnsNull_WhenNoConfigExists()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "dydo-empty-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var service = new ConfigService();
            Assert.Null(service.FindConfigFile(emptyDir));
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }
    }

    [Fact]
    public void FindConfigFile_CachesResult()
    {
        var service = new ConfigService();

        var first = service.FindConfigFile(_testDir);
        var second = service.FindConfigFile(_testDir);

        Assert.Equal(first, second);
    }

    [Fact]
    public void FindConfigFile_WalksUpDirectoryTree()
    {
        var subDir = Path.Combine(_testDir, "a", "b", "c");
        Directory.CreateDirectory(subDir);

        var service = new ConfigService();
        var result = service.FindConfigFile(subDir);

        Assert.NotNull(result);
        Assert.EndsWith("dydo.json", result);
    }

    [Fact]
    public void LoadConfig_ReturnsNull_WhenNoConfigFile()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "dydo-noconf-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var service = new ConfigService();
            Assert.Null(service.LoadConfig(emptyDir));
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }
    }

    [Fact]
    public void LoadConfig_ReturnsNull_WhenInvalidJson()
    {
        var badDir = Path.Combine(Path.GetTempPath(), "dydo-bad-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(badDir);
        File.WriteAllText(Path.Combine(badDir, "dydo.json"), "not valid json {{{");
        try
        {
            var service = new ConfigService();
            Assert.Null(service.LoadConfig(badDir));
        }
        finally
        {
            Directory.Delete(badDir, true);
        }
    }

    [Fact]
    public void LoadConfig_ReturnsConfig_WhenValid()
    {
        var service = new ConfigService();
        var config = service.LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.Equal(1, config.Version);
    }

    [Fact]
    public void SaveConfig_WritesFile()
    {
        var service = new ConfigService();
        var config = new DydoConfig
        {
            Version = 2,
            Structure = new StructureConfig { Root = "dydo" },
            Agents = new AgentsConfig(),
            Integrations = new Dictionary<string, bool>()
        };

        var path = Path.Combine(_testDir, "saved.json");
        service.SaveConfig(config, path);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("\"version\"", content);
    }

    [Fact]
    public void GetProjectRoot_ReturnsNull_WhenNoConfig()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "dydo-noroot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var service = new ConfigService();
            Assert.Null(service.GetProjectRoot(emptyDir));
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }
    }

    [Fact]
    public void GetProjectRoot_ReturnsDirectory_WhenConfigExists()
    {
        var service = new ConfigService();
        var root = service.GetProjectRoot(_testDir);

        Assert.NotNull(root);
        Assert.Equal(_testDir, root);
    }

    [Fact]
    public void GetDydoRoot_UsesConfiguredRoot()
    {
        var service = new ConfigService();
        var dydoRoot = service.GetDydoRoot(_testDir);

        Assert.EndsWith("dydo", dydoRoot);
    }

    [Fact]
    public void GetDydoRoot_FallsBackToStartPath_WhenNoConfig()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "dydo-fallback-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var service = new ConfigService();
            var result = service.GetDydoRoot(emptyDir);

            Assert.Equal(Path.Combine(emptyDir, "dydo"), result);
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }
    }

    [Fact]
    public void GetAgentsPath_ReturnsAgentsSubfolder()
    {
        var service = new ConfigService();
        var result = service.GetAgentsPath(_testDir);

        Assert.EndsWith("agents", result);
    }

    [Fact]
    public void GetDocsPath_ReturnsDydoRoot()
    {
        var service = new ConfigService();
        var dydoRoot = service.GetDydoRoot(_testDir);
        var docsPath = service.GetDocsPath(_testDir);

        Assert.Equal(dydoRoot, docsPath);
    }

    [Fact]
    public void GetTasksPath_ReturnsConfiguredPath()
    {
        var service = new ConfigService();
        var result = service.GetTasksPath(_testDir);

        Assert.Contains("project", result);
        Assert.Contains("tasks", result);
    }

    [Fact]
    public void GetAuditPath_ReturnsSystemAuditSubfolder()
    {
        var service = new ConfigService();
        var result = service.GetAuditPath(_testDir);

        Assert.Contains("_system", result);
        Assert.Contains("audit", result);
    }

    [Fact]
    public void GetIssuesPath_ReturnsConfiguredPath()
    {
        var service = new ConfigService();
        var result = service.GetIssuesPath(_testDir);

        Assert.Contains("project", result);
        Assert.Contains("issues", result);
    }

    [Fact]
    public void GetHumanFromEnv_ReturnsEnvValue()
    {
        var original = Environment.GetEnvironmentVariable("DYDO_HUMAN");
        try
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
            var service = new ConfigService();
            Assert.Equal("testuser", service.GetHumanFromEnv());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DYDO_HUMAN", original);
        }
    }

    [Fact]
    public void LoadConfig_WithNudges_DeserializesCorrectly()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
                "version": 1,
                "structure": { "root": "dydo" },
                "agents": { "pool": [], "assignments": {} },
                "nudges": [
                    { "pattern": "dotnet test.*coverlet", "message": "Use gap_check.py.", "severity": "warn" },
                    { "pattern": "rm -rf", "message": "Don't do that.", "severity": "block" }
                ]
            }
            """);

        var service = new ConfigService();
        var config = service.LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.Equal(2, config!.Nudges.Count);
        Assert.Equal("dotnet test.*coverlet", config.Nudges[0].Pattern);
        Assert.Equal("warn", config.Nudges[0].Severity);
        Assert.Equal("block", config.Nudges[1].Severity);
    }

    [Fact]
    public void LoadConfig_WithoutNudges_DefaultsToEmptyList()
    {
        var service = new ConfigService();
        var config = service.LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.Empty(config!.Nudges);
    }

    [Fact]
    public void LoadConfig_WithWorktreeMergeSafety_DeserializesCorrectly()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
                "version": 1,
                "structure": { "root": "dydo" },
                "agents": { "pool": [], "assignments": {} },
                "worktree": {
                    "mergeSafety": {
                        "ignore": ["custom/**", "*.tmp"],
                        "ignoreDefaults": false
                    }
                }
            }
            """);

        var service = new ConfigService();
        var config = service.LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.False(config!.Worktree.MergeSafety.IgnoreDefaults);
        Assert.Equal(2, config.Worktree.MergeSafety.Ignore.Count);
        Assert.Contains("custom/**", config.Worktree.MergeSafety.Ignore);
    }

    [Fact]
    public void LoadConfig_WithoutWorktreeBlock_UsesDefaults()
    {
        var service = new ConfigService();
        var config = service.LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.True(config!.Worktree.MergeSafety.IgnoreDefaults);
        Assert.Empty(config.Worktree.MergeSafety.Ignore);
    }
}
