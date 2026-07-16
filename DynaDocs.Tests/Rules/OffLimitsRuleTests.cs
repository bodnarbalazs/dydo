namespace DynaDocs.Tests.Rules;

using DynaDocs.Models;
using DynaDocs.Rules;
using DynaDocs.Services;

[Collection("Integration")]
public class OffLimitsRuleTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public OffLimitsRuleTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-offlimitsrule-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo"));
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"),
            """{"version":1,"structure":{"root":"dydo","tasks":"project/tasks"}}""");

        _originalDir = Environment.CurrentDirectory;
        Environment.CurrentDirectory = _testDir;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    private sealed class FakeOffLimitsService : IOffLimitsService
    {
        public bool FileExists { get; init; } = true;
        public List<FormatValidationIssue> FormatIssues { get; init; } = [];
        public List<string> MissingPaths { get; init; } = [];

        public void LoadPatterns(string? basePath = null) { }
        public string? IsPathOffLimits(string path) => null;
        public IReadOnlyList<string> Patterns => [];
        public IReadOnlyList<string> WhitelistPatterns => [];
        public IEnumerable<string> ValidateLiteralPaths(string basePath) => MissingPaths;
        public bool OffLimitsFileExists(string? basePath = null) => FileExists;
        public IEnumerable<FormatValidationIssue> ValidateFormat(string? basePath = null) => FormatIssues;
    }

    private string DydoRoot => Path.Combine(_testDir, "dydo");

    [Fact]
    public void ValidateFolder_MissingOffLimitsFile_YieldsWarning()
    {
        var rule = new OffLimitsRule(new FakeOffLimitsService { FileExists = false });

        var violations = rule.ValidateFolder(DydoRoot, [], _testDir).ToList();

        var v = Assert.Single(violations);
        Assert.Contains("not found", v.Message);
        Assert.Equal(ViolationSeverity.Warning, v.Severity);
    }

    [Fact]
    public void ValidateFolder_FormatIssues_MapToSeverities()
    {
        var rule = new OffLimitsRule(new FakeOffLimitsService
        {
            FormatIssues =
            [
                new FormatValidationIssue("Unclosed code block", IsError: true),
                new FormatValidationIssue("Duplicate pattern: x", IsError: false),
            ]
        });

        var violations = rule.ValidateFolder(DydoRoot, [], _testDir).ToList();

        Assert.Equal(2, violations.Count);
        Assert.Equal(ViolationSeverity.Error, violations[0].Severity);
        Assert.Equal(ViolationSeverity.Warning, violations[1].Severity);
    }

    [Fact]
    public void ValidateFolder_MissingLiteralPaths_YieldWarnings()
    {
        var rule = new OffLimitsRule(new FakeOffLimitsService
        {
            MissingPaths = ["dydo/index.md"]
        });

        var violations = rule.ValidateFolder(DydoRoot, [], _testDir).ToList();

        var v = Assert.Single(violations);
        Assert.Contains("does not exist", v.Message);
        Assert.Contains("dydo/index.md", v.Message);
        Assert.Equal(ViolationSeverity.Warning, v.Severity);
    }

    [Fact]
    public void ValidateFolder_NonRootFolder_YieldsNothing()
    {
        var sub = Path.Combine(DydoRoot, "project");
        Directory.CreateDirectory(sub);
        var rule = new OffLimitsRule(new FakeOffLimitsService { FileExists = false });

        var violations = rule.ValidateFolder(sub, [], _testDir).ToList();

        Assert.Empty(violations);
    }
}
