namespace DynaDocs.Rules;

using DynaDocs.Models;

public abstract class RuleBase : IRule
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual bool CanAutoFix => false;

    public virtual IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        return [];
    }

    public virtual IEnumerable<Violation> ValidateFolder(string folderPath, List<DocFile> allDocs, string basePath)
    {
        return [];
    }

    protected Violation CreateError(DocFile doc, string message, int? lineNumber = null, string? suggestedFix = null)
    {
        return new Violation(doc.RelativePath, Name, message, ViolationSeverity.Error, lineNumber, CanAutoFix, suggestedFix);
    }

    protected Violation CreateWarning(DocFile doc, string message, int? lineNumber = null)
    {
        return new Violation(doc.RelativePath, Name, message, ViolationSeverity.Warning, lineNumber);
    }

    protected Violation CreateFolderError(string folderPath, string message)
    {
        return new Violation(folderPath, Name, message, ViolationSeverity.Error);
    }
}
