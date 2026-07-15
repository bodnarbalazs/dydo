namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

/// <summary>
/// Provenance stamped onto artifacts from the claiming agent's session (<see cref="ArtifactProvenance.FromSession"/>).
/// CWD is switched to the temp project so ConfigService.LoadConfig() discovers the fixture's dydo.json.
/// </summary>
[Collection("Integration")]
public class ArtifactProvenanceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dydoDir;
    private readonly string _originalDir;

    public ArtifactProvenanceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-provenance-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoDir = Path.Combine(_testDir, "dydo");
        Directory.CreateDirectory(Path.Combine(_dydoDir, "agents"));
        _originalDir = Environment.CurrentDirectory;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        if (Directory.Exists(_testDir))
        {
            for (var i = 0; i < 3; i++)
            {
                try { Directory.Delete(_testDir, true); return; }
                catch (IOException) when (i < 2) { Thread.Sleep(50); }
            }
        }
    }

    private AgentRegistry Setup(bool withModels)
    {
        var models = withModels
            ? """, "models": { "tiers": { "anthropic": { "strong": "claude-fable-5" } } }"""
            : "";
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"),
            $$"""{ "version": 1, "structure": { "root": "dydo" }, "agents": { "pool": ["Adele"], "assignments": { "testuser": ["Adele"] } }{{models}} }""");
        Environment.CurrentDirectory = _testDir;
        return new AgentRegistry(_testDir);
    }

    private void WriteSession(string agent, string host, string model)
    {
        var dir = Path.Combine(_dydoDir, "agents", agent);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ".session"),
            $$"""{"Agent":"{{agent}}","SessionId":"sess-1","Host":"{{host}}","Model":"{{model}}","Claimed":"2026-07-15T00:00:00Z","ClaimedPid":123}""");
    }

    [Fact]
    public void FromSession_NoSessionFile_ReturnsNull()
    {
        var registry = Setup(withModels: false);
        Assert.Null(ArtifactProvenance.FromSession(registry, "Adele"));
    }

    [Fact]
    public void FromSession_WithSession_AndModels_StampsAgentAndVendor()
    {
        var registry = Setup(withModels: true);
        WriteSession("Adele", "claude", "claude-fable-5");

        var prov = ArtifactProvenance.FromSession(registry, "Adele");

        Assert.NotNull(prov);
        Assert.Equal("Adele", prov!.Agent);
        Assert.Equal("claude", prov.Vendor);
    }

    [Fact]
    public void FromSession_WithSession_NoModelsSection_StillStampsProvenance()
    {
        var registry = Setup(withModels: false);
        WriteSession("Adele", "claude", "claude-fable-5");

        Assert.NotNull(ArtifactProvenance.FromSession(registry, "Adele"));
    }
}
