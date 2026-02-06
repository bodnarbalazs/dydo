namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class WorkspaceArchiveTests : IDisposable
{
    private readonly string _testDir;

    public WorkspaceArchiveTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-archive-test-" + Guid.NewGuid().ToString("N")[..8]);
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

    #region ArchiveWorkspace Tests

    [Fact]
    public void ArchiveWorkspace_ReturnsNull_ForNonexistentWorkspace()
    {
        var result = AgentRegistry.ArchiveWorkspace(Path.Combine(_testDir, "nonexistent"));
        Assert.Null(result);
    }

    [Fact]
    public void ArchiveWorkspace_ReturnsNull_ForEmptyWorkspace()
    {
        var workspace = CreateWorkspace();

        var result = AgentRegistry.ArchiveWorkspace(workspace);

        Assert.Null(result);
    }

    [Fact]
    public void ArchiveWorkspace_ReturnsNull_WhenOnlySystemManagedFilesExist()
    {
        var workspace = CreateWorkspace();
        File.WriteAllText(Path.Combine(workspace, "workflow.md"), "# Workflow");
        File.WriteAllText(Path.Combine(workspace, "state.md"), "# State");
        File.WriteAllText(Path.Combine(workspace, ".session"), "{}");
        File.WriteAllText(Path.Combine(workspace, ".pending-session"), "sid");
        File.WriteAllText(Path.Combine(workspace, ".claim.lock"), "{}");
        Directory.CreateDirectory(Path.Combine(workspace, "modes"));
        Directory.CreateDirectory(Path.Combine(workspace, "archive"));

        var result = AgentRegistry.ArchiveWorkspace(workspace);

        Assert.Null(result);
    }

    [Fact]
    public void ArchiveWorkspace_ArchivesUserFiles()
    {
        var workspace = CreateWorkspace();
        File.WriteAllText(Path.Combine(workspace, "plan.md"), "# Plan");
        File.WriteAllText(Path.Combine(workspace, "notes.md"), "# Notes");

        var result = AgentRegistry.ArchiveWorkspace(workspace);

        Assert.NotNull(result);
        // Files should be moved into snapshot
        Assert.True(File.Exists(Path.Combine(result, "plan.md")));
        Assert.True(File.Exists(Path.Combine(result, "notes.md")));
        // Files should be removed from workspace root
        Assert.False(File.Exists(Path.Combine(workspace, "plan.md")));
        Assert.False(File.Exists(Path.Combine(workspace, "notes.md")));
    }

    [Fact]
    public void ArchiveWorkspace_ArchivesDirectories_PreservesNestedStructure()
    {
        var workspace = CreateWorkspace();
        var screenshotsDir = Path.Combine(workspace, "screenshots");
        Directory.CreateDirectory(screenshotsDir);
        File.WriteAllText(Path.Combine(screenshotsDir, "img1.png"), "fake image");

        var customDir = Path.Combine(workspace, "custom-dir");
        Directory.CreateDirectory(customDir);
        File.WriteAllText(Path.Combine(customDir, "deep.txt"), "Deep file");

        var result = AgentRegistry.ArchiveWorkspace(workspace);

        Assert.NotNull(result);
        // Directories should be moved into snapshot
        Assert.True(Directory.Exists(Path.Combine(result, "screenshots")));
        Assert.True(File.Exists(Path.Combine(result, "screenshots", "img1.png")));
        Assert.True(Directory.Exists(Path.Combine(result, "custom-dir")));
        Assert.True(File.Exists(Path.Combine(result, "custom-dir", "deep.txt")));
        // Removed from workspace root
        Assert.False(Directory.Exists(Path.Combine(workspace, "screenshots")));
        Assert.False(Directory.Exists(Path.Combine(workspace, "custom-dir")));
    }

    [Fact]
    public void ArchiveWorkspace_ExcludesSystemEntries()
    {
        var workspace = CreateWorkspace();
        // System-managed entries
        File.WriteAllText(Path.Combine(workspace, "workflow.md"), "# Workflow");
        File.WriteAllText(Path.Combine(workspace, "state.md"), "# State");
        File.WriteAllText(Path.Combine(workspace, ".session"), "{}");
        Directory.CreateDirectory(Path.Combine(workspace, "modes"));
        Directory.CreateDirectory(Path.Combine(workspace, "archive"));
        // User files
        File.WriteAllText(Path.Combine(workspace, "plan.md"), "# Plan");

        var result = AgentRegistry.ArchiveWorkspace(workspace);

        Assert.NotNull(result);
        // Only user file should be in snapshot
        Assert.True(File.Exists(Path.Combine(result, "plan.md")));
        // System entries should remain in workspace
        Assert.True(File.Exists(Path.Combine(workspace, "workflow.md")));
        Assert.True(File.Exists(Path.Combine(workspace, "state.md")));
        Assert.True(File.Exists(Path.Combine(workspace, ".session")));
        Assert.True(Directory.Exists(Path.Combine(workspace, "modes")));
        Assert.True(Directory.Exists(Path.Combine(workspace, "archive")));
    }

    [Fact]
    public void ArchiveWorkspace_ExcludesInbox()
    {
        var workspace = CreateWorkspace();
        var inboxDir = Path.Combine(workspace, "inbox");
        Directory.CreateDirectory(inboxDir);
        File.WriteAllText(Path.Combine(inboxDir, "task.md"), "# Task");
        // Also add a user file so archive actually runs
        File.WriteAllText(Path.Combine(workspace, "plan.md"), "# Plan");

        var result = AgentRegistry.ArchiveWorkspace(workspace);

        Assert.NotNull(result);
        // Inbox should stay in workspace root
        Assert.True(Directory.Exists(Path.Combine(workspace, "inbox")));
        Assert.True(File.Exists(Path.Combine(workspace, "inbox", "task.md")));
        // Inbox should NOT be in the snapshot
        Assert.False(Directory.Exists(Path.Combine(result, "inbox")));
        // User file should be archived
        Assert.True(File.Exists(Path.Combine(result, "plan.md")));
    }

    [Fact]
    public void ArchiveWorkspace_CreatesTimestampedDirectory()
    {
        var workspace = CreateWorkspace();
        File.WriteAllText(Path.Combine(workspace, "plan.md"), "# Plan");

        var result = AgentRegistry.ArchiveWorkspace(workspace);

        Assert.NotNull(result);
        // Should be inside archive/
        var archiveDir = Path.Combine(workspace, "archive");
        Assert.True(Directory.Exists(archiveDir));
        Assert.StartsWith(archiveDir, result);
        // Snapshot name should be a timestamp pattern (yyyyMMdd-HHmmss)
        var snapshotName = Path.GetFileName(result);
        Assert.Matches(@"^\d{8}-\d{6}$", snapshotName);
    }

    #endregion

    #region PruneArchive Tests

    [Fact]
    public void PruneArchive_NoOp_WhenNoArchiveDir()
    {
        var workspace = CreateWorkspace();

        // Should not throw
        AgentRegistry.PruneArchive(workspace);
    }

    [Fact]
    public void PruneArchive_NoOp_WhenUnderLimit()
    {
        var workspace = CreateWorkspace();
        var archiveDir = Path.Combine(workspace, "archive");
        var snapshot = Path.Combine(archiveDir, "20260101-120000");
        Directory.CreateDirectory(snapshot);
        File.WriteAllText(Path.Combine(snapshot, "file1.md"), "Content");
        File.WriteAllText(Path.Combine(snapshot, "file2.md"), "Content");

        AgentRegistry.PruneArchive(workspace, maxFiles: 30);

        // Snapshot should still exist
        Assert.True(Directory.Exists(snapshot));
        Assert.Equal(2, Directory.GetFiles(snapshot).Length);
    }

    [Fact]
    public void PruneArchive_DeletesOldestSnapshot_WhenOverLimit()
    {
        var workspace = CreateWorkspace();
        var archiveDir = Path.Combine(workspace, "archive");

        // Create two snapshots: oldest with 3 files, newest with 3 files (total 6)
        var oldest = Path.Combine(archiveDir, "20260101-100000");
        Directory.CreateDirectory(oldest);
        File.WriteAllText(Path.Combine(oldest, "a.md"), "A");
        File.WriteAllText(Path.Combine(oldest, "b.md"), "B");
        File.WriteAllText(Path.Combine(oldest, "c.md"), "C");

        var newest = Path.Combine(archiveDir, "20260101-120000");
        Directory.CreateDirectory(newest);
        File.WriteAllText(Path.Combine(newest, "d.md"), "D");
        File.WriteAllText(Path.Combine(newest, "e.md"), "E");
        File.WriteAllText(Path.Combine(newest, "f.md"), "F");

        AgentRegistry.PruneArchive(workspace, maxFiles: 5);

        // Oldest snapshot should be deleted (6 > 5, remove oldest 3 files -> 3 remaining <= 5)
        Assert.False(Directory.Exists(oldest));
        // Newest snapshot should survive
        Assert.True(Directory.Exists(newest));
    }

    [Fact]
    public void PruneArchive_DeletesMultipleOldestSnapshots_WhenFarOverLimit()
    {
        var workspace = CreateWorkspace();
        var archiveDir = Path.Combine(workspace, "archive");

        // Create 3 snapshots with 5 files each (total 15)
        var snap1 = Path.Combine(archiveDir, "20260101-100000");
        Directory.CreateDirectory(snap1);
        for (var i = 0; i < 5; i++)
            File.WriteAllText(Path.Combine(snap1, $"f{i}.md"), "X");

        var snap2 = Path.Combine(archiveDir, "20260101-110000");
        Directory.CreateDirectory(snap2);
        for (var i = 0; i < 5; i++)
            File.WriteAllText(Path.Combine(snap2, $"f{i}.md"), "X");

        var snap3 = Path.Combine(archiveDir, "20260101-120000");
        Directory.CreateDirectory(snap3);
        for (var i = 0; i < 5; i++)
            File.WriteAllText(Path.Combine(snap3, $"f{i}.md"), "X");

        AgentRegistry.PruneArchive(workspace, maxFiles: 5);

        // First two snapshots should be deleted (15 -> 10 -> 5)
        Assert.False(Directory.Exists(snap1));
        Assert.False(Directory.Exists(snap2));
        // Newest snapshot should survive
        Assert.True(Directory.Exists(snap3));
    }

    [Fact]
    public void PruneArchive_PreservesNewestSnapshots()
    {
        var workspace = CreateWorkspace();
        var archiveDir = Path.Combine(workspace, "archive");

        // Create 3 snapshots: 2 files each (total 6)
        var snap1 = Path.Combine(archiveDir, "20260101-100000");
        Directory.CreateDirectory(snap1);
        File.WriteAllText(Path.Combine(snap1, "a.md"), "A");
        File.WriteAllText(Path.Combine(snap1, "b.md"), "B");

        var snap2 = Path.Combine(archiveDir, "20260101-110000");
        Directory.CreateDirectory(snap2);
        File.WriteAllText(Path.Combine(snap2, "c.md"), "C");
        File.WriteAllText(Path.Combine(snap2, "d.md"), "D");

        var snap3 = Path.Combine(archiveDir, "20260101-120000");
        Directory.CreateDirectory(snap3);
        File.WriteAllText(Path.Combine(snap3, "e.md"), "E");
        File.WriteAllText(Path.Combine(snap3, "f.md"), "F");

        AgentRegistry.PruneArchive(workspace, maxFiles: 4);

        // Oldest removed (6 -> 4), two newest preserved
        Assert.False(Directory.Exists(snap1));
        Assert.True(Directory.Exists(snap2));
        Assert.True(Directory.Exists(snap3));
    }

    #endregion
}
