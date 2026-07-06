namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// The page-tree <see cref="ISyncAdapter"/> (DR 033) — the docs mirror's analogue of
/// <see cref="NotionSyncAdapter"/>. Where the spine adapter maps the rows of ONE data source, this maps the
/// managed pages of a nested-page TREE: it enumerates the tree via <see cref="INotionClient.GetChildPages"/>,
/// reads each managed page's body via <see cref="INotionClient.GetBlockChildren"/> +
/// <see cref="NotionBlockConverter.FromBlocks"/>, creates a doc's page under its repo-owned parent via
/// <see cref="INotionClient.CreatePage"/> + <see cref="NotionParent.Page"/>, replaces a body via the
/// append-then-delete pattern, and archives a removed doc's page via <c>UpdatePage { Archived = true }</c>.
/// The 3-way merge, base-snapshot store, and converter round-trip are reused as-is — no new merge machinery.
///
/// <para>Structure is repo-owned (DR 033 §2): a page's parent and the tree's shape come from the repo, so the
/// adapter is handed a repo-path → parent-page-id map. Folder pages are pre-created by <see cref="DocsTreeSync"/>
/// top-down, so every parent id is already known when a leaf's page is created. A page the sync did not create —
/// a colleague's stray page — is absent from <see cref="_managedPageIds"/> and is ignored, never adopted, so the
/// walk never manufactures a repo file from Notion-born structure.</para>
///
/// <para>A plain Notion page has no property schema, so frontmatter has nowhere to live on the external side:
/// <see cref="NormalizeFields"/> drops every field. The engine then treats frontmatter as adapter-invisible —
/// <c>OverlayAdapterInvisibleFields</c> preserves it on the repo file and never blanks it against the field-less
/// page — so only the body is genuinely bidirectional, exactly as it is for a body-only doc.</para>
/// </summary>
public sealed class DocsPageAdapter : ISyncAdapter
{
    private readonly INotionClient _client;
    private readonly string _rootPageId;
    private readonly IReadOnlyDictionary<string, string> _parentPageIdByLocalId;
    private readonly IReadOnlyDictionary<string, string> _titleByLocalId;
    private readonly IReadOnlySet<string> _managedPageIds;

    /// <param name="rootPageId">The "Docs" root page all top-level docs nest under; read for its own body too.</param>
    /// <param name="parentPageIdByLocalId">Where to create each doc's page — its repo-owned parent's Notion page id.</param>
    /// <param name="titleByLocalId">The Notion page title to set when a doc's page is first created.</param>
    /// <param name="managedPageIds">Page ids the sync already tracks, so the tree walk ignores unmanaged pages.</param>
    public DocsPageAdapter(
        INotionClient client,
        string rootPageId,
        IReadOnlyDictionary<string, string> parentPageIdByLocalId,
        IReadOnlyDictionary<string, string> titleByLocalId,
        IReadOnlySet<string> managedPageIds)
    {
        _client = client;
        _rootPageId = rootPageId;
        _parentPageIdByLocalId = parentPageIdByLocalId;
        _titleByLocalId = titleByLocalId;
        _managedPageIds = managedPageIds;
    }

    public IReadOnlyList<SyncRecord> ReadExternalState()
    {
        var records = new List<SyncRecord>();
        // The root page's own body (the dydo-root index) is not reachable by walking its children, so read it first.
        if (_managedPageIds.Contains(_rootPageId))
            records.Add(ReadPage(_rootPageId));
        foreach (var pageId in WalkManagedTree(_rootPageId))
            records.Add(ReadPage(pageId));
        return records;
    }

    private SyncRecord ReadPage(string pageId) => new()
    {
        ExternalId = pageId,
        Fields = [],
        Body = NotionBlockConverter.FromBlocks(_client.GetBlockChildren(pageId)),
    };

    /// <summary>Depth-first walk of the managed page tree from a parent via child_page enumeration (DR 033 §3).
    /// Only pages the sync created (the managed set) are yielded and descended into; a colleague's stray page is
    /// ignored, so structure is never reverse-engineered from Notion.</summary>
    private IEnumerable<string> WalkManagedTree(string parentPageId)
    {
        foreach (var child in _client.GetChildPages(parentPageId))
        {
            if (!_managedPageIds.Contains(child.Id))
                continue;
            yield return child.Id;
            foreach (var descendant in WalkManagedTree(child.Id))
                yield return descendant;
        }
    }

    public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned)
    {
        foreach (var upsert in changes.Upserts)
        {
            var blocks = NotionBlockConverter.ToBlocks(upsert.Body);
            if (upsert.ExternalId == null)
            {
                var page = _client.CreatePage(new NotionPageCreateRequest
                {
                    Parent = NotionParent.Page(_parentPageIdByLocalId[upsert.LocalId]),
                    Properties = new Dictionary<string, NotionPropertyValue>
                    {
                        ["title"] = new() { Type = "title", Title = NotionRichText.Of(TitleFor(upsert.LocalId)) },
                    },
                    Children = blocks.Count > 0 ? blocks : null,
                });
                // Record the id the instant the page exists so a later throw in this batch does not lose it.
                assigned[upsert.LocalId] = page.Id;
            }
            else
            {
                ReplaceBody(upsert.ExternalId, blocks);
            }
        }

        foreach (var externalId in changes.Deletes)
            _client.UpdatePage(externalId, new NotionPageUpdateRequest { Archived = true });
    }

    private string TitleFor(string localId) =>
        _titleByLocalId.TryGetValue(localId, out var title) && title.Length > 0 ? title : localId;

    /// <summary>Replace a page's BODY, appending the new blocks before deleting the old (their ids snapshotted
    /// first) so a failed append never empties the page (mirrors <see cref="NotionSyncAdapter"/>). A page's
    /// nested sub-pages surface here as <c>child_page</c> blocks — those are repo-owned structure, not body, so
    /// they are excluded from the delete set: replacing a folder's index body never archives its child pages.</summary>
    private void ReplaceBody(string pageId, List<NotionBlock> blocks)
    {
        var existingIds = _client.GetBlockChildren(pageId)
            .Where(b => b.Type != "child_page")
            .Select(b => b.Id)
            .Where(id => id != null)
            .ToList();

        if (blocks.Count > 0)
            _client.AppendBlockChildren(pageId, new NotionAppendChildrenRequest { Children = blocks });

        foreach (var id in existingIds)
            _client.DeleteBlock(id!);
    }

    public string NormalizeBody(string body) =>
        NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(body));

    /// <summary>Drop every field: a plain page carries no properties, so frontmatter is adapter-invisible. The
    /// engine preserves it on the repo (via the invisible-field overlay) and never reads the field-less page as
    /// a frontmatter deletion — only the body round-trips through Notion.</summary>
    public SyncDoc NormalizeFields(SyncDoc doc) => new()
    {
        LocalId = doc.LocalId,
        ExternalId = doc.ExternalId,
        Fields = [],
        Body = doc.Body,
        SourcePath = doc.SourcePath,
    };
}
