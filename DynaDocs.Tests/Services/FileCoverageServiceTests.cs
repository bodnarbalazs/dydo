namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class FileCoverageServiceTests : IDisposable
{
    private readonly string _testDir;

    public FileCoverageServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-fcov-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);

        // Default test seams: no git, no decay
        FileCoverageService.GitLsFilesOverride = _ => ["Commands/Foo.cs", "Commands/Bar.cs", "Services/Baz.cs"];
        FileCoverageService.GetPercentChangeOverride = (_, _, _) => 0;
    }

    public void Dispose()
    {
        FileCoverageService.GitLsFilesOverride = null;
        FileCoverageService.GetPercentChangeOverride = null;

        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    private FileCoverageService CreateService(List<AuditSession>? sessions = null, DydoConfig? config = null)
    {
        return new FileCoverageService(
            new StubAuditService(sessions ?? []),
            new StubConfigService(_testDir, config));
    }

    private static AuditSession MakeSession(string task, string role, DateTime started, params (string Path, AuditEventType Type)[] events)
    {
        var auditEvents = new List<AuditEvent>
        {
            new() { EventType = AuditEventType.Role, Role = role, Task = task, Timestamp = started }
        };

        foreach (var (path, type) in events)
        {
            auditEvents.Add(new AuditEvent { EventType = type, Path = path, Timestamp = started.AddMinutes(auditEvents.Count) });
        }

        return new AuditSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Started = started,
            Events = auditEvents
        };
    }

    #region Grouping

    [Fact]
    public void Grouping_InquisitorWithSubSessions_GroupedByTaskPrefix()
    {
        var inq = MakeSession("investigate-guard", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var sub1 = MakeSession("investigate-guard-hyp-1", "code-writer", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var sub2 = MakeSession("investigate-guard-quality", "reviewer", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Bar.cs", AuditEventType.Read));

        var service = CreateService([inq, sub1, sub2]);
        var report = service.GenerateReport(new FileCoverageOptions());

        // Foo.cs read in inquisitor + sub1 = 2 reads, capped at 3 per group → contributes 2 from one group
        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.True(fooEntry.RawScore >= 1, "Foo.cs should have score from grouped reads");
    }

    [Fact]
    public void Grouping_DoesNotMatchSubstringWithoutHyphen()
    {
        // "foobar" should NOT match task prefix "foo" (requires "foo-" prefix or exact match)
        var inq = MakeSession("foo", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var notSub = MakeSession("foobar", "code-writer", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Bar.cs", AuditEventType.Read));

        var service = CreateService([inq, notSub]);
        var report = service.GenerateReport(new FileCoverageOptions());

        // Bar.cs only read in notSub which is NOT grouped with the inquisitor
        var barEntry = FindEntry(report, "Commands/Bar.cs");
        Assert.Equal(0, barEntry.RawScore);
    }

    [Fact]
    public void Grouping_NoSubSessions_InquisitorAlone()
    {
        var inq = MakeSession("solo-task", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq]);
        var report = service.GenerateReport(new FileCoverageOptions());

        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.Equal(1, fooEntry.RawScore);
    }

    [Fact]
    public void Grouping_MultipleInquisitors_SeparateGroups()
    {
        var inq1 = MakeSession("task-a", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var inq2 = MakeSession("task-b", "inquisitor", DateTime.UtcNow.AddDays(-5),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq1, inq2]);
        var report = service.GenerateReport(new FileCoverageOptions());

        // Foo.cs read once in each group → 1 + 1 = 2
        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.Equal(2, fooEntry.RawScore);
    }

    [Fact]
    public void Grouping_NonInquisitorSessionsIgnored_WhenNoMatchingPrefix()
    {
        var inq = MakeSession("task-x", "inquisitor", DateTime.UtcNow.AddDays(-10));
        var unrelated = MakeSession("other-task", "code-writer", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq, unrelated]);
        var report = service.GenerateReport(new FileCoverageOptions());

        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.Equal(0, fooEntry.RawScore);
    }

    #endregion

    #region Scoring

    [Fact]
    public void Scoring_CapAt3PerGroup()
    {
        var inq = MakeSession("task", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq]);
        var report = service.GenerateReport(new FileCoverageOptions());

        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.Equal(3, fooEntry.RawScore);
    }

    [Fact]
    public void Scoring_AccumulatesAcrossGroups()
    {
        // 3 groups with 2, 4, 1 reads → 2 + 3 + 1 = 6
        var inq1 = MakeSession("t1", "inquisitor", DateTime.UtcNow.AddDays(-30),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var inq2 = MakeSession("t2", "inquisitor", DateTime.UtcNow.AddDays(-20),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var inq3 = MakeSession("t3", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq1, inq2, inq3]);
        var report = service.GenerateReport(new FileCoverageOptions());

        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.Equal(6, fooEntry.RawScore);
    }

    [Fact]
    public void Scoring_FileNotRead_ScoreZero()
    {
        var inq = MakeSession("task", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq]);
        var report = service.GenerateReport(new FileCoverageOptions());

        var bazEntry = FindEntry(report, "Services/Baz.cs");
        Assert.Equal(0, bazEntry.RawScore);
    }

    #endregion

    #region Decay

    [Fact]
    public void Decay_NoChanges_NoKnockdown()
    {
        FileCoverageService.GetPercentChangeOverride = (_, _, _) => 0;

        var inq = MakeSession("task", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq]);
        var report = service.GenerateReport(new FileCoverageOptions());

        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.Equal(fooEntry.RawScore, fooEntry.AdjustedScore);
    }

    [Fact]
    public void Decay_SmallChange_PartialKnockdown()
    {
        // pct_change=5, score=6 → knockdown = ceil(6 * 0.5 * 0.25) = ceil(0.75) = 1
        FileCoverageService.GetPercentChangeOverride = (_, _, _) => 5;

        var inq1 = MakeSession("t1", "inquisitor", DateTime.UtcNow.AddDays(-30),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var inq2 = MakeSession("t2", "inquisitor", DateTime.UtcNow.AddDays(-20),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var inq3 = MakeSession("t3", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq1, inq2, inq3]);
        var report = service.GenerateReport(new FileCoverageOptions());

        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.Equal(6, fooEntry.RawScore);
        Assert.Equal(5, fooEntry.AdjustedScore); // 6 - ceil(6*0.5*0.25) = 6 - 1 = 5
    }

    [Fact]
    public void Decay_TwentyPercentChange_HalfKnockdown()
    {
        // pct_change=20, score=8 → knockdown = ceil(8 * 0.5 * 1.0) = 4, adjusted = 4
        FileCoverageService.GetPercentChangeOverride = (_, _, _) => 20;
        FileCoverageService.GitLsFilesOverride = _ => ["Commands/Foo.cs"];

        // Build up score of 8: 3 groups contributing 3+3+2
        var inq1 = MakeSession("t1", "inquisitor", DateTime.UtcNow.AddDays(-30),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var inq2 = MakeSession("t2", "inquisitor", DateTime.UtcNow.AddDays(-20),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var inq3 = MakeSession("t3", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq1, inq2, inq3]);
        var report = service.GenerateReport(new FileCoverageOptions());

        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.Equal(8, fooEntry.RawScore);
        Assert.Equal(4, fooEntry.AdjustedScore);
    }

    [Fact]
    public void Decay_OverTwentyPercent_CappedAtHalf()
    {
        // pct_change=100, score=8 → knockdown = ceil(8 * 0.5 * 1.0) = 4 (same as 20%)
        FileCoverageService.GetPercentChangeOverride = (_, _, _) => 100;
        FileCoverageService.GitLsFilesOverride = _ => ["Commands/Foo.cs"];

        var inq1 = MakeSession("t1", "inquisitor", DateTime.UtcNow.AddDays(-30),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var inq2 = MakeSession("t2", "inquisitor", DateTime.UtcNow.AddDays(-20),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var inq3 = MakeSession("t3", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq1, inq2, inq3]);
        var report = service.GenerateReport(new FileCoverageOptions());

        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.Equal(8, fooEntry.RawScore);
        Assert.Equal(4, fooEntry.AdjustedScore);
    }

    [Fact]
    public void Decay_ScoreFloorsAtZero()
    {
        FileCoverageService.GetPercentChangeOverride = (_, _, _) => 100;
        FileCoverageService.GitLsFilesOverride = _ => ["Commands/Foo.cs"];

        var inq = MakeSession("task", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq]);
        var report = service.GenerateReport(new FileCoverageOptions());

        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.Equal(1, fooEntry.RawScore);
        Assert.Equal(0, fooEntry.AdjustedScore);
    }

    #endregion

    #region Status

    [Fact]
    public void Status_GapLowCovered_Thresholds()
    {
        FileCoverageService.GitLsFilesOverride = _ => ["a.cs", "b.cs", "c.cs"];
        FileCoverageService.GetPercentChangeOverride = (_, _, _) => 0;

        // a.cs: 0 reads → gap
        // b.cs: 3 reads in 1 group → score 3 → low
        // c.cs: 3 reads in 3 groups → score 9 → covered
        var inq1 = MakeSession("t1", "inquisitor", DateTime.UtcNow.AddDays(-30),
            ($"{_testDir}/c.cs", AuditEventType.Read),
            ($"{_testDir}/c.cs", AuditEventType.Read),
            ($"{_testDir}/c.cs", AuditEventType.Read),
            ($"{_testDir}/b.cs", AuditEventType.Read),
            ($"{_testDir}/b.cs", AuditEventType.Read),
            ($"{_testDir}/b.cs", AuditEventType.Read));
        var inq2 = MakeSession("t2", "inquisitor", DateTime.UtcNow.AddDays(-20),
            ($"{_testDir}/c.cs", AuditEventType.Read),
            ($"{_testDir}/c.cs", AuditEventType.Read),
            ($"{_testDir}/c.cs", AuditEventType.Read));
        var inq3 = MakeSession("t3", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/c.cs", AuditEventType.Read),
            ($"{_testDir}/c.cs", AuditEventType.Read),
            ($"{_testDir}/c.cs", AuditEventType.Read));

        var service = CreateService([inq1, inq2, inq3]);
        var report = service.GenerateReport(new FileCoverageOptions());

        Assert.Equal("gap", FindEntry(report, "a.cs").Status);
        Assert.Equal("low", FindEntry(report, "b.cs").Status);
        Assert.Equal("covered", FindEntry(report, "c.cs").Status);
    }

    #endregion

    #region Path Normalization

    [Fact]
    public void PathNormalization_WindowsAbsolutePaths_MatchRelative()
    {
        // Simulate Windows paths in audit events
        var winPath = _testDir.Replace('/', '\\') + "\\Commands\\Foo.cs";

        var inq = MakeSession("task", "inquisitor", DateTime.UtcNow.AddDays(-10),
            (winPath, AuditEventType.Read));

        var service = CreateService([inq]);
        var report = service.GenerateReport(new FileCoverageOptions());

        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        Assert.Equal(1, fooEntry.RawScore);
    }

    #endregion

    #region Options

    [Fact]
    public void PathFilter_ScopesToSubtree()
    {
        var inq = MakeSession("task", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read),
            ($"{_testDir}/Services/Baz.cs", AuditEventType.Read));

        var service = CreateService([inq]);
        var report = service.GenerateReport(new FileCoverageOptions(PathFilter: "Services"));

        Assert.Equal(1, report.TotalFiles);
        Assert.DoesNotContain(report.Folders.SelectMany(GetAllEntries), e => e.RelativePath.Contains("Commands"));
    }

    [Fact]
    public void GapsOnly_ExcludesCoveredFiles()
    {
        FileCoverageService.GitLsFilesOverride = _ => ["a.cs", "b.cs"];

        var inq1 = MakeSession("t1", "inquisitor", DateTime.UtcNow.AddDays(-30),
            ($"{_testDir}/b.cs", AuditEventType.Read),
            ($"{_testDir}/b.cs", AuditEventType.Read),
            ($"{_testDir}/b.cs", AuditEventType.Read));
        var inq2 = MakeSession("t2", "inquisitor", DateTime.UtcNow.AddDays(-20),
            ($"{_testDir}/b.cs", AuditEventType.Read),
            ($"{_testDir}/b.cs", AuditEventType.Read),
            ($"{_testDir}/b.cs", AuditEventType.Read));
        var inq3 = MakeSession("t3", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/b.cs", AuditEventType.Read),
            ($"{_testDir}/b.cs", AuditEventType.Read),
            ($"{_testDir}/b.cs", AuditEventType.Read));

        var service = CreateService([inq1, inq2, inq3]);
        var report = service.GenerateReport(new FileCoverageOptions(GapsOnly: true));

        // b.cs has score 9 → covered → excluded
        Assert.Equal(1, report.TotalFiles);
        Assert.All(GetAllEntries(report), e => Assert.NotEqual("covered", e.Status));
    }

    [Fact]
    public void Since_ExcludesOldSessions()
    {
        var recent = MakeSession("t1", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));
        var old = MakeSession("t2", "inquisitor", DateTime.UtcNow.AddDays(-400),
            ($"{_testDir}/Commands/Bar.cs", AuditEventType.Read));

        var service = CreateService([recent, old]);
        var report = service.GenerateReport(new FileCoverageOptions(SinceDays: 30));

        var fooEntry = FindEntry(report, "Commands/Foo.cs");
        var barEntry = FindEntry(report, "Commands/Bar.cs");
        Assert.Equal(1, fooEntry.RawScore);
        Assert.Equal(0, barEntry.RawScore);
    }

    #endregion

    #region Rendering

    [Fact]
    public void RenderMarkdown_SummaryOnly_NoFileDetails()
    {
        var inq = MakeSession("task", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq]);
        var options = new FileCoverageOptions(SummaryOnly: true);
        var report = service.GenerateReport(options);
        var markdown = service.RenderMarkdown(report, options);

        Assert.Contains("Folder Summary", markdown);
        Assert.DoesNotContain("[gap]", markdown);
        Assert.DoesNotContain("[covered]", markdown);
    }

    [Fact]
    public void RenderMarkdown_Full_ContainsFileEntries()
    {
        var inq = MakeSession("task", "inquisitor", DateTime.UtcNow.AddDays(-10),
            ($"{_testDir}/Commands/Foo.cs", AuditEventType.Read));

        var service = CreateService([inq]);
        var options = new FileCoverageOptions();
        var report = service.GenerateReport(options);
        var markdown = service.RenderMarkdown(report, options);

        Assert.Contains("## Files", markdown);
        Assert.Contains("[low]", markdown); // Foo.cs with score 1
        Assert.Contains("`Foo.cs`", markdown);
    }

    #endregion

    #region Source Filtering

    [Fact]
    public void SourceFilter_ExcludesNonSourceFiles()
    {
        FileCoverageService.GitLsFilesOverride = _ =>
        [
            "Commands/Foo.cs",
            "Services/Bar.cs",
            "Models/Baz.cs",
            "dydo/_system/audit/2026/session.json",
            "DynaDocs.Tests/SomeTest.cs",
            "Templates/template.md",
            "npm/package.json",
            ".gitignore",
            "DynaDocs.csproj"
        ];

        var config = new DydoConfig
        {
            Paths = new PathsConfig
            {
                Source = ["Commands/**", "Services/**", "Models/**"]
            }
        };

        var service = CreateService(config: config);
        var report = service.GenerateReport(new FileCoverageOptions());

        Assert.Equal(3, report.TotalFiles);
    }

    [Fact]
    public void SourceFilter_NoConfig_IncludesAllFiles()
    {
        FileCoverageService.GitLsFilesOverride = _ =>
        [
            "Commands/Foo.cs",
            "dydo/some-doc.md",
            "npm/package.json"
        ];

        var service = CreateService();
        var report = service.GenerateReport(new FileCoverageOptions());

        Assert.Equal(3, report.TotalFiles);
    }

    [Fact]
    public void SourceFilter_EmptySourcePaths_IncludesAllFiles()
    {
        FileCoverageService.GitLsFilesOverride = _ =>
        [
            "Commands/Foo.cs",
            "dydo/some-doc.md"
        ];

        var config = new DydoConfig { Paths = new PathsConfig { Source = [] } };
        var service = CreateService(config: config);
        var report = service.GenerateReport(new FileCoverageOptions());

        Assert.Equal(2, report.TotalFiles);
    }

    [Fact]
    public void SourceFilter_RootLevelFile_MatchesExactPattern()
    {
        FileCoverageService.GitLsFilesOverride = _ =>
        [
            "Program.cs",
            "DynaDocs.csproj",
            "Commands/Foo.cs"
        ];

        var config = new DydoConfig
        {
            Paths = new PathsConfig
            {
                Source = ["Program.cs", "Commands/**"]
            }
        };

        var service = CreateService(config: config);
        var report = service.GenerateReport(new FileCoverageOptions());

        Assert.Equal(2, report.TotalFiles);
    }

    [Fact]
    public void SourceFilter_CombinesWithPathFilter()
    {
        FileCoverageService.GitLsFilesOverride = _ =>
        [
            "Commands/Foo.cs",
            "Commands/Bar.cs",
            "Services/Baz.cs",
            "dydo/doc.md"
        ];

        var config = new DydoConfig
        {
            Paths = new PathsConfig
            {
                Source = ["Commands/**", "Services/**"]
            }
        };

        var service = CreateService(config: config);
        var report = service.GenerateReport(new FileCoverageOptions(PathFilter: "Commands"));

        Assert.Equal(2, report.TotalFiles);
    }

    #endregion

    #region Helpers

    private static FileCoverageEntry FindEntry(FileCoverageReport report, string fileName)
    {
        var all = GetAllEntries(report);
        var normalized = fileName.Replace('\\', '/').ToLowerInvariant();
        return all.First(e => e.RelativePath.Replace('\\', '/').ToLowerInvariant().EndsWith(normalized));
    }

    private static List<FileCoverageEntry> GetAllEntries(FileCoverageReport report)
    {
        return report.Folders.SelectMany(GetAllEntries).ToList();
    }

    private static IEnumerable<FileCoverageEntry> GetAllEntries(FolderCoverage folder)
    {
        return folder.Files.Concat(folder.SubFolders.SelectMany(GetAllEntries));
    }

    #endregion

    #region Test Doubles

    private class StubAuditService(List<AuditSession> sessions) : IAuditService
    {
        public (IReadOnlyList<AuditSession> Sessions, bool LimitReached) LoadSessions(string? yearFilter = null)
            => (sessions, false);

        public void LogEvent(string sessionId, AuditEvent @event, string? agentName = null, string? human = null, ProjectSnapshot? snapshot = null) { }
        public AuditSession? GetSession(string sessionId) => null;
        public IReadOnlyList<string> ListSessionFiles(string? yearFilter = null) => [];
        public string GetAuditPath() => "";
        public void EnsureAuditFolder() { }
    }

    private class StubConfigService(string projectRoot, DydoConfig? config = null) : IConfigService
    {
        public string? GetProjectRoot(string? startPath = null) => projectRoot;
        public string? FindConfigFile(string? startPath = null) => null;
        public DydoConfig? LoadConfig(string? startPath = null) => config;
        public void SaveConfig(DydoConfig config, string path) { }
        public string? GetHumanFromEnv() => null;
        public string GetDydoRoot(string? startPath = null) => Path.Combine(projectRoot, "dydo");
        public string GetAgentsPath(string? startPath = null) => Path.Combine(projectRoot, "dydo", "agents");
        public string GetDocsPath(string? startPath = null) => Path.Combine(projectRoot, "dydo");
        public string GetTasksPath(string? startPath = null) => Path.Combine(projectRoot, "dydo", "project", "tasks");
        public string GetAuditPath(string? startPath = null) => Path.Combine(projectRoot, "dydo", "_system", "audit");
        public string GetIssuesPath(string? startPath = null) => Path.Combine(projectRoot, "dydo", "project", "issues");
        public string GetChangelogPath(string? startPath = null) => Path.Combine(projectRoot, "dydo", "project", "changelog");
        public (bool CanClaim, string? Error) ValidateAgentClaim(string agentName, string? humanName, DydoConfig? config) => (true, null);
        public string? GetFirstFreeAgent(string humanName, DydoConfig config, Func<string, bool> isAgentFree) => null;
    }

    #endregion
}
