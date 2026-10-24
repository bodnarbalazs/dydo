namespace DynaDocs.Services;

/// <summary>A link inside a worktree: its worktree-relative path, whether it links a directory or a file, and its target as stored.</summary>
internal sealed record ArchiveLink(string Path, string LinksTo, string Target);
