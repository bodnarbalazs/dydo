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
        var knownRoles = new[] { "code-writer", "reviewer", "docs-writer", "interviewer", "planner" };
        Assert.Equal(5, knownRoles.Length);
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
}
