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
    public void GuardEnv_LoadsConfigAndMachineLocalMarkerDir()
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

        var env = DynaDocs.Commands.GuardCommand.GuardEnv.Load(_testDir);

        Assert.NotNull(env.Config);
        Assert.Equal(["Commands/**", "Services/**"], env.Config!.Paths.Source);
        Assert.Equal(["MyTests/**"], env.Config.Paths.Tests);
        Assert.EndsWith(Path.Combine("dydo", "_system", ".local"), env.MarkerDir);
    }

    [Fact]
    public void PathsConfig_DefaultsPreserved_WhenNotInJson()
    {
        var config = new PathsConfig();
        Assert.Equal(["src/**"], config.Source);
        Assert.Equal(["tests/**"], config.Tests);
    }

}
