namespace DynaDocs.Rules;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Utils;

public class SummaryRule : RuleBase
{
    public override string Name => "Summary";
    public override string Description => "First paragraph after title must be a 1-3 sentence summary";

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        if (RuleSkipPaths.IsTemplateOrAddition(PathUtils.NormalizePath(doc.RelativePath)))
            yield break;

        if (string.IsNullOrEmpty(doc.Title))
        {
            yield return CreateError(doc, "Missing title (# heading)");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(doc.SummaryParagraph))
        {
            yield return CreateWarning(doc, "Missing summary paragraph after title");
            yield break;
        }

        if (doc.SummaryParagraph!.Trim() == IssueCreateHandler.SummaryPlaceholder)
        {
            yield return CreateWarning(doc, $"Summary is the '{IssueCreateHandler.SummaryPlaceholder}' placeholder — replace with a real one-line summary");
        }
    }
}
