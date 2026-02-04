namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Utils;

public class OrphanDocsRule : RuleBase
{
    // Cache per-folder reachable docs to avoid recomputing for every document
    private Dictionary<string, HashSet<string>>? _cachedReachableDocsByFolder;
    private List<DocFile>? _cachedAllDocs;
    private string? _cachedBasePath;

    private static readonly string[] MainDocFolders = ["guides", "project", "reference", "understand"];

    public override string Name => "OrphanDocs";
    public override string Description => "Every doc in main folders must be reachable from its folder's _index.md";

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        // Skip index and hub files
        if (doc.IsIndexFile || doc.IsHubFile) yield break;

        // Only check files in the four main documentation folders
        var mainFolder = GetMainFolder(doc.RelativePath);
        if (mainFolder == null) yield break;

        // Find the hub file for this main folder
        var hubPath = mainFolder + "/_index.md";
        var hubDoc = allDocs.FirstOrDefault(d =>
            PathUtils.NormalizePath(d.RelativePath).Equals(hubPath, StringComparison.OrdinalIgnoreCase));

        // No hub file = can't validate (HubFilesRule will catch this)
        if (hubDoc == null) yield break;

        var reachableDocs = GetCachedReachableDocsForFolder(mainFolder, hubDoc, allDocs, basePath);

        if (!reachableDocs.Contains(doc.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            yield return CreateWarning(doc, $"Orphan doc: not reachable from {hubPath}");
        }
    }

    /// <summary>
    /// Get the main documentation folder from a relative path.
    /// Returns null if the file is not in one of the four main folders.
    /// </summary>
    private static string? GetMainFolder(string relativePath)
    {
        var normalized = PathUtils.NormalizePath(relativePath);
        foreach (var folder in MainDocFolders)
        {
            if (normalized.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
                return folder;
        }
        return null;
    }

    private HashSet<string> GetCachedReachableDocsForFolder(
        string mainFolder,
        DocFile hubDoc,
        List<DocFile> allDocs,
        string basePath)
    {
        // Initialize cache if needed or if context changed
        if (_cachedReachableDocsByFolder == null ||
            !ReferenceEquals(_cachedAllDocs, allDocs) ||
            _cachedBasePath != basePath)
        {
            _cachedReachableDocsByFolder = new Dictionary<string, HashSet<string>>(
                StringComparer.OrdinalIgnoreCase);
            _cachedAllDocs = allDocs;
            _cachedBasePath = basePath;
        }

        // Check cache for this folder
        if (!_cachedReachableDocsByFolder.TryGetValue(mainFolder, out var reachableDocs))
        {
            reachableDocs = FindReachableDocs(hubDoc, allDocs, basePath);
            _cachedReachableDocsByFolder[mainFolder] = reachableDocs;
        }

        return reachableDocs;
    }

    private static HashSet<string> FindReachableDocs(DocFile startDoc, List<DocFile> allDocs, string basePath)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<DocFile>();

        queue.Enqueue(startDoc);
        visited.Add(startDoc.RelativePath);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var link in current.Links.Where(l => l.Type == LinkType.Markdown))
            {
                if (string.IsNullOrEmpty(link.Target) || link.Target.StartsWith("http"))
                    continue;

                var resolvedPath = PathUtils.ResolvePath(
                    Path.Combine(basePath, current.RelativePath),
                    link.Target
                );

                var targetDoc = allDocs.FirstOrDefault(d =>
                    PathUtils.NormalizePath(d.FilePath).Equals(resolvedPath, StringComparison.OrdinalIgnoreCase));

                if (targetDoc != null && !visited.Contains(targetDoc.RelativePath))
                {
                    visited.Add(targetDoc.RelativePath);
                    queue.Enqueue(targetDoc);
                }
            }
        }

        return visited;
    }
}
