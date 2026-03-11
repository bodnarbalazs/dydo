namespace DynaDocs.Commands;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Handles hub (_index.md) regeneration and meta file creation for FixCommand.
/// </summary>
internal static class FixHubHandler
{
    private static readonly string[] MainFolders = ["guides", "project", "reference", "understand"];

    public static int RegenerateHubs(string basePath, DocScanner scanner, List<DocFile> docs)
    {
        var fixedCount = 0;

        var folders = scanner.GetAllFolders(basePath)
            .OrderByDescending(f => f.Count(c => c == Path.DirectorySeparatorChar || c == '/'))
            .ToList();

        var generatedHubs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in folders)
        {
            var relativeFolderPath = PathUtils.NormalizePath(Path.GetRelativePath(basePath, folder));

            if (relativeFolderPath == ".") continue;
            if (IsExcludedFolder(relativeFolderPath)) continue;

            var docsInFolder = docs.Where(d =>
            {
                var docDir = Path.GetDirectoryName(d.RelativePath) ?? "";
                docDir = PathUtils.NormalizePath(docDir);
                return docDir.Equals(relativeFolderPath, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            var existingHub = docsInFolder.FirstOrDefault(d => d.FileName == "_index.md");

            var contentDocs = docsInFolder.Where(d =>
                !d.IsHubFile && !d.IsIndexFile && !d.FileName.StartsWith("_")).ToList();
            var subfolderHubs = HubGenerator.GetSubfolderHubs(relativeFolderPath, docs, generatedHubs);

            if (contentDocs.Count == 0 && !subfolderHubs.Any() && existingHub == null) continue;

            var hubPath = Path.Combine(folder, "_index.md");
            var newContent = HubGenerator.GenerateHub(relativeFolderPath, contentDocs, subfolderHubs, docs);

            var hubRelativePath = PathUtils.NormalizePath(Path.Combine(relativeFolderPath, "_index.md"));
            generatedHubs[hubRelativePath] = newContent;

            if (existingHub == null || existingHub.Content != newContent)
            {
                File.WriteAllText(hubPath, newContent);
                var linkCount = contentDocs.Count;
                var subfolderCount = subfolderHubs.Count();
                var action = existingHub == null ? "Created" : "Updated";
                ConsoleOutput.WriteSuccess($"  ✓ {action} {relativeFolderPath}/_index.md ({linkCount} docs, {subfolderCount} subfolders)");
                fixedCount++;
            }
        }

        return fixedCount;
    }

    public static int CreateMissingMetaFiles(string basePath, DocScanner scanner, List<DocFile> docs)
    {
        var fixedCount = 0;

        var folders = scanner.GetAllFolders(basePath);

        foreach (var folder in folders)
        {
            var relativeFolderPath = PathUtils.NormalizePath(Path.GetRelativePath(basePath, folder));
            var parts = relativeFolderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2 || !MainFolders.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
                continue;

            var folderName = parts[1];
            if (folderName.StartsWith("."))
                continue;

            var metaFileName = $"_{folderName}.md";
            var metaFilePath = Path.Combine(folder, metaFileName);

            var docsInFolder = docs.Where(d =>
            {
                var docDir = Path.GetDirectoryName(d.RelativePath) ?? "";
                docDir = PathUtils.NormalizePath(docDir);
                return docDir.Equals(relativeFolderPath, StringComparison.OrdinalIgnoreCase) &&
                       d.FileName != metaFileName;
            }).ToList();

            if (docsInFolder.Count == 0)
                continue;

            if (!File.Exists(metaFilePath))
            {
                var content = GenerateMetaFileScaffold(parts[0], folderName);
                File.WriteAllText(metaFilePath, content);
                ConsoleOutput.WriteSuccess($"  ✓ Created {relativeFolderPath}/{metaFileName}");
                fixedCount++;
            }
        }

        return fixedCount;
    }

    private static string GenerateMetaFileScaffold(string area, string folderName)
    {
        var title = ToTitleCase(folderName);
        return $"""
            ---
            area: {area}
            type: folder-meta
            ---

            # {title}

            TODO: Describe the purpose of this folder.
            """;
    }

    private static string ToTitleCase(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
            return folderName;

        return System.Globalization.CultureInfo.CurrentCulture
            .TextInfo.ToTitleCase(folderName.Replace("-", " "));
    }

    private static bool IsExcludedFolder(string relativeFolderPath)
    {
        if (relativeFolderPath.StartsWith("_system", StringComparison.OrdinalIgnoreCase))
            return true;

        if (relativeFolderPath.StartsWith("agents", StringComparison.OrdinalIgnoreCase))
            return true;

        if (relativeFolderPath.StartsWith("_assets", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
