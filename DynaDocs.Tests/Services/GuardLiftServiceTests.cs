namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class GuardLiftServiceTests : IDisposable
{
    private readonly string _testDir;

    public GuardLiftServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-guardlift-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [Fact]
    public void Lift_AgentDirectoryMissing_CreatesDirectoryAndWritesMarker()
    {
        // In a worktree where the agents junction is absent, the agent directory
        // doesn't exist. Lift must create it rather than crashing.
        var service = new GuardLiftService(_testDir);

        service.Lift("Brian", "testuser", minutes: null);

        Assert.True(service.IsLifted("Brian"));
    }

    [Fact]
    public void Lift_AgentDirectoryExists_StillWorks()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo", "agents", "Brian"));
        var service = new GuardLiftService(_testDir);

        service.Lift("Brian", "testuser", minutes: null);

        Assert.True(service.IsLifted("Brian"));
    }
}
