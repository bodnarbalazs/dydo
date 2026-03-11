namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

internal static class DocLinkResolver
{
    public static string? Resolve(DocFile sourceDoc, LinkInfo link, string basePath)
    {
        var target = link.Target;

        // Remove anchor if present
        var anchorIndex = target.IndexOf('#');
        if (anchorIndex >= 0)
            target = target[..anchorIndex];

        if (string.IsNullOrEmpty(target))
            return null;

        var sourceRelativeDir = Path.GetDirectoryName(sourceDoc.RelativePath) ?? "";

        // Strip leading "./" for cleaner combine
        var resolved = target.StartsWith("./")
            ? Path.Combine(sourceRelativeDir, target[2..])
            : Path.Combine(sourceRelativeDir, target);

        // Normalize the path (handle .. segments)
        var parts = PathUtils.NormalizePath(resolved).Split('/', StringSplitOptions.RemoveEmptyEntries);
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
