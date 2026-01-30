namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

public class DocGraph : IDocGraph
{
    private readonly Dictionary<string, List<(string Target, int LineNumber)>> _outgoing = new();
    private readonly Dictionary<string, List<(string Source, int LineNumber)>> _incoming = new();
    private readonly HashSet<string> _allDocs = [];

    public void Build(List<DocFile> docs, string basePath)
    {
        _outgoing.Clear();
        _incoming.Clear();
        _allDocs.Clear();

        foreach (var doc in docs)
        {
            var normalizedPath = PathUtils.NormalizeForKey(doc.RelativePath);
            _allDocs.Add(normalizedPath);
            _outgoing[normalizedPath] = [];
        }

        foreach (var doc in docs)
        {
            var sourcePath = PathUtils.NormalizeForKey(doc.RelativePath);

            foreach (var link in doc.Links)
            {
                if (link.Type == LinkType.External) continue;

                var resolvedPath = ResolveLink(doc, link, basePath);
                if (resolvedPath == null) continue;

                var targetPath = PathUtils.NormalizeForKey(resolvedPath);
                if (!_allDocs.Contains(targetPath)) continue;

                _outgoing[sourcePath].Add((targetPath, link.LineNumber));

                if (!_incoming.ContainsKey(targetPath))
                    _incoming[targetPath] = [];

                _incoming[targetPath].Add((sourcePath, link.LineNumber));
            }
        }
    }

    public List<(string Doc, int LineNumber)> GetIncoming(string docPath)
    {
        var normalized = PathUtils.NormalizeForKey(docPath);
        return _incoming.GetValueOrDefault(normalized, []);
    }

    public List<(string Doc, int Degree)> GetWithinDegree(string docPath, int maxDegree)
    {
        var normalized = PathUtils.NormalizeForKey(docPath);
        if (!_allDocs.Contains(normalized))
            return [];

        var result = new List<(string, int)>();
        var visited = new HashSet<string> { normalized };
        var queue = new Queue<(string Doc, int Degree)>();

        queue.Enqueue((normalized, 0));

        while (queue.Count > 0)
        {
            var (doc, degree) = queue.Dequeue();

            if (degree > 0)
                result.Add((doc, degree));

            if (degree >= maxDegree)
                continue;

            var outgoing = _outgoing.GetValueOrDefault(doc, []);
            foreach (var (target, _) in outgoing)
            {
                if (visited.Add(target))
                    queue.Enqueue((target, degree + 1));
            }
        }

        return result;
    }

    public bool HasDoc(string docPath)
    {
        var normalized = PathUtils.NormalizeForKey(docPath);
        return _allDocs.Contains(normalized);
    }

    private static string? ResolveLink(DocFile sourceDoc, LinkInfo link, string basePath)
    {
        var target = link.Target;

        // Remove anchor if present
        var anchorIndex = target.IndexOf('#');
        if (anchorIndex >= 0)
            target = target[..anchorIndex];

        if (string.IsNullOrEmpty(target))
            return null;

        // Get the directory of the source doc relative to base
        var sourceRelativeDir = Path.GetDirectoryName(sourceDoc.RelativePath) ?? "";

        // Resolve the target relative to the source directory
        string resolved;
        if (target.StartsWith("./"))
        {
            resolved = Path.Combine(sourceRelativeDir, target[2..]);
        }
        else if (target.StartsWith("../"))
        {
            resolved = Path.Combine(sourceRelativeDir, target);
        }
        else
        {
            resolved = Path.Combine(sourceRelativeDir, target);
        }

        // Normalize the path (handle .. segments)
        var parts = PathUtils.NormalizePath(resolved).Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        var normalized = new List<string>();

        foreach (var part in parts)
        {
            if (part == "..")
            {
                if (normalized.Count > 0)
                    normalized.RemoveAt(normalized.Count - 1);
            }
            else if (part != ".")
            {
                normalized.Add(part);
            }
        }

        return string.Join("/", normalized);
    }
}
