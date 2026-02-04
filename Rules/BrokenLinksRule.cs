namespace DynaDocs.Rules;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

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
        var normalized = PathUtils.NormalizePath(doc.RelativePath);

        // Skip template files - links are relative to deployment location, not storage
        if (normalized.StartsWith("_system/templates/", StringComparison.OrdinalIgnoreCase))
            yield break;

        foreach (var link in doc.Links.Where(l => l.Type != LinkType.External))
        {
            if (link.Type == LinkType.Wikilink)
            {
                continue;
            }

            // For non-markdown links (images, etc.), check if file exists on disk
            if (!link.Target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                var resolvedPath = PathUtils.ResolvePath(
                    Path.Combine(basePath, doc.RelativePath),
                    link.Target
                );

                if (!File.Exists(resolvedPath))
                {
                    yield return CreateError(doc, $"Broken link: {link.Target}", link.LineNumber);
                }
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
