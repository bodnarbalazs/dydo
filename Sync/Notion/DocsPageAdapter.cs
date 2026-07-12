namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// The page-tree <see cref="ISyncAdapter"/> (DR 033) — the docs mirror's analogue of
/// <see cref="NotionSyncAdapter"/>. Where the spine adapter maps the rows of ONE data source, this maps the
/// managed pages of a nested-page TREE: it enumerates the tree via <see cref="INotionClient.GetChildPages"/>,
/// reads each managed page's body via <see cref="INotionClient.GetPageMarkdown"/> (Notion maps blocks→markdown
/// server-side, DR 035), creates a doc's page under its repo-owned parent via
/// <see cref="INotionClient.CreatePage"/> + <see cref="NotionParent.Page"/>, replaces a body via
/// <see cref="INotionClient.UpdatePageMarkdown"/> (Notion maps markdown→blocks server-side), and archives a
/// removed doc's page via <c>UpdatePage { Archived = true }</c>. Bodies round-trip through Notion's native
/// markdown API instead of the lossy <c>NotionBlockConverter</c> — retiring the phantom-conflict corruption
/// class (issue 0235) at its root — while the 3-way merge and base-snapshot store are reused as-is. The
/// converter stays for the spine (issue 0236) but the docs mirror no longer touches it for bodies.
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
    private readonly TextWriter? _log;
    private readonly IReadOnlyDictionary<string, string>? _lastSyncedBodyByPageId;
    /// <summary>Structure is repo-owned (DR 033 §2): the engine must never delete a repo doc or archive its page
    /// because the page was merely missing from an eventual-consistency-lagged external read.</summary>
    public bool RepoOwnedStructure => true;

    /// <param name="rootPageId">The "Docs" root page all top-level docs nest under; read for its own body too.</param>
    /// <param name="parentPageIdByLocalId">Where to create each doc's page — its repo-owned parent's Notion page id.</param>
    /// <param name="titleByLocalId">The Notion page title to set when a doc's page is first created.</param>
    /// <param name="managedPageIds">Page ids the sync already tracks, so the tree walk ignores unmanaged pages.</param>
    /// <param name="log">Optional sink for the one tolerated archive skip — a page already trashed under an
    /// archived ancestor (issue 0221) — and the truncated-read warning (DR 035 caveat). Every other archive
    /// failure propagates rather than being logged and swallowed.</param>
    /// <param name="lastSyncedBodyByPageId">The base snapshot's last-synced body per page id, consulted only when
    /// a body reads back TRUNCATED (past Notion's ~20k-block export ceiling, DR 035 caveat): a truncated read is
    /// shorter than the real body, so the adapter substitutes the last-synced body here to keep the reconcile a
    /// no-op rather than merging a cut-short body onto the canonical file. Null (tests, dry-run) falls back to the
    /// truncated body, which no dydo corpus doc ever triggers.</param>
    public DocsPageAdapter(
        INotionClient client,
        string rootPageId,
        IReadOnlyDictionary<string, string> parentPageIdByLocalId,
        IReadOnlyDictionary<string, string> titleByLocalId,
        IReadOnlySet<string> managedPageIds,
        TextWriter? log = null,
        IReadOnlyDictionary<string, string>? lastSyncedBodyByPageId = null)
    {
        _client = client;
        _rootPageId = rootPageId;
        _parentPageIdByLocalId = parentPageIdByLocalId;
        _titleByLocalId = titleByLocalId;
        _managedPageIds = managedPageIds;
        _log = log;
        _lastSyncedBodyByPageId = lastSyncedBodyByPageId;
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

    // The body is read through Notion's native markdown API (DR 035). It is cleaned structure-preservingly — not
    // fully normalized — because this body may be written back to a canonical repo file on a Notion-side edit:
    // CleanForPersist strips the expiring-URL signatures (never persist an expiring URL) and normalizes line
    // endings while leaving tables and every other construct intact. Dialect/whitespace drift is collapsed only
    // for the merge's change detection, via NormalizeBody — never in the content persisted to disk.
    private SyncRecord ReadPage(string pageId)
    {
        var read = _client.GetPageMarkdown(pageId);
        // Notion caps markdown export at ~20k blocks and CUTS the body short past it (DR 035 caveat). A truncated
        // read is shorter than the real body, so treating it as external state would read as a Notion-side deletion
        // and merge a cut-short body back onto the canonical file, corrupting it. Substitute the last-synced body so
        // the reconcile sees no external change and skips the merge — the canonical file is left intact — and warn.
        if (read.Truncated)
        {
            _log?.WriteLine(
                $"notion docs sync: page {pageId} exceeded Notion's ~20k-block export ceiling and read back truncated — "
                + "reusing the last-synced body and skipping its merge this tick (canonical file untouched)");
            return new SyncRecord
            {
                ExternalId = pageId,
                Fields = [],
                Body = _lastSyncedBodyByPageId?.GetValueOrDefault(pageId) ?? DocsMarkdownNormalizer.CleanForPersist(read.Markdown),
            };
        }
        return new SyncRecord
        {
            ExternalId = pageId,
            Fields = [],
            Body = DocsMarkdownNormalizer.CleanForPersist(read.Markdown),
        };
    }

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

    public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned) =>
        Apply(changes, assigned, new HashSet<string>(), new HashSet<string>());

    public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned, ICollection<string> deleted)
        => Apply(changes, assigned, deleted, new HashSet<string>());

    public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned, ICollection<string> deleted,
        ICollection<string> emptyBodied)
    {
        foreach (var upsert in changes.Upserts)
        {
            if (upsert.ExternalId == null)
            {
                // Create the child page AND its body in ONE atomic call (DR 035 §1 create-with-body). A two-step
                // create-then-UpdatePageMarkdown left a window: a throw after the page existed recorded a full-body
                // base against an empty Notion page, so the next tick read the empty page as an external clear and
                // wiped the canonical repo file (issue 0235). Carrying the body in the create closes the window —
                // the page is created with its body or, on a throw, not at all, so no base is ever advanced against
                // a half-written page. An empty body sends no markdown field, matching a bodyless folder page.
                var page = _client.CreatePage(new NotionPageCreateRequest
                {
                    Parent = NotionParent.Page(_parentPageIdByLocalId[upsert.LocalId]),
                    Properties = new Dictionary<string, NotionPropertyValue>
                    {
                        ["title"] = new() { Type = "title", Title = NotionRichText.Of(TitleFor(upsert.LocalId)) },
                    },
                    Markdown = upsert.Body.Length > 0 ? upsert.Body : null,
                });
                assigned[upsert.LocalId] = page.Id;
                if (upsert.Body.Length == 0)
                    continue;

                // The page exists even if its immediate read-back or fallback PATCH fails. Record an empty base
                // before either operation so a partial tick keeps the id without claiming its body landed.
                emptyBodied.Add(upsert.LocalId);
                if (_client.GetPageMarkdown(page.Id).Markdown.Length == 0)
                {
                    _log?.WriteLine($"notion docs sync: page {page.Id} ({upsert.LocalId}) ignored the markdown field on create; recording an empty base and writing the body via a child-safe PATCH");
                    _client.UpdatePageMarkdown(page.Id, upsert.Body, allowDeletingContent: false);
                }
                emptyBodied.Remove(upsert.LocalId);
            }
            else
            {
                // Notion maps the markdown to blocks and replaces the body server-side. But replace_content with
                // allow_deleting_content:true can TRASH a page's child pages (makenotion/notion-mcp-server#171): a
                // FOLDER page carries the nested docs as child pages, so a destructive replace would structurally
                // wipe them. Issue the body-replace CHILD-SAFE — allow_deleting_content:false — for any page that
                // still has child pages; only a leaf page (no children) takes the destructive full overwrite. On a
                // fresh sync the body is written at page-create (DocsTreeSync), before any children exist, so this
                // update path is only ever a folder-index EDIT on a page whose children must be preserved.
                var hasChildPages = _client.GetChildPages(upsert.ExternalId).Count > 0;
                _client.UpdatePageMarkdown(upsert.ExternalId, upsert.Body, allowDeletingContent: !hasChildPages);
            }
        }

        // Archive DESCENDANTS BEFORE ANCESTORS. SyncRunner emits deletes ascending by local id, and a folder's
        // local id is a path-prefix of its descendants' — so ascending puts every ancestor before its children.
        // Notion rejects editing (incl. archiving) a page whose ancestor is already archived (400 "archived
        // ancestor"), so archiving ancestor-first wedges the tick. Reversing the ascending order archives
        // children first, when their ancestors are still live.
        //
        // The catch tolerates ONLY the archived-ancestor 400 (issue 0221): a page already trashed under an
        // archived ancestor is effectively gone, so skipping it is safe — but it is deliberately NOT recorded as
        // landed, so its base entry survives and next tick's both-sides-gone Retire prunes it cleanly (never
        // dropping tracking for a live page). EVERY other NotionApiException — 429 rate-limit, 401 auth, 5xx,
        // permissions — PROPAGATES, so NotionSyncService surfaces a loud ToolError and the base is left
        // un-advanced for retry instead of silently orphaning a live Notion page behind a success exit.
        foreach (var externalId in Enumerable.Reverse(changes.Deletes))
        {
            try
            {
                _client.UpdatePage(externalId, new NotionPageUpdateRequest { Archived = true });
                deleted.Add(externalId);
            }
            catch (NotionApiException ex) when (IsArchivedAncestor(ex))
            {
                _log?.WriteLine($"notion docs sync: page {externalId} already archived under an archived ancestor, skipping — {ex.Message}");
            }
        }
    }

    /// <summary>The one archive failure this loop tolerates (issue 0221): a 400 rejecting the archive because the
    /// page's ANCESTOR is already archived — Notion cascades archive into the trash, so the descendant is already
    /// gone and re-archiving it is a redundant no-op. Detected by the 400 status plus the "archived"/"ancestor"
    /// signature in the error body. Every other <see cref="NotionApiException"/> (rate-limit, auth, permission,
    /// 5xx) is a real failure that must propagate so the base is not advanced for a page still live in Notion.</summary>
    private static bool IsArchivedAncestor(NotionApiException ex) =>
        ex.StatusCode == 400
        && ex.Body.Contains("archived", StringComparison.OrdinalIgnoreCase)
        && ex.Body.Contains("ancestor", StringComparison.OrdinalIgnoreCase);

    private string TitleFor(string localId) =>
        _titleByLocalId.TryGetValue(localId, out var title) && title.Length > 0 ? title : localId;

    /// <summary>Compare bodies MODULO Notion's markdown dialect and whitespace drift (DR 035 §3): local docs are
    /// CommonMark, Notion echoes back Notion-flavored markdown, so a byte compare would read that well-defined
    /// dialect difference as an edit and churn a phantom conflict every tick. Canonicalizing both sides through
    /// <see cref="DocsMarkdownNormalizer"/> collapses the difference, so an untouched doc stays a no-op.</summary>
    public string NormalizeBody(string body) => DocsMarkdownNormalizer.Normalize(body);

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
