namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

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

    [Fact]
    public void CreateDefault_IncludesDefaultNudges()
    {
        var config = ConfigFactory.CreateDefault("alice");

        Assert.NotEmpty(config.Nudges);
        Assert.Equal(ConfigFactory.DefaultNudges.Count, config.Nudges.Count);
        Assert.All(config.Nudges, n => Assert.False(string.IsNullOrEmpty(n.Pattern)));
    }

    [Fact]
    public void EnsureDefaultNudges_AddsToEmptyList()
    {
        var config = new DydoConfig();

        var added = ConfigFactory.EnsureDefaultNudges(config);

        Assert.Equal(ConfigFactory.DefaultNudges.Count, added);
        Assert.Equal(ConfigFactory.DefaultNudges.Count, config.Nudges.Count);
    }

    [Fact]
    public void EnsureDefaultNudges_SkipsAlreadyPresent()
    {
        var config = ConfigFactory.CreateDefault("alice");
        var originalCount = config.Nudges.Count;

        var added = ConfigFactory.EnsureDefaultNudges(config);

        Assert.Equal(0, added);
        Assert.Equal(originalCount, config.Nudges.Count);
    }

    [Fact]
    public void EnsureDefaultNudges_PreservesCustomNudges()
    {
        var config = new DydoConfig
        {
            Nudges = [new NudgeConfig { Pattern = "custom-pattern", Message = "Custom", Severity = "block" }]
        };

        var added = ConfigFactory.EnsureDefaultNudges(config);

        Assert.Equal(ConfigFactory.DefaultNudges.Count, added);
        Assert.Equal(ConfigFactory.DefaultNudges.Count + 1, config.Nudges.Count);
        Assert.Contains(config.Nudges, n => n.Pattern == "custom-pattern");
    }
}
