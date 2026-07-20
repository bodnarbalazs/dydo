namespace DynaDocs.Sync;

/// <summary>
/// Canonicalises a Notion parent-page id for parent-scoped state keying (issue 0257). A page id is written
/// dashed or undashed and in any letter case, yet all forms denote the same page — so both the
/// override-vs-configured equality check and the <c>hash8</c> that scopes every state path must key off one
/// canonical form, or the same board would resolve two disjoint states. The single source of that hash for
/// BOTH the spine (<see cref="Notion.NotionSpineState"/>) and the docs mirror
/// (<see cref="Notion.DocsTreeSync.SnapshotAdapterName"/>).
/// </summary>
public static class ParentPageKey
{
    public static string Normalize(string parentPageId) =>
        parentPageId.Replace("-", "").ToLowerInvariant();

    public static string Hash8(string parentPageId)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Normalize(parentPageId)));
        return Convert.ToHexString(bytes)[..8].ToLowerInvariant();
    }
}
