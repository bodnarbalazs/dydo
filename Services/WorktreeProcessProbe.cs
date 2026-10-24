namespace DynaDocs.Services;

/// <summary>
/// Says whether a running process is using a worktree. Windows: the folder is renamed to a sibling and
/// straight back; Windows refuses that rename while any process has its working directory or an open
/// file inside. Linux: every readable <c>/proc/&lt;pid&gt;/cwd</c> is compared with the folder, so a
/// process with its working directory inside counts and one only holding a file open does not.
/// Elsewhere (macOS) nothing is detected, and help and the reference say so. A dry run never probes,
/// since the Windows probe moves the folder.
/// </summary>
internal static class WorktreeProcessProbe
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Why the worktree looks busy, or null when no process was found.</summary>
    public static string? FindUse(string worktree)
    {
        if (OperatingSystem.IsWindows())
            return RenameProbe(worktree);
        if (OperatingSystem.IsLinux())
            return ProcScan(worktree, "/proc");
        return null;
    }

    internal static string? RenameProbe(string worktree) =>
        RenameProbe(worktree, Directory.Move, TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// The rename probe with its mover and retry pause exposed. The move back is retried, since an indexer
    /// or virus scanner can grab the folder for a moment; if it still fails, <see cref="ProbeStrandedException"/>
    /// names where the folder is now.
    /// </summary>
    internal static string? RenameProbe(string worktree, Action<string, string> move, TimeSpan retryPause)
    {
        var probe = worktree.TrimEnd('\\', '/') + ".dydo-clean-probe-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            move(worktree, probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "a running process is using it (the folder cannot be renamed)";
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                move(probe, worktree);
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == RestoreAttempts)
                    throw new ProbeStrandedException(probe, ex.Message);
                Thread.Sleep(retryPause * attempt);
            }
        }
    }

    private const int RestoreAttempts = 5;

    internal static string? ProcScan(string worktree, string procRoot)
    {
        var root = worktree.TrimEnd('\\', '/');
        foreach (var process in Directory.EnumerateDirectories(procRoot))
        {
            if (!int.TryParse(Path.GetFileName(process), out var pid) || pid == Environment.ProcessId)
                continue;
            var cwd = ReadLink(Path.Combine(process, "cwd"))?.TrimEnd('\\', '/');
            if (cwd != null && (string.Equals(cwd, root, PathComparison)
                    || cwd.StartsWith(root + Path.DirectorySeparatorChar, PathComparison)))
                return $"a running process (pid {pid}) has its working directory inside";
        }

        return null;
    }

    // Another user's process hides its cwd link; that process is simply not seen.
    private static string? ReadLink(string link)
    {
        try
        {
            return new DirectoryInfo(link).LinkTarget;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>The rename probe moved a worktree aside and could not move it back; <see cref="Probe"/> is where it is.</summary>
internal sealed class ProbeStrandedException(string probe, string reason)
    : IOException($"the busy check renamed it to {probe} and could not move it back ({reason})")
{
    public string Probe => probe;
}
