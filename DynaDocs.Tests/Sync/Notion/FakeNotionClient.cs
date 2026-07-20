namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>In-memory <see cref="INotionClient"/> for exercising the adapter and provisioner without
/// HTTP: holds pages per data source, their block children, and created databases; records calls for
/// assertions. Created databases are retrievable, so provisioner idempotency can be driven end-to-end.</summary>
public sealed class FakeNotionClient : INotionClient
{
    private readonly Dictionary<string, NotionPage> _pages = new();
    private readonly Dictionary<string, string> _pageDataSource = new();
    private readonly Dictionary<string, List<NotionBlock>> _blocks = new();
    // Native markdown bodies (DR 035): the docs mirror reads/writes page bodies as markdown strings, mapped
    // block↔markdown server-side by real Notion. The fake echoes the stored markdown verbatim — a faithful
    // model of a lossless round-trip; the Notion-flavored dialect drift only a live board exhibits is covered
    // by the DocsMarkdownNormalizer unit tests and manual live smoke, not simulated here.
    private readonly Dictionary<string, string> _pageMarkdown = new();
    // Nested-page tree (DR 033): a page's parent page id and its title, so GetChildPages can walk the tree
    // and the docs mirror can be exercised without HTTP.
    private readonly Dictionary<string, string> _pageParent = new();
    private readonly Dictionary<string, string> _pageTitle = new();
    private int _nextPage = 1;
    private int _nextBlock = 1;
    private int _nextDb = 1;

    public List<NotionSearchResult> DiscoverableDataSources { get; } = [];
    public Dictionary<string, NotionDatabase> Databases { get; } = new();
    public List<NotionDatabaseCreateRequest> CreatedDatabases { get; } = [];

    /// <summary>Database ids passed to <see cref="ArchiveDatabase"/>, in call order — lets a reset test assert
    /// the tracked databases were trashed before the reprovision minted fresh ones.</summary>
    public List<string> ArchivedDatabases { get; } = [];
    public List<(string DataSourceId, NotionDataSourceUpdateRequest Request)> DataSourceUpdates { get; } = [];
    public List<NotionViewCreateRequest> CreatedViews { get; } = [];

    /// <summary>Views per database — seeded with an auto-created "default" on CreateDatabase (as real Notion
    /// does), appended by CreateView, and pruned by DeleteView — so the provisioner's default-view removal and
    /// the CreateView match-by-name recovery are testable.</summary>
    private readonly Dictionary<string, List<NotionViewRef>> _viewsByDb = new();
    private int _nextView = 1;
    public List<string> DeletedViews { get; } = [];

    /// <summary>Live property schema per data source id — seeded from CreateDatabase, merged by
    /// UpdateDataSource (a null property body deletes it), and read back by RetrieveDataSource. Lets
    /// schema-drift tests seed rogue properties/options and assert prune deletions.</summary>
    private readonly Dictionary<string, NotionDataSource> _dataSources = new();

    /// <summary>The live schema for a data source, created empty on first access so a test can seed it.</summary>
    public NotionDataSource DataSourceSchema(string dataSourceId)
    {
        if (!_dataSources.TryGetValue(dataSourceId, out var ds))
            _dataSources[dataSourceId] = ds = new NotionDataSource { Id = dataSourceId };
        return ds;
    }
    public List<string> AppendedTo { get; } = [];

    public List<int> CreateChildCounts { get; } = [];
    public List<string> DeletedBlocks { get; } = [];

    /// <summary>Page ids that received a body write via <see cref="UpdatePageMarkdown"/>, in call order — one
    /// entry per write, so a docs-mirror test can assert a body landed AND that a no-op tick issues no repeated
    /// markdown write (the DR 035 replacement for the append-then-delete block dance).</summary>
    public List<string> MarkdownUpdates { get; } = [];

