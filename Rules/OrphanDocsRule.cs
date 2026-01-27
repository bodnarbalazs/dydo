namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Utils;

public class OrphanDocsRule : RuleBase
{
    public override string Name => "OrphanDocs";
    public override string Description => "Every doc should be reachable from index.md";

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        if (doc.IsIndexFile) yield break;

        var indexDoc = allDocs.FirstOrDefault(d => d.IsIndexFile);
        if (indexDoc == null) yield break;

        var reachableDocs = FindReachableDocs(indexDoc, allDocs, basePath);

        if (!reachableDocs.Contains(doc.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            yield return CreateWarning(doc, "Orphan doc: not reachable from index.md");
        }
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
