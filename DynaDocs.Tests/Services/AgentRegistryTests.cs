namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

/// <summary>
/// Tests for the minimal AgentRegistry that survives the DR-041 carve: it exposes only the
/// loaded config (for guard nudges) and the agents-root path (where the guard writes warn-nudge
/// markers). The claim/roster/identity/session/wait/message/resume/agent-state machinery was
/// deleted.
/// </summary>
public class AgentRegistryTests : IDisposable
{
    private readonly string _testDir;

    public AgentRegistryTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-registry-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
        GC.SuppressFinalize(this);
    }

    private AgentRegistry Registry() => new(_testDir);

    [Fact]
    public void WorkspacePath_ResolvesUnderAgentsRoot()
    {
        Assert.EndsWith(Path.Combine("dydo", "agents"), Registry().WorkspacePath);
    }

    [Fact]
    public void Config_IsNull_WhenNoDydoJson()
    {
        Assert.Null(Registry().Config);
    }

    [Fact]
    public void Config_LoadsNudges_WhenDydoJsonPresent()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"),
            """{"version":1,"nudges":[{"pattern":"foo","message":"bar","severity":"warn"}]}""");

        var config = Registry().Config;
        Assert.NotNull(config);
        Assert.Contains(config!.Nudges, n => n.Pattern == "foo");
    }
}
