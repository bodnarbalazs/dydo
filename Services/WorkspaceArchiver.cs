namespace DynaDocs.Services;

public static class WorkspaceArchiver
{
    private static readonly HashSet<string> SystemManagedEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        "workflow.md", "state.md", ".session", ".pending-session", ".claim.lock", "modes", "archive", "inbox",
        ".worktree", ".worktree-path", ".worktree-base", ".worktree-root", ".worktree-hold",
        ".merge-source", ".needs-merge",
        ".reply-pending",
        ".queued"
    };

    /// <summary>
    /// Archives non-system files from a workspace into archive/{timestamp}/.
    /// Returns the snapshot path, or null if nothing to archive.
    /// </summary>
    public static string? ArchiveWorkspace(string workspace)
    {
        if (!Directory.Exists(workspace))
            return null;

        var entries = Directory.GetFileSystemEntries(workspace)
            .Where(e => !SystemManagedEntries.Contains(Path.GetFileName(e)))
            .ToList();

        if (entries.Count == 0)
            return null;

        var snapshotName = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var snapshotPath = Path.Combine(workspace, "archive", snapshotName);
        Directory.CreateDirectory(snapshotPath);

        foreach (var entry in entries)
        {
            var name = Path.GetFileName(entry);
            var dest = Path.Combine(snapshotPath, name);

            if (File.Exists(entry))
                File.Move(entry, dest);
            else if (Directory.Exists(entry))
                Directory.Move(entry, dest);
        }

        return snapshotPath;
    }

    /// <summary>
    /// Prunes the archive directory so total files across all snapshots stays within maxFiles.
    /// Deletes oldest snapshots first.
    /// </summary>
    public static void PruneArchive(string workspace, int maxFiles = 30)
    {
        var archivePath = Path.Combine(workspace, "archive");
        if (!Directory.Exists(archivePath))
            return;

        var snapshots = Directory.GetDirectories(archivePath)
            .Where(d => !string.Equals(Path.GetFileName(d), "inbox", StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => Path.GetFileName(d))
            .ToList();

        if (snapshots.Count == 0)
            return;

        var totalFiles = snapshots.Sum(CountFilesRecursive);

        while (totalFiles > maxFiles && snapshots.Count > 0)
        {
            var oldest = snapshots[0];
            totalFiles -= CountFilesRecursive(oldest);
            Directory.Delete(oldest, recursive: true);
            snapshots.RemoveAt(0);
        }
    }

    private static int CountFilesRecursive(string directory)
    {
        try
        {
            return Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length;
        }
        catch
        {
            return 0;
        }
    }
}
