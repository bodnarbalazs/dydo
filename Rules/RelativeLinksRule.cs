namespace DynaDocs.Rules;

using DynaDocs.Models;

public class RelativeLinksRule : RuleBase
{
    public override string Name => "RelativeLinks";
    public override string Description => "All internal links must be relative markdown links with .md extension";
    public override bool CanAutoFix => true;

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        foreach (var link in doc.Links)
        {
            if (link.Type == LinkType.Wikilink)
            {
                yield return CreateError(doc, $"Wikilink found: {link.RawText}", link.LineNumber);
            }
            else if (link.Type == LinkType.Markdown && !link.Target.StartsWith("http"))
            {
                if (link.Target.StartsWith('/'))
                {
                    yield return CreateError(doc, $"Absolute path found: {link.Target}", link.LineNumber);
                }

                if (!string.IsNullOrEmpty(link.Target) &&
                    !link.Target.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
                    !link.Target.StartsWith('#'))
                {
                    yield return CreateError(doc, $"Link missing .md extension: {link.Target}", link.LineNumber);
                }
            }
        }
    }
}