    /// <summary>Every <see cref="UpdatePageMarkdown"/> call with the allow_deleting_content flag it carried — lets a
    /// child-safety test assert a folder-page body update was issued NON-destructively (<c>false</c>) while a
    /// leaf-page update took the destructive full overwrite (<c>true</c>), per DR 035 §3.</summary>
    public List<(string PageId, bool AllowDeletingContent)> MarkdownUpdateCalls { get; } = [];

    /// <summary>Page ids whose <see cref="GetPageMarkdown"/> reads back TRUNCATED — models a body past Notion's
    /// ~20k-block export ceiling (DR 035 caveat), driving the truncation-guard test.</summary>
    public HashSet<string> TruncatedReadFor { get; } = [];

    /// <summary>Page ids that received an engine-computed <c>last-activity</c> property write via
    /// <see cref="UpdatePage"/> — one entry per write, so a test can assert the value lands AND that a
    /// no-op tick issues no repeated write (DR 030 §3, finding 1).</summary>
    public List<string> LastActivityWrites { get; } = [];

    /// <summary>When set, <see cref="CreatePage"/> throws once this many creates have succeeded — drives
    /// the partial-Apply-failure test (a create fails mid-batch).</summary>
    public int? FailCreateAfter { get; set; }

    /// <summary>When set, <see cref="CreateDatabase"/> throws once this many database creates have succeeded —
    /// drives the mid-provision-failure test (provision state must persist the databases already created).</summary>
    public int? FailCreateDatabaseAfter { get; set; }

    /// <summary>One-shot: the next <see cref="CreateDatabase"/> creates the database AND registers it discoverable
    /// (as real Notion would) but then throws an AMBIGUOUS 500 — modelling a create that succeeded server-side
    /// before its response was lost. Drives the provisioner's re-search-and-adopt recovery (ns-5).</summary>
    public bool CreateDatabaseSucceedsThenAmbiguous5xx { get; set; }

    /// <summary>One-shot: the next <see cref="CreateDatabase"/> throws an AMBIGUOUS 500 BEFORE creating anything —
    /// modelling a create that truly failed. Drives the recovery's re-create-fresh branch (search finds nothing).</summary>
    public bool CreateDatabaseFailsAmbiguously { get; set; }

    /// <summary>One-shot: the next <see cref="CreatePage"/> stores the page but then throws an AMBIGUOUS 500 —
    /// modelling a page create that landed server-side before its response was lost. Drives the adapter's
    /// re-query-and-adopt recovery (ns-5).</summary>
    public bool CreatePageSucceedsThenAmbiguous5xx { get; set; }

    /// <summary>One-shot: the next <see cref="CreatePage"/> throws an AMBIGUOUS 500 BEFORE storing anything —
    /// modelling a page create that truly failed. Drives the adapter's re-create-fresh branch.</summary>
    public bool CreatePageFailsAmbiguously { get; set; }

    /// <summary>One-shot: the next <see cref="CreateView"/> records the view (so a subsequent <see cref="ListViews"/>
    /// finds it by name) but then throws an AMBIGUOUS 500 — modelling a view create that landed server-side before
    /// its response was lost. Drives the provisioner's match-by-name recovery (ns-5).</summary>
    public bool CreateViewSucceedsThenAmbiguous5xx { get; set; }

    /// <summary>One-shot: the next <see cref="CreateView"/> throws an AMBIGUOUS 500 BEFORE recording anything —
    /// modelling a view create that truly failed. Drives the recovery's re-create branch.</summary>
    public bool CreateViewFailsAmbiguously { get; set; }

    /// <summary>When set, <see cref="UpdateDataSource"/> throws once this many updates have succeeded — drives
    /// the mid-post-pass-failure tests: a self-relation PATCH crash (finding 3) and a rollup/formula post-pass
    /// crash proving per-type MarkPostPassDone incremental persistence (finding 10).</summary>
    public int? FailUpdateDataSourceAfter { get; set; }

