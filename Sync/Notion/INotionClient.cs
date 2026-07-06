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

    /// <summary>Create a database view (POST /v1/views) — a board/table/timeline beyond the auto-created
    /// default, with its own filter, sorts, and column order/visibility.</summary>
    void CreateView(NotionViewCreateRequest request);

    /// <summary>List a database's view ids (GET /v1/views?database_id=…) — used to find the auto-created
    /// default view so it can be removed after the declared views are added.</summary>
    IReadOnlyList<string> ListViewIds(string databaseId);

    /// <summary>Delete a database view (DELETE /v1/views/{id}).</summary>
    void DeleteView(string viewId);

    /// <summary>Query a data source, following pagination, returning every page.</summary>
    IReadOnlyList<NotionPage> QueryDataSource(string dataSourceId);

    NotionPage CreatePage(NotionPageCreateRequest request);

    NotionPage UpdatePage(string pageId, NotionPageUpdateRequest request);

    /// <summary>Read a page's block children, following pagination, returning every block. A page's body
    /// is its block children; a nested sub-page shows up here as a <c>child_page</c> block (DR 033).</summary>
    IReadOnlyList<NotionBlock> GetBlockChildren(string blockId);

    /// <summary>Enumerate the sub-pages nested directly under a page — its <c>child_page</c> blocks (DR 033
    /// §3). The docs mirror walks the page tree with this the way the spine queries a data source.</summary>
    IReadOnlyList<NotionChildPage> GetChildPages(string parentPageId);

    /// <summary>Append block children to a page, chunked at Notion's 100-children-per-request cap (DR 033).</summary>
    void AppendBlockChildren(string blockId, NotionAppendChildrenRequest request);

    /// <summary>Archive (soft-delete) a single block — used to clear a page body before re-appending.</summary>
    void DeleteBlock(string blockId);

    /// <summary>Discover accessible data-source ids via POST /v1/search.</summary>
    IReadOnlyList<string> SearchDataSources();
}
