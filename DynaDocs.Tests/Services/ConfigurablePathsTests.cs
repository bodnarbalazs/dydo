namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class ConfigurablePathsTests : IDisposable
{
    private readonly string _testDir;

    public ConfigurablePathsTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-paths-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private void WriteConfig(string json)
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), json);
    }

    private void SetupDydoStructure()
    {
        var dydoRoot = Path.Combine(_testDir, "dydo");
        Directory.CreateDirectory(Path.Combine(dydoRoot, "agents"));
        Directory.CreateDirectory(Path.Combine(dydoRoot, "understand"));
        Directory.CreateDirectory(Path.Combine(dydoRoot, "guides"));
        Directory.CreateDirectory(Path.Combine(dydoRoot, "project", "tasks"));

        // Minimal must-read files
        File.WriteAllText(Path.Combine(dydoRoot, "understand", "about.md"),
            "---\nmust-read: true\n---\n# About\n");
        File.WriteAllText(Path.Combine(dydoRoot, "understand", "architecture.md"),
            "---\nmust-read: true\n---\n# Architecture\n");
        File.WriteAllText(Path.Combine(dydoRoot, "guides", "coding-standards.md"),
            "---\nmust-read: true\n---\n# Standards\n");
    }

    [Fact]
    public void DefaultPaths_WhenNoPathsSection()
    {
        WriteConfig("""
            {
              "version": 1
            }
            """);

        var config = new ConfigService().LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.Equal(["src/**"], config!.Paths.Source);
        Assert.Equal(["tests/**"], config.Paths.Tests);
    }

    [Fact]
    public void CustomPaths_LoadedFromConfig()
    {
        WriteConfig("""
            {
              "version": 1,
              "paths": {
                "source": ["Commands/**", "Services/**"],
                "tests": ["DynaDocs.Tests/**"]
              }
            }
            """);

        var config = new ConfigService().LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.Equal(["Commands/**", "Services/**"], config!.Paths.Source);
        Assert.Equal(["DynaDocs.Tests/**"], config.Paths.Tests);
    }

    [Fact]
    public void AgentRegistry_BuildsPermissionsFromConfig()
    {
        WriteConfig("""
            {
              "version": 1,
              "paths": {
                "source": ["Commands/**", "Services/**"],
                "tests": ["MyTests/**"]
              }
            }
            """);
        SetupDydoStructure();

        // AgentRegistry reads config in constructor and builds role permissions
        var registry = new AgentRegistry(_testDir);

        // Verify the config was loaded with custom paths
        Assert.NotNull(registry.Config);
        Assert.Equal(["Commands/**", "Services/**"], registry.Config!.Paths.Source);
        Assert.Equal(["MyTests/**"], registry.Config.Paths.Tests);
    }

    [Fact]
    public void PathsConfig_DefaultsPreserved_WhenNotInJson()
    {
        var config = new PathsConfig();
        Assert.Equal(["src/**"], config.Source);
        Assert.Equal(["tests/**"], config.Tests);
    }

}
