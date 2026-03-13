namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

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
