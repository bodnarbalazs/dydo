namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Utils;

public class FolderMetaFilesRule : RuleBase
{
    public override string Name => "FolderMetaFiles";
    public override string Description => "Direct children of main folders must have a meta file (_foldername.md)";
    public override bool CanAutoFix => true;

    private static readonly string[] MainFolders = ["guides", "project", "reference", "understand"];

    public override IEnumerable<Violation> ValidateFolder(string folderPath, List<DocFile> allDocs, string basePath)
    {
        var relativeFolderPath = PathUtils.NormalizePath(Path.GetRelativePath(basePath, folderPath));

        // Only validate direct children of main folders
        if (!IsDirectChildOfMainFolder(relativeFolderPath))
            yield break;

        // Skip hidden folders (.obsidian, etc.)
        var folderName = Path.GetFileName(relativeFolderPath);
        if (folderName.StartsWith("."))
            yield break;

        // Skip folders with no docs (excluding the meta file itself)
        var expectedMetaFile = $"_{folderName}.md";
        var hasDocs = allDocs.Any(d =>
            d.FileName != expectedMetaFile &&
            PathUtils.NormalizePath(Path.GetDirectoryName(d.RelativePath) ?? "")
                .Equals(relativeFolderPath, StringComparison.OrdinalIgnoreCase));

        if (!hasDocs)
            yield break;

        // Check for meta file
        var hasMetaFile = allDocs.Any(d =>
            d.FileName == expectedMetaFile &&
            PathUtils.NormalizePath(Path.GetDirectoryName(d.RelativePath) ?? "")
                .Equals(relativeFolderPath, StringComparison.OrdinalIgnoreCase));

        if (!hasMetaFile)
        {
            yield return CreateFolderError(relativeFolderPath + "/",
                $"Missing folder meta file: {expectedMetaFile}");
        }
    }

    private static bool IsDirectChildOfMainFolder(string relativePath)
    {
        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return false;
        return MainFolders.Contains(parts[0], StringComparer.OrdinalIgnoreCase);
    }
}