    /// <summary>When set, <see cref="RetrieveDatabase"/> throws this exception — drives the provisioner
    /// validity-probe tests: a transient 429/5xx must abort the tick (never re-provision), while a
    /// definitive 404/object_not_found re-provisions as a genuinely deleted database (finding 1).</summary>
    public NotionApiException? FailRetrieveDatabase { get; set; }

    /// <summary>Database ids that <see cref="RetrieveDatabase"/> 404s (object_not_found) — a PER-DATABASE
    /// definitive not-found, so a test can re-provision ONE type (a deleted/unshared parent) while its
    /// children reuse their still-valid databases: the cross-type re-provision seam of finding 1.</summary>
    public HashSet<string> NotFoundDatabaseIds { get; } = [];

    /// <summary>When set, <see cref="RetrieveDataSource"/> throws this exception — drives the durable-reset test
    /// (review R2-1): a re-provision mints a fresh EMPTY database, then CheckDrift (which calls RetrieveDataSource
    /// per type, between Provision and Reconcile) throws, aborting the tick after the mint but before the
    /// end-of-tick base Save. Only the schema-drift path calls RetrieveDataSource, so the reconcile path is
    /// unaffected on a healthy tick.</summary>
    public NotionApiException? FailRetrieveDataSource { get; set; }

    /// <summary>When true, <see cref="AppendBlockChildren"/> throws — drives the non-atomic ReplaceBody test.</summary>
    public bool FailAppend { get; set; }

    /// <summary>When true, a create-with-body DROPS the carried markdown field instead of storing it — modelling
    /// live Notion SILENTLY IGNORING the create's <c>markdown</c> field (DR 035 finding). The page is created
    /// empty, so a read-back returns "". Drives the read-back guards that never let a full-body base be recorded
    /// against an empty page (the issue 0235 wipe): both the adapter and fresh-sync structure phase record an EMPTY
    /// base and self-heal via a child-safe body-phase PATCH (graceful degradation).</summary>
    public bool SilentlyIgnoreCreateMarkdown { get; set; }

    /// <summary>When true, the first markdown read immediately following a body-carrying create throws — modelling
    /// a transient read-back failure after Notion has already created the page.</summary>
    public bool ThrowFirstMarkdownReadAfterBodyCreate { get; set; }

    private bool _throwMarkdownReadAfterBodyCreate;

    /// <summary>Page ids whose <see cref="GetPageMarkdown"/> throws a 404 — models a page archived/trashed in
    /// Notion whose body read now fails (DR 035 finding), driving the promotion-read guard test.</summary>
    public HashSet<string> FailMarkdownReadFor { get; } = [];

    /// <summary>When true, <see cref="UpdatePageMarkdown"/> throws — drives the create-with-body atomicity test
    /// (DR 035 §1, issue 0235): a resurrect create must persist its body in the CREATE call, never a separate
    /// UpdatePageMarkdown that could independently fail and leave a full-body base against an empty page. With
    /// this set, a create-with-body still lands its body (the create carries it), proving the write is atomic.</summary>
    public bool FailMarkdownUpdate { get; set; }

    /// <summary>When true, <see cref="QueryDataSource"/> echoes every schema RELATION property a page lacks as
    /// an EMPTY relation — exactly as real Notion does (it returns all schema properties, so an all-unresolvable
    /// relation reads back as an empty value, not an absent key). Off by default so existing tests are unaffected;
    /// the empty-relation convergence tests turn it on to reproduce the real echo (finding 6).</summary>
    public bool EchoEmptyRelations { get; set; }

    /// <summary>When true, <see cref="UpdatePage"/> throws — drives the Apply-failure guards: a non-create
    /// push (property update) and a delete (archive) both go through UpdatePage, so this pins that the base
    /// neither advances a failed push nor drops a failed delete's entry.</summary>
    public bool FailUpdate { get; set; }

    private int _createCount;

