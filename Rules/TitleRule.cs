namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Utils;

public class TitleRule : RuleBase
{
    public override string Name => "Title";
    public override string Description => "Every document must have a title (# heading)";

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        if (RuleSkipPaths.IsTemplateAddition(PathUtils.NormalizePath(doc.RelativePath)))
            yield break;

        if (string.IsNullOrEmpty(doc.Title))
        {
            yield return CreateError(doc, "Missing title (# heading)");
        }
    }
}
