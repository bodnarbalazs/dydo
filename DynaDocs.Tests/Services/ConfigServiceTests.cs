namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class ConfigServiceTests : IDisposable
{
    private readonly string _testDir;

    public ConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-config-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);

        // Create minimal dydo.json
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
}
