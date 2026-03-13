namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class MustReadTrackerTests : IDisposable
{
    private readonly string _testDir;
    private readonly FakeConfigService _configService;
    private readonly FakeAuditService _auditService;
    private readonly MustReadTracker _tracker;

    public MustReadTrackerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-mustread-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _configService = new FakeConfigService(_testDir);
        _auditService = new FakeAuditService();
        _tracker = new MustReadTracker(
            _testDir,
            _configService,
            _auditService,
            agent => Path.Combine(_testDir, "agents", agent));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private void CreateModeFile(string agentName, string role, string content)
    {
        var dir = Path.Combine(_testDir, "agents", agentName, "modes");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{role}.md"), content);
    }

    private void CreateLinkedFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_testDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    [Fact]
    public void ComputeUnreadMustReads_NoModeFile_ReturnsEmpty()
    {
        var result = _tracker.ComputeUnreadMustReads("Alice", "code-writer", null);
        Assert.Empty(result);
    }

    [Fact]
    public void ComputeUnreadMustReads_ModeFileOnly_ReturnsModeFileItself()
    {
        CreateModeFile("Alice", "code-writer", "# Code Writer\nNo links here.");

        var result = _tracker.ComputeUnreadMustReads("Alice", "code-writer", null);

        Assert.Single(result);
        Assert.Contains("code-writer.md", result[0]);
    }

    [Fact]
    public void ComputeUnreadMustReads_LinkedMustReadFile_Included()
    {
        CreateLinkedFile("docs/about.md", "---\nmust-read: true\n---\n# About");
        CreateModeFile("Alice", "code-writer", "# Mode\n\n[About](../../../docs/about.md)");

        var result = _tracker.ComputeUnreadMustReads("Alice", "code-writer", null);

        Assert.True(result.Count >= 2);
        Assert.Contains(result, r => r.Contains("about.md"));
    }

    [Fact]
    public void ComputeUnreadMustReads_LinkedNonMustReadFile_NotIncluded()
    {
        CreateLinkedFile("docs/optional.md", "---\nmust-read: false\n---\n# Optional");
        CreateModeFile("Alice", "code-writer", "# Mode\n\n[Opt](../../../docs/optional.md)");

        var result = _tracker.ComputeUnreadMustReads("Alice", "code-writer", null);

        Assert.DoesNotContain(result, r => r.Contains("optional.md"));
    }

    [Fact]
    public void ComputeUnreadMustReads_ExternalLink_Ignored()
    {
        CreateModeFile("Alice", "code-writer", "# Mode\n\n[Google](https://google.com)");

        var result = _tracker.ComputeUnreadMustReads("Alice", "code-writer", null);

        Assert.Single(result); // only the mode file itself
    }

    [Fact]
    public void ComputeUnreadMustReads_WithSession_FiltersAlreadyRead()
    {
        CreateLinkedFile("docs/about.md", "---\nmust-read: true\n---\n# About");
        CreateModeFile("Alice", "code-writer", "# Mode\n\n[About](../../../docs/about.md)");

        _auditService.SessionToReturn = new AuditSession
        {
            SessionId = "sess-1",
            Events =
            [
                new AuditEvent
                {
                    EventType = AuditEventType.Read,
                    Path = "docs/about.md"
                }
            ]
        };

        var result = _tracker.ComputeUnreadMustReads("Alice", "code-writer", "sess-1");

        Assert.DoesNotContain(result, r => r.Contains("about.md"));
    }

    [Fact]
    public void ComputeUnreadMustReads_AuditServiceThrows_StillReturnsResults()
    {
        CreateLinkedFile("docs/about.md", "---\nmust-read: true\n---\n# About");
        CreateModeFile("Alice", "code-writer", "# Mode\n\n[About](../../../docs/about.md)");

        _auditService.ShouldThrow = true;

        var result = _tracker.ComputeUnreadMustReads("Alice", "code-writer", "sess-1");

        Assert.True(result.Count >= 1);
    }

    [Fact]
    public void NormalizeMustReadPath_StripsPrefixBeforeDydo()
    {
        var result = MustReadTracker.NormalizeMustReadPath(@"C:\projects\myapp\dydo\guides\coding.md");
        Assert.StartsWith("dydo/", result);
    }

    [Fact]
    public void NormalizeMustReadPath_NoDydoPrefix_ReturnsNormalized()
    {
        var result = MustReadTracker.NormalizeMustReadPath(@"some\other\path.md");
        Assert.Equal("some/other/path.md", result);
    }

    #region Test Fakes

    private class FakeConfigService : IConfigService
    {
        private readonly string _basePath;
        public FakeConfigService(string basePath) => _basePath = basePath;

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

    private class FakeAuditService : IAuditService
    {
        public AuditSession? SessionToReturn { get; set; }
        public bool ShouldThrow { get; set; }

        public void LogEvent(string sessionId, AuditEvent @event, string? agentName = null, string? human = null, ProjectSnapshot? snapshot = null) { }

        public AuditSession? GetSession(string sessionId)
        {
            if (ShouldThrow) throw new IOException("audit failure");
            return SessionToReturn;
        }

        public (IReadOnlyList<AuditSession> Sessions, bool LimitReached) LoadSessions(string? yearFilter = null) => ([], false);
        public IReadOnlyList<string> ListSessionFiles(string? yearFilter = null) => [];
        public string GetAuditPath() => "";
        public void EnsureAuditFolder() { }
    }

    #endregion
}