    /// <summary>Page ids the tree listing omits even though the page still exists and is not archived — simulates
    /// Notion's list eventual-consistency after a bulk create (a just-made page not yet visible to a child_page
    /// enumeration). Lets a test reproduce the repo-present-but-external-missing case the docs mirror must never
    /// treat as a deletion.</summary>
    public HashSet<string> HiddenFromListing { get; } = [];

    /// <summary>Whether a page is archived (in trash) — a test hook for asserting the docs mirror's archive
    /// ordering actually archived a page, since <see cref="GetChildPages"/> hides archived pages from the walk.</summary>
    public bool IsArchived(string pageId) => _pages.TryGetValue(pageId, out var p) && p.Archived;

    /// <summary>Replace a page's block children outright — simulates an external body edit in Notion.</summary>
    public void SetBlockChildren(string pageId, List<NotionBlock> blocks) => _blocks[pageId] = blocks;

    /// <summary>Replace a page's native markdown body outright — simulates an external body edit in Notion for
    /// the docs mirror's markdown-API path (DR 035), the counterpart to <see cref="SetBlockChildren"/>.</summary>
    public void SetPageMarkdown(string pageId, string markdown) => _pageMarkdown[pageId] = markdown;

    public NotionPage SeedPage(string id, Dictionary<string, NotionPropertyValue> props,
        List<NotionBlock>? blocks = null, string dataSourceId = "ds1")
    {
        var page = new NotionPage { Id = id, Properties = props };
        _pages[id] = page;
        _pageDataSource[id] = dataSourceId;
        _blocks[id] = blocks ?? [];
        return page;
    }

    public NotionDatabase RetrieveDatabase(string databaseId)
    {
        if (NotFoundDatabaseIds.Contains(databaseId))
            throw new NotionApiException(404, "{\"code\":\"object_not_found\"}");
        if (FailRetrieveDatabase is { } ex)
            throw ex;
        return Databases.TryGetValue(databaseId, out var db) ? db : new NotionDatabase { Id = databaseId };
    }

    public NotionDataSource RetrieveDataSource(string dataSourceId)
    {
        if (FailRetrieveDataSource is { } ex)
            throw ex;
        return DataSourceSchema(dataSourceId);
    }

    public NotionDatabase CreateDatabase(NotionDatabaseCreateRequest request)
    {
        if (FailCreateDatabaseAfter is { } limit && CreatedDatabases.Count >= limit)
            throw new NotionApiException(429, "simulated database create failure");
        if (CreateDatabaseFailsAmbiguously)
        {
            CreateDatabaseFailsAmbiguously = false;
            throw new NotionApiException(500, "simulated ambiguous database create failure (nothing persisted)");
        }
        var n = _nextDb++;
        var dataSourceId = $"ds-{n}";
        var title = NotionRichText.Flatten(request.Title);
        var db = new NotionDatabase
        {
            Id = $"db-{n}",
            Parent = new NotionParent { Type = "page_id", PageId = request.Parent.PageId },
            DataSources = [new NotionDataSourceRef { Id = dataSourceId, Name = title }],
        };
        Databases[db.Id] = db;
        _viewsByDb[db.Id] = [new NotionViewRef { Id = $"default-{n}", Name = "Default view" }]; // Notion auto-creates one
        CreatedDatabases.Add(request);
        // Real Notion makes every created data source searchable — the source of truth the CreateDatabase
        // recovery re-queries when a create response is lost (ns-5).
        DiscoverableDataSources.Add(new NotionSearchResult
        {
            Id = dataSourceId,
            Object = "data_source",
            Name = title,
            Parent = new NotionParent { Type = "database_id", DatabaseId = db.Id },
        });
        _dataSources[dataSourceId] = new NotionDataSource
        {
            Id = dataSourceId,
            Properties = new Dictionary<string, NotionPropertySchema>(request.InitialDataSource.Properties),
        };
        // Real Notion assigns each property an id, returned on read and referenced by view configs; mirror that
        // by using the property name as its id so RetrieveDataSource yields a usable name→id map in tests.
        foreach (var (name, schema) in _dataSources[dataSourceId].Properties)
            schema.Id ??= name;
        if (CreateDatabaseSucceedsThenAmbiguous5xx)
        {
            CreateDatabaseSucceedsThenAmbiguous5xx = false;
            throw new NotionApiException(500, "simulated ambiguous database create failure (persisted server-side)");
        }
        return db;
    }

