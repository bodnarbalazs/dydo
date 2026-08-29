namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public sealed class LegacyPmManifestServiceTests : IDisposable
{
    private readonly string _dydoRoot = Path.Combine(Path.GetTempPath(), "legacy-pm-manifest-" + Guid.NewGuid().ToString("N"));

    public LegacyPmManifestServiceTests()
    {
        Directory.CreateDirectory(_dydoRoot);
    }

    public void Dispose()
    {
        Directory.Delete(_dydoRoot, recursive: true);
    }

    [Fact]
    public void InactiveManifest_HasNoRecords()
    {
        var service = new LegacyPmManifestService(_dydoRoot);

        Assert.False(service.IsActive);
        Assert.Empty(service.GetManifestRecordPaths());
    }

    [Fact]
    public void GetManifestRecordPaths_NormalizesEveryRecord()
    {
        WriteManifest(("DYDO\\project\\tasks\\ONE.md", "applied", "remove-historical", null),
            (ProjectPath("future-features", "idea.md"), "applied", "retain-normalize", ProjectPath("future-features", "idea.md")));
        var service = new LegacyPmManifestService(_dydoRoot);

        Assert.Equal(2, service.GetManifestRecordPaths().Count);
        Assert.Contains(ProjectPath("tasks", "one.md"), service.GetManifestRecordPaths());
    }

    [Fact]
    public void GetAllowedPaths_AllowsExactlyThreeLiveRetainedFutureFeatures()
    {
        var retained = new[]
        {
            ProjectPath("future-features", "agent-graph-metrics.md"),
            ProjectPath("future-features", "coverage.py-update.md"),
            ProjectPath("future-features", "doc-coverage.md")
        };
        WriteManifest(
            (retained[0], "pending", "retain-normalize", retained[0]),
            (retained[1], "pending", "retain-normalize", retained[1]),
            (retained[2], "pending", "retain-normalize", retained[2]),
            (ProjectPath("issues", "removed.md"), "applied", "remove-historical", null),
            (ProjectPath("issues", "cancelled.md"), "applied", "cancel-remove", null),
            (ProjectPath("issues", "migrated.md"), "applied", "migrate-issue", null),
            (ProjectPath("future-features", "not-yet-retained.md"), "pending", "retain", ProjectPath("future-features", "not-yet-retained.md")));
        var service = new LegacyPmManifestService(_dydoRoot);

        Assert.Equal(3, service.GetAllowedPaths().Count);
        foreach (var path in retained)
            Assert.Contains(path, service.GetAllowedPaths());
        Assert.DoesNotContain(ProjectPath("issues", "removed.md"), service.GetAllowedPaths());
        Assert.DoesNotContain(ProjectPath("issues", "cancelled.md"), service.GetAllowedPaths());
        Assert.DoesNotContain(ProjectPath("issues", "migrated.md"), service.GetAllowedPaths());
        Assert.DoesNotContain(ProjectPath("future-features", "not-yet-retained.md"), service.GetAllowedPaths());
    }

    [Fact]
    public void GetAllowedPaths_AcceptsAppliedRetainTransitions()
    {
        var retained = ProjectPath("future-features", "idea.md");
        var normalized = ProjectPath("future-features", "normalized.md");
        WriteManifest(
            (retained, "applied", "retain", retained),
            (normalized, "applied", "retain-normalize", normalized));
        var service = new LegacyPmManifestService(_dydoRoot);

        Assert.Equal(2, service.GetAllowedPaths().Count);
        Assert.Contains(retained, service.GetAllowedPaths());
        Assert.Contains(normalized, service.GetAllowedPaths());
        Assert.Same(service.GetAllowedPaths(), service.GetAllowedPaths());
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("..")]
    public void NormalizeRepoPath_RejectsEscapingPath(string path)
    {
        Assert.Throws<InvalidDataException>(() => LegacyPmManifestService.NormalizeRepoPath(path));
    }

    [Fact]
    public void NormalizeRepoPath_RejectsRootedPath()
    {
        Assert.Throws<InvalidDataException>(() => LegacyPmManifestService.NormalizeRepoPath(Path.GetFullPath("outside.md")));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"records\":{}}")]
    public void GetManifestRecordPaths_RejectsMissingOrNonArrayRecords(string json)
    {
        WriteRawManifest(json);

        Assert.Throws<InvalidDataException>(() => new LegacyPmManifestService(_dydoRoot).GetManifestRecordPaths());
    }

    [Theory]
    [InlineData("{\"path\":\"dydo/project/tasks/one.md\",\"finalDisposition\":\"remove-historical\"}")]
    [InlineData("{\"path\":\"dydo/project/tasks/one.md\",\"executionState\":\"unknown\",\"finalDisposition\":\"remove-historical\"}")]
    [InlineData("{\"path\":\"dydo/project/tasks/one.md\",\"executionState\":\"applied\"}")]
    public void GetManifestRecordPaths_RejectsIncompleteRecord(string record)
    {
        WriteRawManifest($"{{\"records\":[{record}]}}");

        Assert.Throws<InvalidDataException>(() => new LegacyPmManifestService(_dydoRoot).GetManifestRecordPaths());
    }

    [Theory]
    [InlineData("{", "Legacy PM manifest is malformed:")]
    [InlineData("{\"records\":[{\"executionState\":\"applied\",\"finalDisposition\":\"remove-historical\"}]}", "Every legacy PM record requires a path.")]
    [InlineData("{\"records\":[{\"path\":\"project/tasks/one.md\",\"executionState\":\"applied\",\"finalDisposition\":\"remove-historical\"}]}", "Legacy PM manifest path must be under dydo/: project/tasks/one.md")]
    public void GetManifestRecordPaths_RejectsMalformedOrInvalidPath(string json, string expectedMessage)
    {
        WriteRawManifest(json);

        var exception = Assert.Throws<InvalidDataException>(() => new LegacyPmManifestService(_dydoRoot).GetManifestRecordPaths());

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public void GetAllowedPaths_RejectsAppliedRetainedPathWithoutMatchingTarget()
    {
        WriteManifest((ProjectPath("future-features", "idea.md"), "applied", "retain-normalize", ProjectPath("future-features", "other.md")));

        Assert.Throws<InvalidDataException>(() => new LegacyPmManifestService(_dydoRoot).GetAllowedPaths());
    }

    [Fact]
    public void GetManifestRecordPaths_RejectsDuplicateNormalizedPath()
    {
        WriteManifest(
            (ProjectPath("tasks", "one.md"), "applied", "remove-historical", null),
            ("DYDO\\PROJECT\\TASKS\\ONE.md", "applied", "remove-historical", null));

        Assert.Throws<InvalidDataException>(() => new LegacyPmManifestService(_dydoRoot).GetManifestRecordPaths());
    }

    private void WriteManifest(params (string Path, string State, string Disposition, string? Target)[] records)
    {
        var rows = records.Select(record => new
        {
            path = record.Path,
            executionState = record.State,
            finalDisposition = record.Disposition,
            target = record.Target == null ? null : new { kind = "retained-path", value = record.Target }
        });
        WriteRawManifest(System.Text.Json.JsonSerializer.Serialize(new { records = rows }));
    }

    private void WriteRawManifest(string json)
    {
        var directory = Path.Combine(_dydoRoot, "project", "migrations");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "3.0-pm-records.json"), json);
    }

    private static string ProjectPath(string folder, string fileName) => string.Join('/', "dydo", "project", folder, fileName);
}
