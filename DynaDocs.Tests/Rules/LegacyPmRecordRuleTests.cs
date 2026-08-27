namespace DynaDocs.Tests.Rules;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Rules;
using DynaDocs.Services;

public sealed class LegacyPmRecordRuleTests : IDisposable
{
    private readonly string _dydoRoot = Path.Combine(
        Path.GetTempPath(),
        $"legacy-pm-rule-{Guid.NewGuid():N}");

    public LegacyPmRecordRuleTests()
    {
        Directory.CreateDirectory(_dydoRoot);
    }

    public void Dispose()
    {
        Directory.Delete(_dydoRoot, recursive: true);
    }

    public static TheoryData<string> RetainedNonRecordPaths => new()
    {
        DydoProjectPath("campaigns", "_index.md"),
        DydoProjectPath("sprints", "_index.md"),
        DydoProjectPath("slices", "_index.md"),
        DydoProjectPath("tasks", "_index.md"),
        DydoProjectPath("issues", "_index.md"),
        DydoProjectPath("backlog", "_index.md"),
        DydoProjectPath("campaigns", "_campaigns.md"),
        DydoProjectPath("sprints", "_sprints.md"),
        DydoProjectPath("slices", "_slices.md"),
        DydoProjectPath("tasks", "_tasks.md"),
        DydoProjectPath("issues", "_issues.md"),
        DydoProjectPath("backlog", "_backlog.md")
    };

    [Fact]
    public void Validate_AcceptsManifestBackedRecord()
    {
        var path = DydoProjectPath("issues", "resolved/known.md");
        WriteManifest((path, "pending"));
        var rule = CreateRule();
        var doc = CreateDoc(path, "type: issue");

        var violations = rule.Validate(doc, [doc], _dydoRoot);

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(RetainedNonRecordPaths))]
    public void Validate_AcceptsEachRetainedNonRecordPath(string path)
    {
        WriteManifest();
        var rule = CreateRule();
        var doc = CreateDoc(path, "type: hub");

        var violations = rule.Validate(doc, [doc], _dydoRoot);

        Assert.Empty(violations);
    }

    [Fact]
    public void Validate_RejectsUnknownUnderscoreFileInCanonicalDirectory()
    {
        WriteManifest();
        var rule = CreateRule();
        var doc = CreateDoc(DydoProjectPath("tasks", "_unknown.md"), "type: folder-meta");

        var violation = Assert.Single(rule.Validate(doc, [doc], _dydoRoot));

        Assert.Equal(ViolationSeverity.Error, violation.Severity);
        Assert.Contains("allow-set", violation.Message);
    }

    [Fact]
    public void Validate_RejectsLegacyTypedRecordOutsideCanonicalDirectory()
    {
        WriteManifest();
        var rule = CreateRule();
        var doc = CreateDoc("guides/not-work.md", "type: issue");

        var violation = Assert.Single(rule.Validate(doc, [doc], _dydoRoot));

        Assert.Equal("guides/not-work.md", violation.FilePath);
    }

    [Fact]
    public void Validate_RejectsLegacyTypeWithCanonicalWhitespaceTolerantOpener()
    {
        WriteManifest();
        var rule = CreateRule();
        var doc = CreateDoc("guides/not-work.md", "type: issue", "--- ");

        Assert.Single(rule.Validate(doc, [doc], _dydoRoot));
    }

    [Fact]
    public void CheckDocValidator_RegistersLegacyPmRecordRule()
    {
        WriteManifest();
        var guides = Path.Combine(_dydoRoot, "guides");
        Directory.CreateDirectory(guides);
        File.WriteAllText(
            Path.Combine(guides, "not-work.md"),
            "--- \narea: guides\ntype: issue\n---\n\n# Not Work\n\nA valid summary.\n");

        var result = CheckDocValidator.Validate(_dydoRoot);

        Assert.Contains(result.Violations, violation =>
            violation.RuleName == "LegacyPmRecord" &&
            violation.FilePath == "guides/not-work.md");
    }

    [Fact]
    public void ValidateFolder_RejectsMissingPendingManifestPath()
    {
        WriteManifest((DydoProjectPath("tasks", "missing.md"), "pending"));
        var rule = CreateRule();

        var violation = Assert.Single(rule.ValidateFolder(_dydoRoot, [], _dydoRoot));

        Assert.Equal(RepoProjectPath("tasks", "missing.md"), violation.FilePath);
        Assert.Contains("does not resolve", violation.Message);
    }

    [Fact]
    public void Validate_RejectsOrdinaryNonManifestRecordCandidate()
    {
        WriteManifest();
        var rule = CreateRule();
        var doc = CreateDoc(DydoProjectPath("tasks", "new-work.md"), "type: context");

        Assert.Single(rule.Validate(doc, [doc], _dydoRoot));
    }

    [Fact]
    public void Validate_RejectsAppliedManifestRecord()
    {
        var path = DydoProjectPath("issues", "resolved/applied.md");
        WriteManifest((path, "applied"));
        var rule = CreateRule();
        var doc = CreateDoc(path, "type: issue");

        Assert.Single(rule.Validate(doc, [doc], _dydoRoot));
    }

    [Fact]
    public void Validate_RejectsAppliedManifestRecordWithoutLegacyFrontmatter()
    {
        const string path = "project/historical-release.md";
        WriteManifest((path, "applied"));
        var rule = CreateRule();
        var doc = CreateDoc(path, "type: changelog");

        Assert.Single(rule.Validate(doc, [doc], _dydoRoot));
    }

    [Fact]
    public void Validate_AcceptsOrdinaryDurableKnowledge()
    {
        WriteManifest();
        var rule = CreateRule();
        var doc = CreateDoc("guides/testing.md", "type: guide");

        Assert.Empty(rule.Validate(doc, [doc], _dydoRoot));
    }

    [Fact]
    public void Validate_IsInactiveWhenManifestDoesNotExist()
    {
        var rule = CreateRule();
        var doc = CreateDoc(DydoProjectPath("tasks", "new-work.md"), "type: task");

        Assert.Empty(rule.Validate(doc, [doc], _dydoRoot));
        Assert.Empty(rule.ValidateFolder(_dydoRoot, [doc], _dydoRoot));
    }

    private LegacyPmRecordRule CreateRule()
    {
        return new LegacyPmRecordRule(new LegacyPmManifestService(_dydoRoot));
    }

    private static DocFile CreateDoc(
        string relativePath,
        string frontmatter,
        string opener = "---")
    {
        return new DocFile
        {
            FilePath = Path.Combine("/base", relativePath),
            RelativePath = relativePath,
            FileName = Path.GetFileName(relativePath),
            Content = $"{opener}\narea: project\n{frontmatter}\n---\n\n# Test\n"
        };
    }

    private void WriteManifest(params (string Path, string State)[] records)
    {
        var directory = Path.Combine(_dydoRoot, "project", "migrations");
        Directory.CreateDirectory(directory);
        var rows = string.Join(",", records.Select(record =>
        {
            var repoPath = $"dydo/{record.Path}";
            return $$"""{"path":"{{repoPath}}","executionState":"{{record.State}}"}""";
        }));
        File.WriteAllText(
            Path.Combine(directory, "3.0-pm-records.json"),
            $$"""{"records":[{{rows}}]}""");
    }

    private static string DydoProjectPath(string folder, string fileName)
    {
        return string.Join('/', "project", folder, fileName);
    }

    private static string RepoProjectPath(string folder, string fileName)
    {
        return string.Join('/', "dydo", "project", folder, fileName);
    }
}
