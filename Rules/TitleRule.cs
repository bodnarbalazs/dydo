namespace DynaDocs.Rules;

using DynaDocs.Models;

public class TitleRule : RuleBase
{
    public override string Name => "Title";
    public override string Description => "Every document must have a title (# heading)";

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        if (string.IsNullOrEmpty(doc.Title))
        {
            yield return CreateError(doc, "Missing title (# heading)");
        }
    }
}
