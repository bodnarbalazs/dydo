namespace DynaDocs.Tests.Rules;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Rules;
using DynaDocs.Services;

public sealed class LegacyPmRecordRuleTests : IDisposable
{
    private readonly string _dydoRoot = Path.Combine(Path.GetTempPath(), $"legacy-pm-rule-{Guid.NewGuid():N}");

    public LegacyPmRecordRuleTests()
    {
        Directory.CreateDirectory(_dydoRoot);
    }

    public void Dispose()
    {
        Directory.Delete(_dydoRoot, recursive: true);
    }

    [Fact]
    public void Validate_AcceptsAppliedRetainedFutureFeature()
    {
        var path = "project/future-features/idea.md";
        WriteManifest((path, "applied", "retain-normalize", path));
        var doc = CreateDoc(path, "type: concept");

        Assert.Empty(CreateRule().Validate(doc, [doc], _dydoRoot));
    }

    [Fact]
    public void Validate_AcceptsPendingRetainedFutureFeature()
    {
        var path = "project/future-features/idea.md";
        WriteManifest((path, "pending", "retain-normalize", path));
        var doc = CreateDoc(path, "type: concept");

        Assert.Empty(CreateRule().Validate(doc, [doc], _dydoRoot));
    }

    [Fact]
    public void Validate_RejectsReintroducedAppliedManifestPath()
    {
        var path = "project/historical-release.md";
        WriteManifest((path, "applied", "remove-historical", null));
        var doc = CreateDoc(path, "type: concept");

        Assert.Single(CreateRule().Validate(doc, [doc], _dydoRoot));
    }

    [Fact]
    public void Validate_RejectsNewDirectChildOfRetiredCorpusDirectory()
    {
        WriteManifest();
        var doc = CreateDoc("project/tasks/new-work.md", "type: concept");

        Assert.Single(CreateRule().Validate(doc, [doc], _dydoRoot));
    }

    [Fact]
    public void Validate_RejectsLegacyFrontmatterOutsideRetiredDirectory()
    {
        WriteManifest();
        var doc = CreateDoc("guides/not-work.md", "type: issue");

        Assert.Single(CreateRule().Validate(doc, [doc], _dydoRoot));
    }

    [Fact]
    public void CheckDocValidator_RegistersLegacyPmRecordRule()
    {
        WriteManifest();
        var guides = Path.Combine(_dydoRoot, "guides");
        Directory.CreateDirectory(guides);
        File.WriteAllText(Path.Combine(guides, "not-work.md"), "---\narea: guides\ntype: issue\n---\n\n# Not Work\n\nA valid summary.\n");

        var result = CheckDocValidator.Validate(_dydoRoot);

        Assert.Contains(result.Violations, violation => violation.RuleName == "LegacyPmRecord" && violation.FilePath == "guides/not-work.md");
    }

    [Fact]
    public void Validate_AcceptsOrdinaryDurableKnowledge()
    {
        WriteManifest();
        var doc = CreateDoc("guides/testing.md", "type: guide");

        Assert.Empty(CreateRule().Validate(doc, [doc], _dydoRoot));
    }

    [Fact]
    public void Validate_IsInactiveWhenManifestDoesNotExist()
    {
        var doc = CreateDoc("project/tasks/new-work.md", "type: task");

        Assert.Empty(CreateRule().Validate(doc, [doc], _dydoRoot));
    }

    private LegacyPmRecordRule CreateRule() => new(new LegacyPmManifestService(_dydoRoot));

    private static DocFile CreateDoc(string relativePath, string frontmatter)
    {
        return new DocFile
        {
            FilePath = Path.Combine("/base", relativePath),
            RelativePath = relativePath,
            FileName = Path.GetFileName(relativePath),
            Content = $"---\narea: project\n{frontmatter}\n---\n\n# Test\n"
        };
    }

    private void WriteManifest(params (string Path, string State, string Disposition, string? Target)[] records)
    {
        var rows = records.Select(record => new
        {
            path = $"dydo/{record.Path}",
            executionState = record.State,
            finalDisposition = record.Disposition,
            target = record.Target == null ? null : new { kind = "retained-path", value = $"dydo/{record.Target}" }
        });
        var directory = Path.Combine(_dydoRoot, "project", "migrations");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "3.0-pm-records.json"), System.Text.Json.JsonSerializer.Serialize(new { records = rows }));
    }
}
