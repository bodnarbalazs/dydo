namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class WorkspaceArchiverTests : IDisposable
{
    private readonly string _testDir;

    public WorkspaceArchiverTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-wsarchiver-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private string CreateWorkspace()
    {
        var workspace = Path.Combine(_testDir, "workspace");
        Directory.CreateDirectory(workspace);
        return workspace;
    }

    #region ArchiveWorkspace

    [Fact]
    public void ArchiveWorkspace_NonexistentDir_ReturnsNull()
    {
        Assert.Null(WorkspaceArchiver.ArchiveWorkspace(Path.Combine(_testDir, "nope")));
    }

    [Fact]
    public void ArchiveWorkspace_OnlySystemFiles_ReturnsNull()
    {
        var ws = CreateWorkspace();
        File.WriteAllText(Path.Combine(ws, "workflow.md"), "x");
        File.WriteAllText(Path.Combine(ws, "state.md"), "x");
        Directory.CreateDirectory(Path.Combine(ws, "modes"));
        Directory.CreateDirectory(Path.Combine(ws, "archive"));
        Directory.CreateDirectory(Path.Combine(ws, "inbox"));

        Assert.Null(WorkspaceArchiver.ArchiveWorkspace(ws));
    }

    [Fact]
    public void ArchiveWorkspace_MovesUserFiles()
    {
        var ws = CreateWorkspace();
        File.WriteAllText(Path.Combine(ws, "plan.md"), "plan");
        File.WriteAllText(Path.Combine(ws, "notes.md"), "notes");

        var result = WorkspaceArchiver.ArchiveWorkspace(ws);

        Assert.NotNull(result);
        Assert.True(File.Exists(Path.Combine(result, "plan.md")));
        Assert.True(File.Exists(Path.Combine(result, "notes.md")));
        Assert.False(File.Exists(Path.Combine(ws, "plan.md")));
    }

    [Fact]
    public void ArchiveWorkspace_MovesUserDirectories()
    {
        var ws = CreateWorkspace();
        var sub = Path.Combine(ws, "custom-dir");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "file.txt"), "content");

        var result = WorkspaceArchiver.ArchiveWorkspace(ws);

        Assert.NotNull(result);
        Assert.True(Directory.Exists(Path.Combine(result, "custom-dir")));
        Assert.False(Directory.Exists(Path.Combine(ws, "custom-dir")));
    }

    [Fact]
    public void ArchiveWorkspace_PreservesSystemEntries()
    {
        var ws = CreateWorkspace();
        File.WriteAllText(Path.Combine(ws, "state.md"), "state");
        File.WriteAllText(Path.Combine(ws, "plan.md"), "plan");

        WorkspaceArchiver.ArchiveWorkspace(ws);

        Assert.True(File.Exists(Path.Combine(ws, "state.md")));
    }

    [Fact]
    public void ArchiveWorkspace_PreservesWorktreeMarkers()
    {
        var ws = CreateWorkspace();
        var worktreeMarkers = new[]
        {
            ".worktree", ".worktree-path", ".worktree-base", ".worktree-root",
            ".worktree-hold", ".merge-source", ".needs-merge"
        };
        foreach (var marker in worktreeMarkers)
            File.WriteAllText(Path.Combine(ws, marker), "marker");
        File.WriteAllText(Path.Combine(ws, "plan.md"), "plan");

        var result = WorkspaceArchiver.ArchiveWorkspace(ws);

        Assert.NotNull(result);
        Assert.True(File.Exists(Path.Combine(result, "plan.md")));
        foreach (var marker in worktreeMarkers)
            Assert.True(File.Exists(Path.Combine(ws, marker)), $"{marker} must survive ArchiveWorkspace");
    }

    [Fact]
    public void ArchiveWorkspace_CreatesTimestampedSnapshot()
    {
        var ws = CreateWorkspace();
        File.WriteAllText(Path.Combine(ws, "plan.md"), "plan");

        var result = WorkspaceArchiver.ArchiveWorkspace(ws)!;

        Assert.Matches(@"\d{8}-\d{6}$", Path.GetFileName(result));
    }

    #endregion

    #region PruneArchive

    [Fact]
    public void PruneArchive_NoArchiveDir_DoesNotThrow()
    {
        var ws = CreateWorkspace();
        WorkspaceArchiver.PruneArchive(ws);
    }

    [Fact]
    public void PruneArchive_UnderLimit_KeepsAll()
    {
        var ws = CreateWorkspace();
        var snap = Path.Combine(ws, "archive", "20260101-100000");
        Directory.CreateDirectory(snap);
        File.WriteAllText(Path.Combine(snap, "a.md"), "a");

        WorkspaceArchiver.PruneArchive(ws, maxFiles: 30);

        Assert.True(Directory.Exists(snap));
    }

    [Fact]
    public void PruneArchive_OverLimit_DeletesOldest()
    {
        var ws = CreateWorkspace();
        var old = Path.Combine(ws, "archive", "20260101-100000");
        var newer = Path.Combine(ws, "archive", "20260101-120000");
        Directory.CreateDirectory(old);
        Directory.CreateDirectory(newer);
        for (var i = 0; i < 4; i++)
            File.WriteAllText(Path.Combine(old, $"f{i}.md"), "x");
        for (var i = 0; i < 4; i++)
            File.WriteAllText(Path.Combine(newer, $"f{i}.md"), "x");

        WorkspaceArchiver.PruneArchive(ws, maxFiles: 5);

        Assert.False(Directory.Exists(old));
        Assert.True(Directory.Exists(newer));
    }

    [Fact]
    public void PruneArchive_IgnoresInboxSubfolder()
    {
        var ws = CreateWorkspace();
        var inbox = Path.Combine(ws, "archive", "inbox");
        Directory.CreateDirectory(inbox);
        File.WriteAllText(Path.Combine(inbox, "msg.md"), "msg");

        var snap = Path.Combine(ws, "archive", "20260101-100000");
        Directory.CreateDirectory(snap);
        for (var i = 0; i < 35; i++)
            File.WriteAllText(Path.Combine(snap, $"f{i}.md"), "x");

        WorkspaceArchiver.PruneArchive(ws, maxFiles: 30);

        Assert.True(Directory.Exists(inbox));
    }

    [Fact]
    public void PruneArchive_EmptySnapshots_NoOp()
    {
        var ws = CreateWorkspace();
        Directory.CreateDirectory(Path.Combine(ws, "archive"));

        WorkspaceArchiver.PruneArchive(ws, maxFiles: 5);
    }

    #endregion
}
