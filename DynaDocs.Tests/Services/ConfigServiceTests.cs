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
}

public class AgentClaimValidatorTests
{
    [Fact]
    public void Validate_ReturnsError_WhenHumanNameNull()
    {
        var (canClaim, error) = AgentClaimValidator.Validate("Adele", null, null);

        Assert.False(canClaim);
        Assert.Contains("DYDO_HUMAN", error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenHumanNameEmpty()
    {
        var (canClaim, error) = AgentClaimValidator.Validate("Adele", "", null);

        Assert.False(canClaim);
        Assert.Contains("DYDO_HUMAN", error);
    }

    [Fact]
    public void Validate_AllowsClaim_WhenNoConfig()
    {
        var (canClaim, error) = AgentClaimValidator.Validate("Adele", "alice", null);

        Assert.True(canClaim);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenAgentNotInPool()
    {
        var config = new DydoConfig
        {
            Agents = new AgentsConfig { Pool = ["Brian", "Charlie"] }
        };

        var (canClaim, error) = AgentClaimValidator.Validate("Zebra", "alice", config);

        Assert.False(canClaim);
        Assert.Contains("not in the configured agent pool", error);
    }

    [Fact]
    public void Validate_AllowsClaim_WhenAgentUnassigned()
    {
        var config = new DydoConfig
        {
            Agents = new AgentsConfig
            {
                Pool = ["Adele"],
                Assignments = new Dictionary<string, List<string>>()
            }
        };

        var (canClaim, error) = AgentClaimValidator.Validate("Adele", "alice", config);

        Assert.True(canClaim);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_AllowsClaim_WhenAgentAssignedToSameHuman()
    {
        var config = new DydoConfig
        {
            Agents = new AgentsConfig
            {
                Pool = ["Adele"],
                Assignments = new Dictionary<string, List<string>>
                {
                    ["alice"] = ["Adele"]
                }
            }
        };

        var (canClaim, error) = AgentClaimValidator.Validate("Adele", "alice", config);

        Assert.True(canClaim);
        Assert.Null(error);
    }

    [Fact]
    public void Validate_ReturnsError_WhenAgentAssignedToDifferentHuman()
    {
        var config = new DydoConfig
        {
            Agents = new AgentsConfig
            {
                Pool = ["Adele"],
                Assignments = new Dictionary<string, List<string>>
                {
                    ["bob"] = ["Adele"]
                }
            }
        };

        var (canClaim, error) = AgentClaimValidator.Validate("Adele", "alice", config);

        Assert.False(canClaim);
        Assert.Contains("assigned to human 'bob'", error);
    }

    [Fact]
    public void Validate_IncludesClaimableAgents_InMismatchError()
    {
        var config = new DydoConfig
        {
            Agents = new AgentsConfig
            {
                Pool = ["Adele", "Brian"],
                Assignments = new Dictionary<string, List<string>>
                {
                    ["bob"] = ["Adele"],
                    ["alice"] = ["Brian"]
                }
            }
        };

        var (_, error) = AgentClaimValidator.Validate("Adele", "alice", config);

        Assert.Contains("Brian", error);
    }

    [Fact]
    public void Validate_ShowsNoneAssigned_WhenHumanHasNoAgents()
    {
        var config = new DydoConfig
        {
            Agents = new AgentsConfig
            {
                Pool = ["Adele"],
                Assignments = new Dictionary<string, List<string>>
                {
                    ["bob"] = ["Adele"]
                }
            }
        };

        var (_, error) = AgentClaimValidator.Validate("Adele", "alice", config);

        Assert.Contains("(none assigned)", error);
    }

    [Fact]
    public void FindFirstFree_ReturnsFirstFreeAgent()
    {
        var config = new DydoConfig
        {
            Agents = new AgentsConfig
            {
                Pool = ["Adele", "Brian", "Charlie"],
                Assignments = new Dictionary<string, List<string>>
                {
                    ["alice"] = ["Adele", "Brian", "Charlie"]
                }
            }
        };

        var result = AgentClaimValidator.FindFirstFree("alice", config,
            name => name == "Brian");

        Assert.Equal("Brian", result);
    }

    [Fact]
    public void FindFirstFree_ReturnsNull_WhenNoneFree()
    {
        var config = new DydoConfig
        {
            Agents = new AgentsConfig
            {
                Pool = ["Adele"],
                Assignments = new Dictionary<string, List<string>>
                {
                    ["alice"] = ["Adele"]
                }
            }
        };

        var result = AgentClaimValidator.FindFirstFree("alice", config,
            _ => false);

        Assert.Null(result);
    }
}

public class ConfigFactoryTests
{
    [Fact]
    public void CreateDefault_SetsVersion1()
    {
        var config = ConfigFactory.CreateDefault("alice");

        Assert.Equal(1, config.Version);
    }

    [Fact]
    public void CreateDefault_AssignsAllAgentsToHuman()
    {
        var config = ConfigFactory.CreateDefault("alice", 5);

        Assert.Equal(5, config.Agents.Pool.Count);
        Assert.Equal(5, config.Agents.Assignments["alice"].Count);
    }

    [Fact]
    public void CreateDefault_UsesDefaultRoot()
    {
        var config = ConfigFactory.CreateDefault("alice");

        Assert.Equal("dydo", config.Structure.Root);
    }

    [Fact]
    public void AddHuman_AssignsAvailableAgents()
    {
        var config = ConfigFactory.CreateDefault("alice", 5);
        ConfigFactory.AddHuman(config, "bob", 3);

        Assert.True(config.Agents.Assignments.ContainsKey("bob"));
    }

    [Fact]
    public void AddHuman_ExpandsPool_WhenNotEnoughAvailable()
    {
        var config = ConfigFactory.CreateDefault("alice", 3);
        var originalPoolSize = config.Agents.Pool.Count;

        ConfigFactory.AddHuman(config, "bob", 5);

        Assert.True(config.Agents.Pool.Count > originalPoolSize);
        Assert.Equal(5, config.Agents.Assignments["bob"].Count);
    }

    [Fact]
    public void AddHuman_UsesUnassignedAgentsFirst()
    {
        var config = new DydoConfig
        {
            Agents = new AgentsConfig
            {
                Pool = ["Adele", "Brian", "Charlie", "Dexter"],
                Assignments = new Dictionary<string, List<string>>
                {
                    ["alice"] = ["Adele", "Brian"]
                }
            }
        };

        ConfigFactory.AddHuman(config, "bob", 2);

        var bobAgents = config.Agents.Assignments["bob"];
        Assert.Contains("Charlie", bobAgents);
        Assert.Contains("Dexter", bobAgents);
    }
}
