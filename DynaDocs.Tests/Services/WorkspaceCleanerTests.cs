namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class WorkspaceCleanerTests : IDisposable
{
    private readonly string _testDir;

    public WorkspaceCleanerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-cleaner-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    Directory.Delete(_testDir, true);
                    return;
                }
                catch (IOException) when (i < 2)
                {
                    Thread.Sleep(50 * (i + 1));
                }
            }
        }
    }

    [Fact]
    public void CleanWorkspace_RemovesStateFiles()
    {
        var workspace = Path.Combine(_testDir, "agent");
        Directory.CreateDirectory(workspace);

        File.WriteAllText(Path.Combine(workspace, "state.md"), "busy");
        File.WriteAllText(Path.Combine(workspace, ".session"), "abc");
        File.WriteAllText(Path.Combine(workspace, "plan.md"), "plan");
        File.WriteAllText(Path.Combine(workspace, "notes.md"), "notes");
        File.WriteAllText(Path.Combine(workspace, ".auto-close"), "yes");

        WorkspaceCleaner.CleanWorkspace(workspace);

        Assert.False(File.Exists(Path.Combine(workspace, "state.md")));
        Assert.False(File.Exists(Path.Combine(workspace, ".session")));
        Assert.False(File.Exists(Path.Combine(workspace, "plan.md")));
        Assert.False(File.Exists(Path.Combine(workspace, "notes.md")));
        Assert.False(File.Exists(Path.Combine(workspace, ".auto-close")));
    }

    [Fact]
    public void CleanWorkspace_ClearsInboxContents()
    {
        var workspace = Path.Combine(_testDir, "agent");
        var inbox = Path.Combine(workspace, "inbox");
        Directory.CreateDirectory(inbox);

        File.WriteAllText(Path.Combine(inbox, "msg1.md"), "msg");
        File.WriteAllText(Path.Combine(inbox, "msg2.md"), "msg");

        WorkspaceCleaner.CleanWorkspace(workspace);

        Assert.True(Directory.Exists(inbox)); // directory preserved
        Assert.Empty(Directory.GetFiles(inbox)); // files removed
    }

    [Fact]
    public void CleanWorkspace_RemovesModesDirectory()
    {
        var workspace = Path.Combine(_testDir, "agent");
        var modes = Path.Combine(workspace, "modes");
        Directory.CreateDirectory(modes);
        File.WriteAllText(Path.Combine(modes, "code-writer.md"), "mode");

        WorkspaceCleaner.CleanWorkspace(workspace);

        Assert.False(Directory.Exists(modes));
    }

    [Fact]
    public void CleanWorkspace_RemovesWaitingDirectory()
    {
        var workspace = Path.Combine(_testDir, "agent");
        var waiting = Path.Combine(workspace, ".waiting");
        Directory.CreateDirectory(waiting);
        File.WriteAllText(Path.Combine(waiting, "marker.json"), "{}");

        WorkspaceCleaner.CleanWorkspace(workspace);

        Assert.False(Directory.Exists(waiting));
    }

    [Fact]
    public void CleanWorkspace_RemovesReplyPendingDirectory()
    {
        var workspace = Path.Combine(_testDir, "agent");
        var replyPending = Path.Combine(workspace, ".reply-pending");
        Directory.CreateDirectory(replyPending);
        File.WriteAllText(Path.Combine(replyPending, "reply.md"), "pending");

        WorkspaceCleaner.CleanWorkspace(workspace);

        Assert.False(Directory.Exists(replyPending));
    }

    [Fact]
    public void CleanWorkspace_HandlesMissingDirectories()
    {
        var workspace = Path.Combine(_testDir, "agent");
        Directory.CreateDirectory(workspace);

        // Should not throw when directories don't exist
        WorkspaceCleaner.CleanWorkspace(workspace);
    }

    [Fact]
    public void CleanWorkspace_PreservesOtherFiles()
    {
        var workspace = Path.Combine(_testDir, "agent");
        Directory.CreateDirectory(workspace);

        File.WriteAllText(Path.Combine(workspace, "workflow.md"), "keep");
        File.WriteAllText(Path.Combine(workspace, "state.md"), "remove");

        WorkspaceCleaner.CleanWorkspace(workspace);

        Assert.True(File.Exists(Path.Combine(workspace, "workflow.md")));
        Assert.False(File.Exists(Path.Combine(workspace, "state.md")));
    }
}
