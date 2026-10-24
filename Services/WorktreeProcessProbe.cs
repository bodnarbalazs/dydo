namespace DynaDocs.Services;

/// <summary>
/// Says whether a running process is using a worktree. Windows: the folder is renamed to a sibling and
/// straight back; Windows refuses that rename while any process has its working directory or an open
/// file inside. Linux: every readable <c>/proc/&lt;pid&gt;/cwd</c> is compared with the folder, so a
/// process with its working directory inside counts and one only holding a file open does not.
/// Elsewhere (macOS) nothing is detected, and help and the reference say so.
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

    internal static string? RenameProbe(string worktree)
    {
        var probe = worktree.TrimEnd('\\', '/') + ".dydo-clean-probe-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            Directory.Move(worktree, probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "a running process is using it (the folder cannot be renamed)";
        }

        Directory.Move(probe, worktree);
        return null;
    }

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