    public void UpdateDataSource(string dataSourceId, NotionDataSourceUpdateRequest request)
    {
        if (FailUpdateDataSourceAfter is { } limit && DataSourceUpdates.Count >= limit)
            throw new NotionApiException(429, "simulated data source update failure");
        DataSourceUpdates.Add((dataSourceId, request));
        var schema = DataSourceSchema(dataSourceId).Properties;
        foreach (var (name, body) in request.Properties)
        {
            if (body == null)
            {
                schema.Remove(name); // a null body prunes the property
            }
            else
            {
                body.Id ??= name; // mirror Notion assigning an id, so views can resolve it (see CreateDatabase)
                schema[name] = body;
            }
        }
    }

    /// <summary>When true, <see cref="ArchiveDatabase"/> throws — drives the reset Notion-API-error path.</summary>
    public bool FailArchiveDatabase { get; set; }

    // Records the archive; the database stays retrievable, mirroring real Notion (a trashed database returns
    // in_trash: true, it does not 404) — which is exactly why reset must clear provision state to force a fresh mint.
    public void ArchiveDatabase(string databaseId)
    {
        if (FailArchiveDatabase)
            throw new NotionApiException(500, "simulated archive failure");
        ArchivedDatabases.Add(databaseId);
    }

    public void CreateView(NotionViewCreateRequest request)
    {
        if (CreateViewFailsAmbiguously)
        {
            CreateViewFailsAmbiguously = false;
            throw new NotionApiException(500, "simulated ambiguous view create failure (nothing persisted)");
        }
        CreatedViews.Add(request);
        if (!_viewsByDb.TryGetValue(request.DatabaseId, out var list))
            _viewsByDb[request.DatabaseId] = list = [];
        list.Add(new NotionViewRef { Id = $"view-{_nextView++}", Name = request.Name });
        if (CreateViewSucceedsThenAmbiguous5xx)
        {
            CreateViewSucceedsThenAmbiguous5xx = false;
            throw new NotionApiException(500, "simulated ambiguous view create failure (persisted server-side)");
        }
    }

    public IReadOnlyList<NotionViewRef> ListViews(string databaseId) =>
        _viewsByDb.TryGetValue(databaseId, out var list) ? list.ToList() : [];

    public void DeleteView(string viewId)
    {
        DeletedViews.Add(viewId);
        foreach (var list in _viewsByDb.Values)
            list.RemoveAll(v => v.Id == viewId);
    }

    public IReadOnlyList<NotionPage> QueryDataSource(string dataSourceId)
    {
        var pages = _pages.Values.Where(p => _pageDataSource[p.Id] == dataSourceId).ToList();
        if (EchoEmptyRelations && _dataSources.TryGetValue(dataSourceId, out var ds))
            foreach (var page in pages)
                foreach (var (name, schema) in ds.Properties)
                    if (schema.Relation != null && !page.Properties.ContainsKey(name))
                        page.Properties[name] = new NotionPropertyValue { Type = "relation", Relation = [] };
        return pages;
    }

