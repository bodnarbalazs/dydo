namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

public class LinkResolver : ILinkResolver
{
    public bool ResolveLink(DocFile sourceDoc, LinkInfo link, List<DocFile> allDocs, string basePath)
    {
        if (link.Type == LinkType.External) return true;

        var resolvedPath = PathUtils.ResolvePath(
            Path.Combine(basePath, sourceDoc.RelativePath),
            link.Target
        );

        var targetDoc = allDocs.FirstOrDefault(d =>
            PathUtils.NormalizePath(d.FilePath).Equals(resolvedPath, StringComparison.OrdinalIgnoreCase));

        if (targetDoc == null) return false;

        if (link.Anchor != null)
        {
            return ValidateAnchor(link.Anchor, targetDoc);
        }

        return true;
    }

    public string? FindFileByName(string fileName, List<DocFile> allDocs)
    {
        var matches = allDocs.Where(d =>
            d.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase) ||
            d.FileName.Equals(fileName + ".md", StringComparison.OrdinalIgnoreCase)
        ).ToList();

        return matches.Count == 1 ? matches[0].RelativePath : null;
    }

    public bool ValidateAnchor(string? anchor, DocFile targetDoc)
    {
        if (anchor == null) return true;
        return targetDoc.Anchors.Contains(anchor, StringComparer.OrdinalIgnoreCase);
    }
}
