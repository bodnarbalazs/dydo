namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class PathPermissionCheckerTests
{
    private readonly string _basePath;
    private readonly FakeConfigServiceForPPC _configService;

    public PathPermissionCheckerTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "dydo-ppc-test");
        _configService = new FakeConfigServiceForPPC(_basePath);
    }

    private PathPermissionChecker CreateChecker(Dictionary<string, RoleDefinition>? roleDefs = null) =>
        new(_basePath, _configService, roleDefs ?? new Dictionary<string, RoleDefinition>());

    private static AgentState CreateAgent(string name, string? role, List<string> writable, List<string>? readOnly = null) =>
        new()
        {
            Name = name,
            Role = role,
            WritablePaths = writable,
            ReadOnlyPaths = readOnly ?? []
        };

    #region IsPathAllowed

    [Fact]
    public void IsPathAllowed_NoRole_ReturnsFalse()
    {
        var checker = CreateChecker();
        var agent = CreateAgent("Alice", null, ["src/**"]);

        var result = checker.IsPathAllowed(agent, "src/file.cs", "write", out var error);

        Assert.False(result);
        Assert.Contains("no role set", error);
    }

    [Fact]
    public void IsPathAllowed_WritablePathMatch_ReturnsTrue()
    {
        var checker = CreateChecker();
        var agent = CreateAgent("Alice", "code-writer", ["Commands/**", "Services/**"]);

        Assert.True(checker.IsPathAllowed(agent, "Commands/MyCommand.cs", "write", out _));
    }

    [Fact]
    public void IsPathAllowed_NoWritablePaths_ReturnsFalse()
    {
        var checker = CreateChecker();
        var agent = CreateAgent("Alice", "code-writer", []);

        var result = checker.IsPathAllowed(agent, "something.cs", "write", out var error);

        Assert.False(result);
        Assert.Contains("no write permissions", error);
    }

    [Fact]
    public void IsPathAllowed_PathNotInWritable_ReturnsFalse()
    {
        var checker = CreateChecker();
        var agent = CreateAgent("Alice", "code-writer", ["src/**"]);

        var result = checker.IsPathAllowed(agent, "docs/readme.md", "write", out var error);

        Assert.False(result);
        Assert.Contains("cannot write", error);
    }

    [Fact]
    public void IsPathAllowed_ReadOnlyPathBlocked_ReturnsFalse()
    {
        var checker = CreateChecker();
        var agent = CreateAgent("Alice", "code-writer", ["src/**"], readOnly: ["dydo/**"]);

        var result = checker.IsPathAllowed(agent, "dydo/config.json", "write", out var error);

        Assert.False(result);
        Assert.Contains("cannot write", error);
    }

    [Fact]
    public void IsPathAllowed_ReadOnlyWildcard_BlocksUnlessWritable()
    {
        var checker = CreateChecker();
        var agent = CreateAgent("Alice", "code-writer", ["src/**"], readOnly: ["**"]);

        var result = checker.IsPathAllowed(agent, "src/file.cs", "write", out _);

        Assert.True(result);
    }

    [Fact]
    public void IsPathAllowed_RoledPathGetsRestrictionMessage()
    {
        var roleDefs = new Dictionary<string, RoleDefinition>
        {
            ["code-writer"] = new()
            {
                Name = "code-writer",
                Description = "Writes code",
                WritablePaths = ["src/**"],
                ReadOnlyPaths = [],
                TemplateFile = "mode-code-writer.template.md",
                DenialHint = "Code-writer can only edit source files."
            }
        };
        var checker = CreateChecker(roleDefs);
        var agent = CreateAgent("Alice", "code-writer", ["src/**"]);

        checker.IsPathAllowed(agent, "docs/readme.md", "write", out var error);

        Assert.Contains("Code-writer can only edit source files", error);
    }

    [Fact]
    public void IsPathAllowed_ClaudePlansPath_GetsSpecialNudge()
    {
        var checker = CreateChecker();
        var agent = CreateAgent("Alice", "code-writer", ["src/**"]);

        checker.IsPathAllowed(agent, ".claude/plans/my-plan.md", "write", out var error);

        Assert.Contains("planner mode", error);
    }

    [Fact]
    public void IsPathAllowed_AbsolutePath_ConvertsToRelative()
    {
        var checker = CreateChecker();
        var agent = CreateAgent("Alice", "code-writer", ["Commands/**"]);
        var absPath = Path.Combine(_basePath, "Commands", "MyCmd.cs");

        Assert.True(checker.IsPathAllowed(agent, absPath, "write", out _));
    }

    [Fact]
    public void IsPathAllowed_WorktreeBasePath_ResolvesAbsolutePathCorrectly()
    {
        // Simulate a worktree CWD
        var mainRoot = Path.Combine(Path.GetTempPath(), $"dydo-ppc-wt-{Guid.NewGuid():N}");
        var worktreeBase = Path.Combine(mainRoot, "dydo", "_system", ".local", "worktrees", "fix-auth");
        var configService = new FakeConfigServiceForPPC(worktreeBase);
        var checker = new PathPermissionChecker(worktreeBase, configService, new Dictionary<string, RoleDefinition>());
        var agent = CreateAgent("Alice", "code-writer", ["Commands/**"]);

        // Absolute path in the main project (after NormalizeWorktreePath)
        var absPath = Path.Combine(mainRoot, "Commands", "MyCmd.cs");

        Assert.True(checker.IsPathAllowed(agent, absPath, "write", out _));
    }

    #endregion

    #region MatchesGlob

    [Theory]
    [InlineData("src/file.cs", "src/**", true)]
    [InlineData("src/nested/file.cs", "src/**", true)]
    [InlineData("other/file.cs", "src/**", false)]
    [InlineData("Commands/Foo.cs", "Commands/*", true)]
    [InlineData("Commands/Sub/Foo.cs", "Commands/*", false)]
    // **/ prefix should optionally match root-level paths
    [InlineData("secrets.json", "**/secrets.json", true)]
    [InlineData("config/secrets.json", "**/secrets.json", true)]
    [InlineData("deep/nested/secrets.json", "**/secrets.json", true)]
    // ? should match a single character
    [InlineData("file1.cs", "file?.cs", true)]
    [InlineData("fileAB.cs", "file?.cs", false)]
    public void MatchesGlob_VariousPatterns(string path, string pattern, bool expected)
    {
        Assert.Equal(expected, PathPermissionChecker.MatchesGlob(path, pattern));
    }

    #endregion

    private class FakeConfigServiceForPPC : IConfigService
    {
        private readonly string _basePath;
        public FakeConfigServiceForPPC(string basePath) => _basePath = basePath;
        public string? FindConfigFile(string? startPath = null) => null;
        public DydoConfig? LoadConfig(string? startPath = null) => null;
        public void SaveConfig(DydoConfig config, string path) { }
        public string? GetHumanFromEnv() => "tester";
        public string? GetProjectRoot(string? startPath = null) => _basePath;
        public string GetDydoRoot(string? startPath = null) => Path.Combine(_basePath, "dydo");
        public string GetAgentsPath(string? startPath = null) => Path.Combine(_basePath, "agents");
        public string GetDocsPath(string? startPath = null) => Path.Combine(_basePath, "docs");
        public string GetTasksPath(string? startPath = null) => Path.Combine(_basePath, "tasks");
        public string GetAuditPath(string? startPath = null) => Path.Combine(_basePath, "audit");
        public string GetChangelogPath(string? startPath = null) => Path.Combine(_basePath, "changelog");
        public string GetIssuesPath(string? startPath = null) => Path.Combine(_basePath, "issues");
        public (bool CanClaim, string? Error) ValidateAgentClaim(string agentName, string? humanName, DydoConfig? config) => (true, null);
    }
}