    public NotionPage CreatePage(NotionPageCreateRequest request)
    {
        CreateChildCounts.Add(request.Children?.Count ?? 0);
        if (request.Children?.Count > 100)
            throw new NotionApiException(400, "body failed validation: body.children.length should be <= 100");
        if (CreatePageFailsAmbiguously)
        {
            CreatePageFailsAmbiguously = false;
            throw new NotionApiException(500, "simulated ambiguous page create failure (nothing persisted)");
        }
        if (FailCreateAfter is { } limit && _createCount >= limit)
            throw new NotionApiException(500, "simulated create failure");
        _createCount++;
        var id = $"page-{_nextPage++}";
        var page = new NotionPage { Id = id, Properties = request.Properties };
        _pages[id] = page;
        _pageDataSource[id] = request.Parent.DataSourceId ?? "";
        if (request.Parent.PageId != null)
            _pageParent[id] = request.Parent.PageId;
        if (request.Properties.TryGetValue("title", out var title) && title.Title != null)
            _pageTitle[id] = NotionRichText.Flatten(title.Title);
        _blocks[id] = request.Children ?? [];
        // Create-with-body (DR 035 §1): a markdown body carried in the create is stored atomically with the page,
        // so the read-back echoes it verbatim — modelling the atomic create the docs mirror relies on. It is NOT
        // recorded in MarkdownUpdates (which tracks UpdatePageMarkdown calls only), so a test can assert an atomic
        // create issued zero separate body writes.
        if (request.Markdown != null)
        {
            if (!SilentlyIgnoreCreateMarkdown)
                _pageMarkdown[id] = request.Markdown;
            _throwMarkdownReadAfterBodyCreate = ThrowFirstMarkdownReadAfterBodyCreate;
        }
        if (CreatePageSucceedsThenAmbiguous5xx)
        {
            CreatePageSucceedsThenAmbiguous5xx = false;
            throw new NotionApiException(500, "simulated ambiguous page create failure (persisted server-side)");
        }
        return page;
    }

    public IReadOnlyList<NotionChildPage> GetChildPages(string parentPageId) =>
        _pages.Values
            .Where(p => !p.Archived && !HiddenFromListing.Contains(p.Id)
                && _pageParent.TryGetValue(p.Id, out var parent) && parent == parentPageId)
            .Select(p => new NotionChildPage { Id = p.Id, Title = _pageTitle.GetValueOrDefault(p.Id, "") })
            .ToList();

    public NotionPage UpdatePage(string pageId, NotionPageUpdateRequest request)
    {
        if (FailUpdate)
            throw new NotionApiException(500, "simulated update failure");
        var page = _pages[pageId];
        // Real Notion rejects editing OR archiving a page whose ANCESTOR is already archived with a 400
        // ("Can't edit page ... with an archived ancestor"). This is the live constraint the docs-mirror delete
        // ordering must respect: archiving a folder before its children makes each child's archive fail. The
        // fake now enforces it so a test can catch the ancestor-before-descendant bug the old fake missed.
        if (HasArchivedAncestor(pageId))
            throw new NotionApiException(400, $"Can't edit page on block '{pageId}': its ancestor is archived.");
        // Real Notion rejects a property write on an archived (in-trash) page with 400 — the page must be
        // restored first. Archiving/unarchiving itself carries no Properties, so those still pass.
        if (page.Archived && request.Properties != null)
            throw new NotionApiException(400, $"Can't update properties of page '{pageId}': it is archived (in trash).");
        if (request.Properties != null)
        {
            if (request.Properties.ContainsKey("last-activity"))
                LastActivityWrites.Add(pageId);
            foreach (var (k, v) in request.Properties)
                page.Properties[k] = v;
        }
        if (request.Archived == true)
            page.Archived = true;
        return page;
    }

    /// <summary>Whether any ancestor of <paramref name="pageId"/> (walking the child→parent chain) is archived —
    /// the condition real Notion rejects an edit/archive under.</summary>
    private bool HasArchivedAncestor(string pageId)
    {
        var current = pageId;
        while (_pageParent.TryGetValue(current, out var parent))
        {
            if (_pages.TryGetValue(parent, out var p) && p.Archived)
                return true;
            current = parent;
        }
        return false;
    }

    public IReadOnlyList<NotionBlock> GetBlockChildren(string blockId) =>
        _blocks.TryGetValue(blockId, out var b) ? b : [];

