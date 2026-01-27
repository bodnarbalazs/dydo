namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Services;

public class BrokenLinksRule : RuleBase
{
    private readonly ILinkResolver _linkResolver;

    public override string Name => "BrokenLinks";
    public override string Description => "All internal links must point to existing files and anchors";

    public BrokenLinksRule(ILinkResolver linkResolver)
    {
        _linkResolver = linkResolver;
    }

    public override IEnumerable<Violation> Validate(DocFile doc, List<DocFile> allDocs, string basePath)
    {
        foreach (var link in doc.Links.Where(l => l.Type != LinkType.External))
        {
            if (link.Type == LinkType.Wikilink)
            {
                continue;
            }

            if (!_linkResolver.ResolveLink(doc, link, allDocs, basePath))
            {
                var anchorInfo = link.Anchor != null ? $"#{link.Anchor}" : "";
                yield return CreateError(doc, $"Broken link: {link.Target}{anchorInfo}", link.LineNumber);
            }
        }
    }
}
