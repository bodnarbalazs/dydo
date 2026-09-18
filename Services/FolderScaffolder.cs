namespace DynaDocs.Services;

public class FolderScaffolder : IFolderScaffolder
{
    // Folders the shipped scaffold cannot carry: they hold no file, so Git has no tree entry for
    // them and the embedded resource walk never reaches them.
    private static readonly string[] EmptyFolders =
    [
        "project/releases",
        "_system/.local",
        "_assets"
    ];

    public void Scaffold(string basePath)
    {
        foreach (var folder in EmptyFolders)
            Directory.CreateDirectory(Path.Combine(basePath, folder));

        // The single SHARED scratch folder (gitignored): agents drop temporary work products
        // here instead of polluting the repo root. No per-agent subfolders (DR-041 — identity
        // is assigned at spawn, nothing owns a named workspace).
        Directory.CreateDirectory(Path.Combine(basePath, "agents", "workspace"));

        ScaffoldTree.WriteDydoTree(basePath);
    }
}
