namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public sealed class LegacyPmManifestServiceTests : IDisposable
{
    private readonly string _dydoRoot = Path.Combine(
        Path.GetTempPath(),
        $"legacy-pm-manifest-{Guid.NewGuid():N}");

    public LegacyPmManifestServiceTests()
    {
        Directory.CreateDirectory(_dydoRoot);
    }

    public void Dispose()
    {
        Directory.Delete(_dydoRoot, recursive: true);
    }

    [Fact]
    public void InactiveManifest_HasNoPendingRecords()
    {
        var service = new LegacyPmManifestService(_dydoRoot);

        Assert.False(service.IsActive);
        Assert.Empty(service.GetPendingRecordPaths());
    }

    [Fact]
    public void GetPendingRecordPaths_ParsesPendingRowsAndNormalizesPaths()
    {
        WriteManifest(
            ("DYDO\\project\\tasks\\ONE.md", "pending"),
            (ProjectPath("issues", "two.md"), "applied"));
        var service = new LegacyPmManifestService(_dydoRoot);

        var path = Assert.Single(service.GetPendingRecordPaths());

        Assert.Equal(ProjectPath("tasks", "one.md"), path);
        Assert.Equal(2, service.GetManifestRecordPaths().Count);
    }

    [Fact]
    public void GetRetainedNonRecordPaths_ReturnsExactClosedAllowSet()
    {
        var paths = LegacyPmManifestService.GetRetainedNonRecordPaths();

        Assert.Equal(12, paths.Count);
        Assert.Equal(
            [
                ProjectPath("backlog", "_backlog.md"),
                ProjectPath("backlog", "_index.md"),
                ProjectPath("campaigns", "_campaigns.md"),
                ProjectPath("campaigns", "_index.md"),
                ProjectPath("issues", "_index.md"),
                ProjectPath("issues", "_issues.md"),
                ProjectPath("slices", "_index.md"),
                ProjectPath("slices", "_slices.md"),
                ProjectPath("sprints", "_index.md"),
                ProjectPath("sprints", "_sprints.md"),
                ProjectPath("tasks", "_index.md"),
                ProjectPath("tasks", "_tasks.md")
            ],
            paths.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void GetAllowedPaths_CachesPendingAndRetainedPaths()
    {
        WriteManifest((ProjectPath("tasks", "one.md"), "pending"));
        var service = new LegacyPmManifestService(_dydoRoot);

        var first = service.GetAllowedPaths();
        var second = service.GetAllowedPaths();

        Assert.Same(first, second);
        Assert.Equal(13, first.Count);
    }

    [Fact]
    public void GetPendingRecordPaths_RejectsEscapingPath()
    {
        WriteManifest(("dydo/../outside.md", "pending"));
        var service = new LegacyPmManifestService(_dydoRoot);

        var exception = Assert.Throws<InvalidDataException>(() => service.GetPendingRecordPaths());

        Assert.Contains("under dydo", exception.Message);
    }

    [Fact]
    public void NormalizeRepoPath_RejectsPathThatEscapesRepository()
    {
        Assert.Throws<InvalidDataException>(() =>
            LegacyPmManifestService.NormalizeRepoPath("../outside.md"));
    }

    [Fact]
    public void NormalizeRepoPath_RejectsParentDirectoryItself()
    {
        Assert.Throws<InvalidDataException>(() =>
            LegacyPmManifestService.NormalizeRepoPath(".."));
    }

    [Fact]
    public void NormalizeRepoPath_RejectsRootedPath()
    {
        var rootedPath = Path.GetFullPath("outside.md");

        Assert.Throws<InvalidDataException>(() =>
            LegacyPmManifestService.NormalizeRepoPath(rootedPath));
    }

    [Fact]
    public void GetPendingRecordPaths_RejectsDuplicateNormalizedPath()
    {
        WriteManifest(
            (ProjectPath("tasks", "one.md"), "pending"),
            ("DYDO\\PROJECT\\TASKS\\ONE.md", "pending"));
        var service = new LegacyPmManifestService(_dydoRoot);

        var exception = Assert.Throws<InvalidDataException>(() => service.GetPendingRecordPaths());

        Assert.Contains("Duplicate", exception.Message);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"records\":{}}")]
    public void GetPendingRecordPaths_RejectsMissingOrNonArrayRecords(string json)
    {
        WriteRawManifest(json);
        var service = new LegacyPmManifestService(_dydoRoot);

        Assert.Throws<InvalidDataException>(() => service.GetPendingRecordPaths());
    }

    [Theory]
    [InlineData("{\"path\":\"dydo/project/tasks/one.md\"}")]
    [InlineData("{\"path\":\"dydo/project/tasks/one.md\",\"executionState\":1}")]
    [InlineData("{\"path\":\"dydo/project/tasks/one.md\",\"executionState\":\"unknown\"}")]
    public void GetPendingRecordPaths_RejectsInvalidExecutionState(string record)
    {
        WriteRawManifest($"{{\"records\":[{record}]}}");
        var service = new LegacyPmManifestService(_dydoRoot);

        Assert.Throws<InvalidDataException>(() => service.GetPendingRecordPaths());
    }

    [Theory]
    [InlineData("{\"executionState\":\"pending\"}")]
    [InlineData("{\"path\":1,\"executionState\":\"pending\"}")]
    [InlineData("{\"path\":\"\",\"executionState\":\"pending\"}")]
    public void GetPendingRecordPaths_RejectsInvalidPath(string record)
    {
        WriteRawManifest($"{{\"records\":[{record}]}}");
        var service = new LegacyPmManifestService(_dydoRoot);

        Assert.Throws<InvalidDataException>(() => service.GetPendingRecordPaths());
    }

    [Fact]
    public void GetPendingRecordPaths_WrapsMalformedJson()
    {
        WriteRawManifest("{");
        var service = new LegacyPmManifestService(_dydoRoot);

        var exception = Assert.Throws<InvalidDataException>(() => service.GetPendingRecordPaths());

        Assert.Contains("malformed", exception.Message);
    }

    private void WriteManifest(params (string Path, string State)[] records)
    {
        var rows = string.Join(",", records.Select(record =>
            $$"""{"path":{{System.Text.Json.JsonSerializer.Serialize(record.Path)}},"executionState":"{{record.State}}"}"""));
        WriteRawManifest($$"""{"records":[{{rows}}]}""");
    }

    private void WriteRawManifest(string json)
    {
        var directory = Path.Combine(_dydoRoot, "project", "migrations");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "3.0-pm-records.json"), json);
    }

    private static string ProjectPath(string folder, string fileName)
    {
        return string.Join('/', "dydo", "project", folder, fileName);
    }
}
