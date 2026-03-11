namespace DynaDocs.Tests.Models;

using DynaDocs.Models;

public class AgentsConfigTests
{
    private static AgentsConfig CreateConfig() => new()
    {
        Pool = ["Adele", "Brian", "Charlie"],
        Assignments = new Dictionary<string, List<string>>
        {
            ["alice"] = ["Adele", "Brian"],
            ["Bob"] = ["Charlie"]
        }
    };

    [Fact]
    public void GetHumanForAgent_ReturnsHuman_WhenAssigned()
    {
        var config = CreateConfig();

        Assert.Equal("alice", config.GetHumanForAgent("Adele"));
        Assert.Equal("Bob", config.GetHumanForAgent("Charlie"));
    }

    [Fact]
    public void GetHumanForAgent_IsCaseInsensitive()
    {
        var config = CreateConfig();

        Assert.Equal("alice", config.GetHumanForAgent("adele"));
        Assert.Equal("alice", config.GetHumanForAgent("BRIAN"));
    }

    [Fact]
    public void GetHumanForAgent_ReturnsNull_WhenNotAssigned()
    {
        var config = CreateConfig();

        Assert.Null(config.GetHumanForAgent("Dexter"));
    }

    [Fact]
    public void GetHumanForAgent_ReturnsNull_WhenNoAssignments()
    {
        var config = new AgentsConfig();

        Assert.Null(config.GetHumanForAgent("Adele"));
    }

    [Fact]
    public void GetAgentsForHuman_ReturnsAgents_ExactMatch()
    {
        var config = CreateConfig();

        var agents = config.GetAgentsForHuman("alice");

        Assert.Equal(2, agents.Count);
        Assert.Contains("Adele", agents);
        Assert.Contains("Brian", agents);
    }

    [Fact]
    public void GetAgentsForHuman_FallsBackToCaseInsensitiveMatch()
    {
        var config = CreateConfig();

        var agents = config.GetAgentsForHuman("ALICE");

        Assert.Equal(2, agents.Count);
        Assert.Contains("Adele", agents);
    }

    [Fact]
    public void GetAgentsForHuman_ReturnsEmptyList_WhenNotFound()
    {
        var config = CreateConfig();

        var agents = config.GetAgentsForHuman("unknown");

        Assert.Empty(agents);
    }

    [Fact]
    public void GetAgentsForHuman_ReturnsEmptyList_WhenNoAssignments()
    {
        var config = new AgentsConfig();

        var agents = config.GetAgentsForHuman("alice");

        Assert.Empty(agents);
    }

    [Fact]
    public void IsAgentAssignedTo_ReturnsTrue_WhenCorrectHuman()
    {
        var config = CreateConfig();

        Assert.True(config.IsAgentAssignedTo("Adele", "alice"));
    }

    [Fact]
    public void IsAgentAssignedTo_IsCaseInsensitive_ForHuman()
    {
        var config = CreateConfig();

        Assert.True(config.IsAgentAssignedTo("Adele", "ALICE"));
    }

    [Fact]
    public void IsAgentAssignedTo_ReturnsFalse_WhenDifferentHuman()
    {
        var config = CreateConfig();

        Assert.False(config.IsAgentAssignedTo("Adele", "Bob"));
    }

    [Fact]
    public void IsAgentAssignedTo_ReturnsFalse_WhenAgentNotAssigned()
    {
        var config = CreateConfig();

        Assert.False(config.IsAgentAssignedTo("Dexter", "alice"));
    }

    [Fact]
    public void Pool_DefaultsToEmptyList()
    {
        var config = new AgentsConfig();

        Assert.Empty(config.Pool);
    }

    [Fact]
    public void Assignments_DefaultsToEmptyDictionary()
    {
        var config = new AgentsConfig();

        Assert.Empty(config.Assignments);
    }
}