    /// <summary>The raw stored markdown body of a page, WITHOUT the child-page reference tags
    /// <see cref="GetPageMarkdown"/> appends to model Notion's export — the exact body a write persisted. Lets a
    /// docs-mirror test assert the persisted body independent of the read-modeling, which is orthogonal to it.</summary>
    public string StoredMarkdown(string pageId) => _pageMarkdown.GetValueOrDefault(pageId, "");

    // An unwritten page reads back as the empty string, never null — matching the real markdown API's echo of
    // an empty body and INotionClient's contract. A page in FailMarkdownReadFor 404s (archived/trashed); a page in
    // TruncatedReadFor echoes its stored body with truncated:true, modelling the ~20k-block export ceiling.
    //
    // Real Notion's GET /pages/{id}/markdown APPENDS the page's child pages as child-page reference tags — one per
    // line, after the prose body: `<page url="https://app.notion.com/p/{childId}">{title}</page>` (issue 0235, DR
    // 035 §3). A folder page with an empty body and children thus reads back as JUST those tag lines. The fake models
    // that here — using the SAME child enumeration and order as GetChildPages — so a test can catch the docs mirror
    // treating this structure as body. A leaf page (no children) reads back its stored body verbatim.
    public NotionMarkdownResponse GetPageMarkdown(string pageId)
    {
        if (_throwMarkdownReadAfterBodyCreate)
        {
            _throwMarkdownReadAfterBodyCreate = false;
            throw new NotionApiException(500, "simulated post-create markdown read failure");
        }
        if (FailMarkdownReadFor.Contains(pageId))
            throw new NotionApiException(404, $"{{\"code\":\"object_not_found\",\"message\":\"page {pageId} not found\"}}");
        var body = _pageMarkdown.GetValueOrDefault(pageId, "");
        var childTags = string.Join("\n", GetChildPages(pageId)
            .Select(c => $"<page url=\"https://app.notion.com/p/{c.Id}\">{c.Title}</page>"));
        var markdown = childTags.Length == 0 ? body
            : body.Length == 0 ? childTags
            : body + "\n\n" + childTags;
        return new NotionMarkdownResponse
        {
            Object = "page_markdown",
            Markdown = markdown,
            Truncated = TruncatedReadFor.Contains(pageId),
        };
    }

    public void UpdatePageMarkdown(string pageId, string markdown, bool allowDeletingContent)
    {
        if (FailMarkdownUpdate)
            throw new NotionApiException(500, "simulated markdown update failure");
        MarkdownUpdates.Add(pageId);
        MarkdownUpdateCalls.Add((pageId, allowDeletingContent));
        _pageMarkdown[pageId] = markdown;
        // Model the Notion child-page-trash bug (makenotion/notion-mcp-server#171, DR 035 §3): a destructive
        // replace_content (allow_deleting_content:true) TRASHES the page's child pages. A child-safe update
        // (false) replaces the body and leaves the nested child pages intact — the behaviour the adapter relies on.
        if (allowDeletingContent)
            foreach (var child in _pages.Values.Where(p => _pageParent.GetValueOrDefault(p.Id) == pageId).ToList())
                child.Archived = true;
    }

    public void AppendBlockChildren(string blockId, NotionAppendChildrenRequest request)
    {
        if (FailAppend)
            throw new NotionApiException(500, "simulated append failure");
        AppendedTo.Add(blockId);
        _blocks.TryAdd(blockId, []);
        foreach (var child in request.Children)
        {
            child.Id ??= $"block-{_nextBlock++}";
            _blocks[blockId].Add(child);
        }
    }

    public void DeleteBlock(string blockId)
    {
        DeletedBlocks.Add(blockId);
        foreach (var list in _blocks.Values)
            list.RemoveAll(b => b.Id == blockId);
    }

    public IReadOnlyList<NotionSearchResult> SearchDataSources() => DiscoverableDataSources;
}
