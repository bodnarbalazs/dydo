namespace DynaDocs.Sync.Notion;

using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// The thin Notion REST surface the sync adapter needs (Decision 025 §6): resolve a database to its
/// data sources, query/create/update pages, read/append block children, and discover data sources.
/// Pagination is handled inside the implementation — callers get the full result. All Notion-specific
/// knowledge lives behind this; nothing else in the codebase talks to Notion.
/// </summary>
public interface INotionClient
{
    NotionDatabase RetrieveDatabase(string databaseId);

    /// <summary>Retrieve a data source's live property schema (GET /v1/data_sources/{id}) — the input to
    /// the schema-drift check (DR 029 §6), which compares it against the project's canonical model.</summary>
    NotionDataSource RetrieveDataSource(string dataSourceId);

    /// <summary>Create a database under a parent page; the response carries its data source(s).</summary>
    NotionDatabase CreateDatabase(NotionDatabaseCreateRequest request);

    /// <summary>Add or update properties on an existing data source's schema (PATCH /v1/data_sources/{id}).
    /// Used for the self-relation second pass, where the target data source id is only known post-create.</summary>
    void UpdateDataSource(string dataSourceId, NotionDataSourceUpdateRequest request);

    /// <summary>Query a data source, following pagination, returning every page.</summary>
    IReadOnlyList<NotionPage> QueryDataSource(string dataSourceId);

    NotionPage CreatePage(NotionPageCreateRequest request);

    NotionPage UpdatePage(string pageId, NotionPageUpdateRequest request);

    /// <summary>Read a page's block children, following pagination, returning every block.</summary>
    IReadOnlyList<NotionBlock> GetBlockChildren(string blockId);

    void AppendBlockChildren(string blockId, NotionAppendChildrenRequest request);

    /// <summary>Archive (soft-delete) a single block — used to clear a page body before re-appending.</summary>
    void DeleteBlock(string blockId);

    /// <summary>Discover accessible data-source ids via POST /v1/search.</summary>
    IReadOnlyList<string> SearchDataSources();
}
