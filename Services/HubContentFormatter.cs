namespace DynaDocs.Services;

using System.Text;
using DynaDocs.Models;
using DynaDocs.Utils;

/// <summary>
/// Formats document and subfolder links for hub files.
/// Extracted from HubGenerator to reduce class-level cyclomatic complexity.
/// </summary>
internal static class HubContentFormatter
{
    public static string FormatDocumentLinks(IEnumerable<DocFile> docs)
    {
        var sb = new StringBuilder();
        var docList = docs.OrderBy(d => d.FileName).ToList();

        if (docList.Count == 0)
        {
            sb.AppendLine("*No documents in this folder yet.*");
        }
        else
        {
            foreach (var doc in docList)
            {
                var title = doc.Title ?? KebabToTitleCase(Path.GetFileNameWithoutExtension(doc.FileName));
                var summary = doc.SummaryParagraph;
                var link = $"[{title}](./{doc.FileName})";

                if (!string.IsNullOrEmpty(summary))
                {
                    var firstSentence = GetFirstSentence(summary);
                    sb.AppendLine($"- {link} - {firstSentence}");
                }
                else
                {
                    sb.AppendLine($"- {link}");
                }
            }
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatSubfolderLinks(
        IEnumerable<DocFile> subfolderHubs,
        string parentFolder,
        List<DocFile> allDocs)
    {
        var sb = new StringBuilder();

        foreach (var hub in subfolderHubs.OrderBy(h => h.RelativePath))
        {
            var hubDir = Path.GetDirectoryName(hub.RelativePath) ?? "";
            var folderName = Path.GetFileName(hubDir);
            var title = HubGenerator.ToTitleCase(folderName);

            var metaFileName = $"_{folderName}.md";
            var metaFile = allDocs.FirstOrDefault(d =>
                PathUtils.NormalizePath(Path.GetDirectoryName(d.RelativePath) ?? "")
                    .Equals(PathUtils.NormalizePath(hubDir), StringComparison.OrdinalIgnoreCase)
                && d.FileName.Equals(metaFileName, StringComparison.OrdinalIgnoreCase));

            var relativePath = $"./{folderName}/_index.md";
            var link = $"[{title}]({relativePath})";

            if (metaFile?.SummaryParagraph != null)
            {
                var firstSentence = GetFirstSentence(metaFile.SummaryParagraph);
                sb.AppendLine($"- {link} - {firstSentence}");
            }
            else
            {
                sb.AppendLine($"- {link}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string GetFirstSentence(string text)
    {
        var endIndex = text.IndexOfAny(['.', '!', '?']);
        if (endIndex > 0 && endIndex < 150)
        {
            return text[..(endIndex + 1)];
        }
        return text.Length > 150 ? text[..147] + "..." : text;
    }

    private static string KebabToTitleCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(name.Replace("-", " "));
    }
}
