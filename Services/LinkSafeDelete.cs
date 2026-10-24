namespace DynaDocs.Services;

/// <summary>
/// Deletes a directory tree without ever following a link out of it. Every junction, symlink or other
/// reparse point inside is removed as a link (its target untouched) before anything recursive runs, and
/// on Windows every path goes through the <c>\\?\</c> form, so paths past 260 characters delete too.
/// </summary>
internal static class LinkSafeDelete
{
    public static bool IsLink(FileSystemInfo entry) => entry.Attributes.HasFlag(FileAttributes.ReparsePoint);

    /// <summary>Removes every link inside <paramref name="root"/>; real files and folders stay.</summary>
    public static void UnlinkAll(string root)
    {
        foreach (var entry in new DirectoryInfo(Extended(root)).EnumerateFileSystemInfos())
        {
            if (IsLink(entry))
                RemoveLink(entry);
            else if (entry is DirectoryInfo directory)
                UnlinkAll(directory.FullName);
        }
    }

    /// <summary>Deletes <paramref name="root"/> and everything under it; a missing root is already done.</summary>
    public static void DeleteTree(string root)
    {
        var top = new DirectoryInfo(Extended(root));
        if (!top.Exists)
            return;
        if (IsLink(top))
        {
            RemoveLink(top);
            return;
        }

        DeleteContents(top);
        top.Attributes = FileAttributes.Directory;
        top.Delete();
    }

    private static void DeleteContents(DirectoryInfo directory)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if (IsLink(entry))
            {
                RemoveLink(entry);
                continue;
            }

            if (entry is DirectoryInfo child)
                DeleteContents(child);
            entry.Attributes = entry is DirectoryInfo ? FileAttributes.Directory : FileAttributes.Normal;
            entry.Delete();
        }
    }

    // On Windows a directory link (junction or directory symlink) goes with a non-recursive
    // RemoveDirectory, which unlinks it; everywhere else unlink(2) removes any symlink. Neither
    // touches the target.
    private static void RemoveLink(FileSystemInfo link)
    {
        if (OperatingSystem.IsWindows() && link is DirectoryInfo directory)
            directory.Delete(recursive: false);
        else
            File.Delete(link.FullName);
    }

    internal static string Extended(string path)
    {
        var full = Path.GetFullPath(path);
        if (!OperatingSystem.IsWindows() || full.StartsWith(@"\\?\", StringComparison.Ordinal))
            return full;
        return full.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + full[2..] : @"\\?\" + full;
    }
}
