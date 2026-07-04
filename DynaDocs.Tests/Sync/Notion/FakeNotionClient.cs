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
    private int _nextPage = 1;
    private int _nextBlock = 1;
    private int _nextDb = 1;

    public List<string> DiscoverableDataSources { get; } = [];
    public Dictionary<string, NotionDatabase> Databases { get; } = new();
    public List<NotionDatabaseCreateRequest> CreatedDatabases { get; } = [];
    public List<(string DataSourceId, NotionDataSourceUpdateRequest Request)> DataSourceUpdates { get; } = [];

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

    /// <summary>When true, <see cref="AppendBlockChildren"/> throws — drives the non-atomic ReplaceBody test.</summary>
    public bool FailAppend { get; set; }

    /// <summary>When true, <see cref="UpdatePage"/> throws — drives the Apply-failure guards: a non-create
    /// push (property update) and a delete (archive) both go through UpdatePage, so this pins that the base
    /// neither advances a failed push nor drops a failed delete's entry.</summary>
    public bool FailUpdate { get; set; }

    private int _createCount;

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

    public NotionDatabase RetrieveDatabase(string databaseId) =>
        Databases.TryGetValue(databaseId, out var db) ? db : new NotionDatabase { Id = databaseId };

    public NotionDataSource RetrieveDataSource(string dataSourceId) => DataSourceSchema(dataSourceId);

    public NotionDatabase CreateDatabase(NotionDatabaseCreateRequest request)
    {
        var n = _nextDb++;
        var dataSourceId = $"ds-{n}";
        var db = new NotionDatabase
        {
            Id = $"db-{n}",
            DataSources = [new NotionDataSourceRef { Id = dataSourceId, Name = NotionRichText.Flatten(request.Title) }],
        };
        Databases[db.Id] = db;
        CreatedDatabases.Add(request);
        _dataSources[dataSourceId] = new NotionDataSource
        {
            Id = dataSourceId,
            Properties = new Dictionary<string, NotionPropertySchema>(request.InitialDataSource.Properties),
        };
        return db;
    }

    public void UpdateDataSource(string dataSourceId, NotionDataSourceUpdateRequest request)
    {
        DataSourceUpdates.Add((dataSourceId, request));
        var schema = DataSourceSchema(dataSourceId).Properties;
        foreach (var (name, body) in request.Properties)
        {
            if (body == null)
                schema.Remove(name); // a null body prunes the property
            else
                schema[name] = body;
        }
    }

    public IReadOnlyList<NotionPage> QueryDataSource(string dataSourceId) =>
        _pages.Values.Where(p => _pageDataSource[p.Id] == dataSourceId).ToList();

    public NotionPage CreatePage(NotionPageCreateRequest request)
    {
        if (FailCreateAfter is { } limit && _createCount >= limit)
            throw new NotionApiException(500, "simulated create failure");
        _createCount++;
        var id = $"page-{_nextPage++}";
        var page = new NotionPage { Id = id, Properties = request.Properties };
        _pages[id] = page;
        _pageDataSource[id] = request.Parent.DataSourceId;
        _blocks[id] = request.Children ?? [];
        return page;
    }

    public NotionPage UpdatePage(string pageId, NotionPageUpdateRequest request)
    {
        if (FailUpdate)
            throw new NotionApiException(500, "simulated update failure");
        var page = _pages[pageId];
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
