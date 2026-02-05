namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Utils;

public class UncustomizedDocsRule : RuleBase
{
    public override string Name => "UncustomizedDocs";
    public override string Description => "Foundation docs should be customized";

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        var normalized = PathUtils.NormalizePath(doc.RelativePath);

        if (normalized.EndsWith("understand/about.md", StringComparison.OrdinalIgnoreCase))
        {
            if (doc.Content.Contains("[Describe the project in 2-3 sentences]"))
            {
                yield return CreateWarning(doc, "About.md is not customized. Consider updating it.");
            }
        }

        if (normalized.EndsWith("understand/architecture.md", StringComparison.OrdinalIgnoreCase))
        {
            if (doc.Content.Contains("**Fill this in.**"))
            {
                yield return CreateWarning(doc, "Architecture.md is not customized. Consider updating it.");
            }
        }
    }
}
