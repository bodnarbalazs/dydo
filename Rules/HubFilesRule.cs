namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Utils;

public class HubFilesRule : RuleBase
{
    public override string Name => "HubFiles";
    public override string Description => "Every folder containing docs must have an _index.md file";
    public override bool CanAutoFix => true;

    public override IEnumerable<Violation> ValidateFolder(string folderPath, List<DocFile> allDocs, string basePath)
    {
        var relativeFolderPath = PathUtils.NormalizePath(Path.GetRelativePath(basePath, folderPath));

        if (relativeFolderPath == ".")
        {
            yield break;
        }

        var docsInFolder = allDocs.Where(d =>
        {
            var docDir = Path.GetDirectoryName(d.RelativePath) ?? "";
            docDir = PathUtils.NormalizePath(docDir);
            return docDir.Equals(relativeFolderPath, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        if (docsInFolder.Count == 0) yield break;

        var hasIndex = docsInFolder.Any(d => d.FileName == "_index.md");
        if (!hasIndex)
        {
            yield return CreateFolderError(relativeFolderPath + "/", "Missing _index.md hub file");
        }
    }
}
