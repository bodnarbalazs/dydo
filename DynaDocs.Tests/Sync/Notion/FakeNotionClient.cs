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
    // Nested-page tree (DR 033): a page's parent page id and its title, so GetChildPages can walk the tree
    // and the docs mirror can be exercised without HTTP.
    private readonly Dictionary<string, string> _pageParent = new();
    private readonly Dictionary<string, string> _pageTitle = new();
    private int _nextPage = 1;
    private int _nextBlock = 1;
    private int _nextDb = 1;

    public List<string> DiscoverableDataSources { get; } = [];
    public Dictionary<string, NotionDatabase> Databases { get; } = new();
    public List<NotionDatabaseCreateRequest> CreatedDatabases { get; } = [];
    public List<(string DataSourceId, NotionDataSourceUpdateRequest Request)> DataSourceUpdates { get; } = [];
    public List<NotionViewCreateRequest> CreatedViews { get; } = [];

    /// <summary>View ids per database — seeded with an auto-created "default" on CreateDatabase (as real Notion
    /// does), appended by CreateView, and pruned by DeleteView — so the provisioner's default-view removal is testable.</summary>
    private readonly Dictionary<string, List<string>> _viewsByDb = new();
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
    public List<string> DeletedBlocks { get; } = [];

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
        var n = _nextDb++;
        var dataSourceId = $"ds-{n}";
        var db = new NotionDatabase
        {
            Id = $"db-{n}",
            DataSources = [new NotionDataSourceRef { Id = dataSourceId, Name = NotionRichText.Flatten(request.Title) }],
        };
        Databases[db.Id] = db;
        _viewsByDb[db.Id] = [$"default-{n}"]; // Notion auto-creates a default view on database creation
        CreatedDatabases.Add(request);
        _dataSources[dataSourceId] = new NotionDataSource
        {
            Id = dataSourceId,
            Properties = new Dictionary<string, NotionPropertySchema>(request.InitialDataSource.Properties),
        };
        // Real Notion assigns each property an id, returned on read and referenced by view configs; mirror that
        // by using the property name as its id so RetrieveDataSource yields a usable name→id map in tests.
        foreach (var (name, schema) in _dataSources[dataSourceId].Properties)
            schema.Id ??= name;
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

    public void CreateView(NotionViewCreateRequest request)
    {
        CreatedViews.Add(request);
        if (!_viewsByDb.TryGetValue(request.DatabaseId, out var list))
            _viewsByDb[request.DatabaseId] = list = [];
        list.Add($"view-{_nextView++}");
    }

    public IReadOnlyList<string> ListViewIds(string databaseId) =>
        _viewsByDb.TryGetValue(databaseId, out var list) ? list.ToList() : [];

    public void DeleteView(string viewId)
    {
        DeletedViews.Add(viewId);
        foreach (var list in _viewsByDb.Values)
            list.Remove(viewId);
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

    public IReadOnlyList<string> SearchDataSources() => DiscoverableDataSources;
}
