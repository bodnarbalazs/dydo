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
    public List<string> AppendedTo { get; } = [];
    public List<string> DeletedBlocks { get; } = [];

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

    public NotionDatabase CreateDatabase(NotionDatabaseCreateRequest request)
    {
        var n = _nextDb++;
        var db = new NotionDatabase
        {
            Id = $"db-{n}",
            DataSources = [new NotionDataSourceRef { Id = $"ds-{n}", Name = NotionRichText.Flatten(request.Title) }],
        };
        Databases[db.Id] = db;
        CreatedDatabases.Add(request);
        return db;
    }

    public IReadOnlyList<NotionPage> QueryDataSource(string dataSourceId) =>
        _pages.Values.Where(p => _pageDataSource[p.Id] == dataSourceId).ToList();

    public NotionPage CreatePage(NotionPageCreateRequest request)
    {
        var id = $"page-{_nextPage++}";
        var page = new NotionPage { Id = id, Properties = request.Properties };
        _pages[id] = page;
        _pageDataSource[id] = request.Parent.DataSourceId;
        _blocks[id] = request.Children ?? [];
        return page;
    }

    public NotionPage UpdatePage(string pageId, NotionPageUpdateRequest request)
    {
        var page = _pages[pageId];
        if (request.Properties != null)
            foreach (var (k, v) in request.Properties)
                page.Properties[k] = v;
        if (request.Archived == true)
            page.Archived = true;
        return page;
    }

    public IReadOnlyList<NotionBlock> GetBlockChildren(string blockId) =>
        _blocks.TryGetValue(blockId, out var b) ? b : [];

    public void AppendBlockChildren(string blockId, NotionAppendChildrenRequest request)
    {
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
