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
              "version": 1,
              "agents": { "pool": ["Adele"], "assignments": { "test": ["Adele"] } }
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
              },
              "agents": { "pool": ["Adele"], "assignments": { "test": ["Adele"] } }
            }
            """);

        var config = new ConfigService().LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.Equal(["Commands/**", "Services/**"], config!.Paths.Source);
        Assert.Equal(["DynaDocs.Tests/**"], config.Paths.Tests);
    }

    [Fact]
    public void BuildRolePermissions_DefaultPaths_MatchesOriginalBehavior()
    {
        var perms = AgentRegistry.BuildRolePermissions(["src/**"], ["tests/**"]);

        Assert.Contains("src/**", perms["code-writer"].Writable);
        Assert.Contains("tests/**", perms["code-writer"].Writable);
        Assert.Contains("dydo/agents/{self}/**", perms["code-writer"].Writable);

        Assert.Contains("src/**", perms["co-thinker"].ReadOnly);
        Assert.Contains("tests/**", perms["co-thinker"].ReadOnly);

        Assert.Contains("src/**", perms["planner"].ReadOnly);
        Assert.DoesNotContain("tests/**", perms["planner"].ReadOnly);

        Assert.Contains("tests/**", perms["test-writer"].Writable);
        Assert.Contains("src/**", perms["test-writer"].ReadOnly);
    }

    [Fact]
    public void BuildRolePermissions_CustomPaths_AppliedToAllRoles()
    {
        var source = new List<string> { "Commands/**", "Services/**", "Models/**" };
        var tests = new List<string> { "DynaDocs.Tests/**" };
        var perms = AgentRegistry.BuildRolePermissions(source, tests);

        // code-writer gets all source + test paths as writable
        Assert.Contains("Commands/**", perms["code-writer"].Writable);
        Assert.Contains("Services/**", perms["code-writer"].Writable);
        Assert.Contains("Models/**", perms["code-writer"].Writable);
        Assert.Contains("DynaDocs.Tests/**", perms["code-writer"].Writable);
        Assert.DoesNotContain("src/**", perms["code-writer"].Writable);

        // co-thinker gets source + test as read-only
        Assert.Contains("Commands/**", perms["co-thinker"].ReadOnly);
        Assert.Contains("DynaDocs.Tests/**", perms["co-thinker"].ReadOnly);

        // docs-writer gets source + test as read-only
        Assert.Contains("Commands/**", perms["docs-writer"].ReadOnly);
        Assert.Contains("DynaDocs.Tests/**", perms["docs-writer"].ReadOnly);

        // planner gets source as read-only (not tests)
        Assert.Contains("Commands/**", perms["planner"].ReadOnly);
        Assert.DoesNotContain("DynaDocs.Tests/**", perms["planner"].ReadOnly);

        // test-writer gets test paths as writable, source as read-only
        Assert.Contains("DynaDocs.Tests/**", perms["test-writer"].Writable);
        Assert.Contains("Commands/**", perms["test-writer"].ReadOnly);
        Assert.DoesNotContain("Commands/**", perms["test-writer"].Writable);
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
              },
              "agents": { "pool": ["Adele"], "assignments": { "testuser": ["Adele"] } }
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

    [Fact]
    public void FormatPathsList_SinglePath()
    {
        var result = TemplateGenerator.FormatPathsList(["src/**"]);
        Assert.Equal("`src/**`", result);
    }

    [Fact]
    public void FormatPathsList_MultiplePaths()
    {
        var result = TemplateGenerator.FormatPathsList(["Commands/**", "Services/**", "Models/**"]);
        Assert.Equal("`Commands/**`, `Services/**`, `Models/**`", result);
    }

    [Fact]
    public void GenerateModeFile_UsesConfiguredPaths()
    {
        var content = TemplateGenerator.GenerateModeFile("TestAgent", "code-writer",
            sourcePaths: ["Commands/**", "Services/**"],
            testPaths: ["MyTests/**"]);

        Assert.Contains("`Commands/**`", content);
        Assert.Contains("`Services/**`", content);
        Assert.Contains("`MyTests/**`", content);
        Assert.DoesNotContain("src/**", content);
    }

    [Fact]
    public void GenerateModeFile_TestWriterUsesConfiguredPaths()
    {
        var content = TemplateGenerator.GenerateModeFile("TestAgent", "test-writer",
            sourcePaths: ["Commands/**"],
            testPaths: ["MyTests/**"]);

        Assert.Contains("`MyTests/**`", content);
        Assert.Contains("`Commands/**`", content);
    }

    [Fact]
    public void GenerateModeFile_DefaultPaths_WhenNotProvided()
    {
        var content = TemplateGenerator.GenerateModeFile("TestAgent", "code-writer");

        Assert.Contains("`src/**`", content);
        Assert.Contains("`tests/**`", content);
    }
}
