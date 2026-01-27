namespace DynaDocs.Rules;

using DynaDocs.Models;

public interface IRule
{
    string Name { get; }
    string Description { get; }
    bool CanAutoFix { get; }

    IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath);
    IEnumerable<Violation> ValidateFolder(string folderPath, List<DocFile> allDocs, string basePath);
}
