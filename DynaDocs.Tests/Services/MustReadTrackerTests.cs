namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

file static class TestConditionalMustReads
{
    public static readonly List<ConditionalMustRead> CodeWriter =
    [
        new()
        {
            When = new ConditionalMustReadCondition { MarkerExists = ".merge-source" },
            Path = "dydo/guides/how-to-merge-worktrees.md"
        }
    ];

    public static readonly List<ConditionalMustRead> Reviewer =
    [
        new()
        {
            When = new ConditionalMustReadCondition { TaskNameMatches = "*-merge" },
            Path = "dydo/guides/how-to-review-worktree-merges.md"
        },
        new()
        {
            Path = "dydo/project/tasks/{task}.md"
        },
        new()
        {
            When = new ConditionalMustReadCondition { DispatchedByRole = "docs-writer" },
            Path = "dydo/reference/writing-docs.md"
        }
    ];
}

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

    #region Conditional Must-Reads (Decision 013)

    [Fact]
    public void ConditionalMustRead_CodeWriterWithMergeSource_IncludesMergeGuide()
    {
        CreateModeFile("Alice", "code-writer", "# Code Writer\nNo links here.");

        var workspace = Path.Combine(_testDir, "agents", "Alice");
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "worktree/some-branch");

        CreateLinkedFile("dydo/guides/how-to-merge-worktrees.md",
            "---\nmust-read: true\n---\n# How to Merge");

        var result = _tracker.ComputeUnreadMustReads("Alice", "code-writer", null,
            conditionalMustReads: TestConditionalMustReads.CodeWriter);

        Assert.Contains(result, r => r.Contains("how-to-merge-worktrees.md"));
    }

    [Fact]
    public void ConditionalMustRead_CodeWriterWithoutMergeSource_NoMergeGuide()
    {
        CreateModeFile("Alice", "code-writer", "# Code Writer\nNo links here.");

        CreateLinkedFile("dydo/guides/how-to-merge-worktrees.md",
            "---\nmust-read: true\n---\n# How to Merge");

        var result = _tracker.ComputeUnreadMustReads("Alice", "code-writer", null,
            conditionalMustReads: TestConditionalMustReads.CodeWriter);

        Assert.DoesNotContain(result, r => r.Contains("how-to-merge-worktrees.md"));
    }

    [Fact]
    public void ConditionalMustRead_ReviewerOnMergeTask_IncludesMergeReviewGuide()
    {
        CreateModeFile("Alice", "reviewer", "# Reviewer\nNo links here.");

        CreateLinkedFile("dydo/guides/how-to-review-worktree-merges.md",
            "---\nmust-read: true\n---\n# How to Review Merges");

        var result = _tracker.ComputeUnreadMustReads("Alice", "reviewer", null, task: "feature-x-merge",
            conditionalMustReads: TestConditionalMustReads.Reviewer);

        Assert.Contains(result, r => r.Contains("how-to-review-worktree-merges.md"));
    }

    [Fact]
    public void ConditionalMustRead_ReviewerOnNonMergeTask_NoMergeReviewGuide()
    {
        CreateModeFile("Alice", "reviewer", "# Reviewer\nNo links here.");

        CreateLinkedFile("dydo/guides/how-to-review-worktree-merges.md",
            "---\nmust-read: true\n---\n# How to Review Merges");

        var result = _tracker.ComputeUnreadMustReads("Alice", "reviewer", null, task: "feature-x",
            conditionalMustReads: TestConditionalMustReads.Reviewer);

        Assert.DoesNotContain(result, r => r.Contains("how-to-review-worktree-merges.md"));
    }

    [Fact]
    public void ConditionalMustRead_ReviewerWithTask_IncludesTaskFile()
    {
        CreateModeFile("Alice", "reviewer", "# Reviewer\nNo links here.");

        CreateLinkedFile("dydo/project/tasks/feature-x.md",
            "---\nname: feature-x\nstatus: review-pending\n---\n# Task: feature-x\n\nImplement feature X.");

        var result = _tracker.ComputeUnreadMustReads("Alice", "reviewer", null, task: "feature-x",
            conditionalMustReads: TestConditionalMustReads.Reviewer);

        Assert.Contains(result, r => r.Contains("feature-x.md"));
    }

    [Fact]
    public void ConditionalMustRead_ReviewerNoTask_NoTaskFile()
    {
        CreateModeFile("Alice", "reviewer", "# Reviewer\nNo links here.");

        var result = _tracker.ComputeUnreadMustReads("Alice", "reviewer", null, task: null,
            conditionalMustReads: TestConditionalMustReads.Reviewer);

        Assert.Single(result);
    }

    [Fact]
    public void ConditionalMustRead_ReviewerTaskFileDoesNotExist_NoTaskFile()
    {
        CreateModeFile("Alice", "reviewer", "# Reviewer\nNo links here.");

        var result = _tracker.ComputeUnreadMustReads("Alice", "reviewer", null, task: "nonexistent-task",
            conditionalMustReads: TestConditionalMustReads.Reviewer);

        Assert.Single(result);
    }

    [Fact]
    public void ConditionalMustRead_NonCodeWriterWithMergeSource_NoMergeGuide()
    {
        CreateModeFile("Alice", "reviewer", "# Reviewer\nNo links here.");

        var workspace = Path.Combine(_testDir, "agents", "Alice");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "worktree/some-branch");

        CreateLinkedFile("dydo/guides/how-to-merge-worktrees.md",
            "---\nmust-read: true\n---\n# How to Merge");

        // Reviewer role does not have markerExists conditional — so no merge guide
        var result = _tracker.ComputeUnreadMustReads("Alice", "reviewer", null,
            conditionalMustReads: TestConditionalMustReads.Reviewer);

        Assert.DoesNotContain(result, r => r.Contains("how-to-merge-worktrees.md"));
    }

    [Fact]
    public void ConditionalMustRead_MergeGuideDoesNotExist_NoError()
    {
        CreateModeFile("Alice", "code-writer", "# Code Writer\nNo links here.");

        var workspace = Path.Combine(_testDir, "agents", "Alice");
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "worktree/some-branch");

        // Don't create the merge guide file
        var result = _tracker.ComputeUnreadMustReads("Alice", "code-writer", null,
            conditionalMustReads: TestConditionalMustReads.CodeWriter);

        Assert.DoesNotContain(result, r => r.Contains("how-to-merge-worktrees.md"));
    }

    [Fact]
    public void AddConditionalMustReads_MarkerExists_AddsMustRead()
    {
        var workspace = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "worktree/branch");

        var guidePath = Path.Combine(_testDir, "dydo", "guides", "how-to-merge-worktrees.md");
        Directory.CreateDirectory(Path.GetDirectoryName(guidePath)!);
        File.WriteAllText(guidePath, "# Guide");

        var conditionals = new List<ConditionalMustRead>
        {
            new()
            {
                When = new ConditionalMustReadCondition { MarkerExists = ".merge-source" },
                Path = "dydo/guides/how-to-merge-worktrees.md"
            }
        };

        var mustReads = new List<string>();
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, null, _testDir, "Alice", conditionals, null);

        Assert.Single(mustReads);
        Assert.Contains("how-to-merge-worktrees.md", mustReads[0]);
    }

    [Fact]
    public void AddConditionalMustReads_TaskNameMatchAndNoWhen_AddsBoth()
    {
        var workspace = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(workspace);

        var guidePath = Path.Combine(_testDir, "dydo", "guides", "how-to-review-worktree-merges.md");
        Directory.CreateDirectory(Path.GetDirectoryName(guidePath)!);
        File.WriteAllText(guidePath, "# Guide");

        var taskPath = Path.Combine(_testDir, "dydo", "project", "tasks", "feat-merge.md");
        Directory.CreateDirectory(Path.GetDirectoryName(taskPath)!);
        File.WriteAllText(taskPath, "# Task");

        var conditionals = new List<ConditionalMustRead>
        {
            new()
            {
                When = new ConditionalMustReadCondition { TaskNameMatches = "*-merge" },
                Path = "dydo/guides/how-to-review-worktree-merges.md"
            },
            new()
            {
                Path = "dydo/project/tasks/{task}.md"
            }
        };

        var mustReads = new List<string>();
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, "feat-merge", _testDir, "Alice", conditionals, null);

        Assert.Equal(2, mustReads.Count);
        Assert.Contains(mustReads, r => r.Contains("how-to-review-worktree-merges.md"));
        Assert.Contains(mustReads, r => r.Contains("feat-merge.md"));
    }

    [Fact]
    public void ConditionalMustRead_EmptyList_NoEffect()
    {
        var workspace = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(workspace);

        var mustReads = new List<string>();
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, "some-task", _testDir, "Alice", [], null);

        Assert.Empty(mustReads);
    }

    [Fact]
    public void ConditionalMustRead_NoWhen_AlwaysApplies()
    {
        var workspace = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(workspace);

        var taskPath = Path.Combine(_testDir, "dydo", "project", "tasks", "my-task.md");
        Directory.CreateDirectory(Path.GetDirectoryName(taskPath)!);
        File.WriteAllText(taskPath, "# Task");

        var conditionals = new List<ConditionalMustRead>
        {
            new() { Path = "dydo/project/tasks/{task}.md" }
        };

        var mustReads = new List<string>();
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, "my-task", _testDir, "Alice", conditionals, null);

        Assert.Single(mustReads);
        Assert.Contains("my-task.md", mustReads[0]);
    }

    [Fact]
    public void ConditionalMustRead_PathInterpolation_NullTask_Skipped()
    {
        var workspace = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(workspace);

        var conditionals = new List<ConditionalMustRead>
        {
            new() { Path = "dydo/project/tasks/{task}.md" }
        };

        var mustReads = new List<string>();
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, null, _testDir, "Alice", conditionals, null);

        Assert.Empty(mustReads);
    }

    [Fact]
    public void ConditionalMustRead_FileDoesNotExist_Skipped()
    {
        var workspace = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(workspace);

        var conditionals = new List<ConditionalMustRead>
        {
            new() { Path = "dydo/nonexistent/file.md" }
        };

        var mustReads = new List<string>();
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, null, _testDir, "Alice", conditionals, null);

        Assert.Empty(mustReads);
    }

    [Fact]
    public void ConditionalMustRead_MultipleConditions_AllMustMatch()
    {
        var workspace = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, ".merge-source"), "branch");

        var guidePath = Path.Combine(_testDir, "dydo", "guides", "how-to-merge-worktrees.md");
        Directory.CreateDirectory(Path.GetDirectoryName(guidePath)!);
        File.WriteAllText(guidePath, "# Guide");

        // Both markerExists AND taskNameMatches must pass
        var conditionals = new List<ConditionalMustRead>
        {
            new()
            {
                When = new ConditionalMustReadCondition
                {
                    MarkerExists = ".merge-source",
                    TaskNameMatches = "*-merge"
                },
                Path = "dydo/guides/how-to-merge-worktrees.md"
            }
        };

        // Task matches pattern AND marker exists → should pass
        var mustReads = new List<string>();
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, "feat-merge", _testDir, "Alice", conditionals, null);
        Assert.Single(mustReads);

        // Task does NOT match pattern → should fail (AND logic)
        mustReads = [];
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, "feat-impl", _testDir, "Alice", conditionals, null);
        Assert.Empty(mustReads);
    }

    [Fact]
    public void ConditionalMustRead_InboxReaderNull_DispatchedByRoleSkipped()
    {
        var workspace = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(workspace);

        var docPath = Path.Combine(_testDir, "dydo", "reference", "writing-docs.md");
        Directory.CreateDirectory(Path.GetDirectoryName(docPath)!);
        File.WriteAllText(docPath, "# Writing Docs");

        var conditionals = new List<ConditionalMustRead>
        {
            new()
            {
                When = new ConditionalMustReadCondition { DispatchedByRole = "docs-writer" },
                Path = "dydo/reference/writing-docs.md"
            }
        };

        var mustReads = new List<string>();
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, "my-task", _testDir, "Alice", conditionals, null);

        Assert.Empty(mustReads);
    }

    [Fact]
    public void ConditionalMustRead_TaskNameDoesNotMatch_Skipped()
    {
        var workspace = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(workspace);

        var guidePath = Path.Combine(_testDir, "dydo", "guides", "how-to-review-worktree-merges.md");
        Directory.CreateDirectory(Path.GetDirectoryName(guidePath)!);
        File.WriteAllText(guidePath, "# Guide");

        var conditionals = new List<ConditionalMustRead>
        {
            new()
            {
                When = new ConditionalMustReadCondition { TaskNameMatches = "*-merge" },
                Path = "dydo/guides/how-to-review-worktree-merges.md"
            }
        };

        var mustReads = new List<string>();
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, "feat-impl", _testDir, "Alice", conditionals, null);

        Assert.Empty(mustReads);
    }

    [Fact]
    public void ConditionalMustRead_MarkerDoesNotExist_Skipped()
    {
        var workspace = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(workspace);

        var guidePath = Path.Combine(_testDir, "dydo", "guides", "how-to-merge-worktrees.md");
        Directory.CreateDirectory(Path.GetDirectoryName(guidePath)!);
        File.WriteAllText(guidePath, "# Guide");

        var conditionals = new List<ConditionalMustRead>
        {
            new()
            {
                When = new ConditionalMustReadCondition { MarkerExists = ".merge-source" },
                Path = "dydo/guides/how-to-merge-worktrees.md"
            }
        };

        var mustReads = new List<string>();
        MustReadTracker.AddConditionalMustReads(mustReads, workspace, null, _testDir, "Alice", conditionals, null);

        Assert.Empty(mustReads);
    }

    #endregion

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
