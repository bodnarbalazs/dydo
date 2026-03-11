namespace DynaDocs.Commands;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Handles file-level fixes: naming conventions and wikilink conversion.
/// </summary>
internal static class FixFileHandler
{
    public static int FixNaming(List<DocFile> docs)
    {
        var fixedCount = 0;

        foreach (var doc in docs.ToList())
        {
            if (IsExcludedPath(doc.RelativePath))
                continue;

            if (!PathUtils.IsKebabCase(doc.FileName))
            {
                var newName = PathUtils.ToKebabCase(Path.GetFileNameWithoutExtension(doc.FileName)) + ".md";
                var newPath = Path.Combine(Path.GetDirectoryName(doc.FilePath)!, newName);

                File.Move(doc.FilePath, newPath);
                ConsoleOutput.WriteSuccess($"  ✓ Renamed {doc.FileName} -> {newName}");
                fixedCount++;
            }
        }

        return fixedCount;
    }

    public static (int converted, List<string> manualFixes) FixWikilinks(List<DocFile> docs)
    {
        var linkResolver = new LinkResolver();
        var linksConverted = 0;
        var manualFixes = new List<string>();

        foreach (var doc in docs)
        {
            var wikilinks = doc.Links.Where(l => l.Type == LinkType.Wikilink).ToList();
            if (wikilinks.Count == 0) continue;

            var content = doc.Content;

            foreach (var link in wikilinks)
            {
                var resolvedPath = linkResolver.FindFileByName(link.Target + ".md", docs);
                if (resolvedPath != null)
                {
                    var relativePath = PathUtils.GetRelativePath(doc.RelativePath, resolvedPath);
                    var newLink = $"[{link.DisplayText}]({relativePath})";
                    content = content.Replace(link.RawText, newLink);
                    linksConverted++;
                }
                else
                {
                    manualFixes.Add($"{doc.RelativePath} - Ambiguous wikilink: {link.RawText}");
                }
            }

            if (content != doc.Content)
            {
                File.WriteAllText(doc.FilePath, content);
            }
        }

        return (linksConverted, manualFixes);
    }

    public static List<string> FindManualFixes(List<DocFile> docs)
    {
        var manualFixNeeded = new List<string>();

        foreach (var doc in docs)
        {
            if (IsExcludedPath(doc.RelativePath))
                continue;

            if (!doc.HasFrontmatter)
                manualFixNeeded.Add($"{doc.RelativePath} - Add frontmatter");
            else if (string.IsNullOrEmpty(doc.SummaryParagraph))
                manualFixNeeded.Add($"{doc.RelativePath} - Add summary paragraph");
        }

        return manualFixNeeded;
    }

    private static bool IsExcludedPath(string relativePath)
    {
        var normalized = PathUtils.NormalizePath(relativePath);

        if (normalized.StartsWith("_system/templates/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("_system/template-additions/", StringComparison.OrdinalIgnoreCase))
            return true;

        if (normalized.StartsWith("agents/", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
