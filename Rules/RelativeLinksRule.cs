namespace DynaDocs.Rules;

using DynaDocs.Models;

public class RelativeLinksRule : RuleBase
{
    private static readonly string[] AssetExtensions =
        [".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".pdf", ".zip", ".tar", ".gz"];

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

                // Skip asset files (images, PDFs, etc.) - they don't need .md extension
                if (IsAssetLink(link.Target))
                    continue;

                if (!string.IsNullOrEmpty(link.Target) &&
                    !link.Target.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
                    !link.Target.StartsWith('#'))
                {
                    yield return CreateError(doc, $"Link missing .md extension: {link.Target}", link.LineNumber);
                }
            }
        }
    }

    private static bool IsAssetLink(string target)
    {
        return AssetExtensions.Any(ext => target.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
}
