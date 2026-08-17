namespace DynaDocs.Sync.Projection;

/// <summary>Projects external Markdown edits onto untouched local source spans.</summary>
public static class ProjectedMarkdownMerge
{
    public static ProjectedBodyResult Merge(DualBodyBase bodyBase, string currentLocal, string currentExternal, string? pageTitle = null) =>
        MarkdownPatchPlanner.Merge(bodyBase, currentLocal, currentExternal, pageTitle);

    public static ProjectedBodyResult Merge(string localBase, string externalBase, string currentLocal, string currentExternal,
        string? pageTitle = null) => MarkdownPatchPlanner.Merge(localBase, externalBase, currentLocal, currentExternal, pageTitle);
}
