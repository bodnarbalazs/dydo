namespace DynaDocs.Sync.Notion;

using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// The real Notion <see cref="ISyncAdapter"/> (Decision 025, slice brief §2). Maps Notion pages of
/// one data source to/from neutral <see cref="SyncRecord"/>s: page id ↔ external id, properties ↔
/// fields (by name, via <see cref="NotionPropertyMapper"/>), block children ↔ markdown body (via
/// <see cref="NotionBlockConverter"/>). Apply creates pages for new upserts (returning the assigned
/// id keyed by local id), updates properties and replaces the body for existing ones, and archives
/// pages for deletes. All Notion knowledge stays here and in the client — delete this file and the
/// engine still runs against any other adapter.
///
/// A provisioned data source supplies its schema explicitly (a freshly created database has no rows
/// to infer from); without one, the schema is inferred from the live pages. Relation id maps thread
/// the parent type's local↔page id mapping so child relations resolve in both directions.
/// </summary>
public sealed class NotionSyncAdapter : ISyncAdapter
{
    private readonly INotionClient _client;
    private readonly string _dataSourceId;
    private readonly IReadOnlyDictionary<string, string>? _explicitSchema;
    private readonly IReadOnlyDictionary<string, string>? _relationLocalToPageId;
    private readonly IReadOnlyDictionary<string, string>? _relationPageIdToLocalId;
    private readonly string? _icon;
    private IReadOnlyDictionary<string, string> _schema = new Dictionary<string, string>();

    public NotionSyncAdapter(
        INotionClient client,
        string dataSourceId,
        IReadOnlyDictionary<string, string>? schema = null,
        IReadOnlyDictionary<string, string>? relationLocalToPageId = null,
        IReadOnlyDictionary<string, string>? relationPageIdToLocalId = null,
        string? icon = null)
    {
        _client = client;
        _dataSourceId = dataSourceId;
        _explicitSchema = schema;
        _relationLocalToPageId = relationLocalToPageId;
        _relationPageIdToLocalId = relationPageIdToLocalId;
        _icon = icon;
    }

    public IReadOnlyList<SyncRecord> ReadExternalState()
    {
        var pages = _client.QueryDataSource(_dataSourceId);
        // A provisioned data source knows its own schema; otherwise infer it from the live pages so
        // we never push a property of the wrong type or one the DB doesn't have.
        _schema = _explicitSchema ?? NotionPropertyMapper.InferSchema(pages);

        var records = new List<SyncRecord>();
        foreach (var page in pages)
        {
            if (page.Archived)
                continue;
            var blocks = _client.GetBlockChildren(page.Id);
            records.Add(new SyncRecord
            {
                ExternalId = page.Id,
                Fields = NotionPropertyMapper.ToFields(page.Properties, _relationPageIdToLocalId),
                Body = NotionBlockConverter.FromBlocks(blocks),
            });
        }
        return records;
    }

    public IReadOnlyDictionary<string, string> Apply(SyncChangeSet changes)
    {
        var schema = EnsureSchema();
        var assigned = new Dictionary<string, string>();

        foreach (var upsert in changes.Upserts)
        {
            var properties = NotionPropertyMapper.ToProperties(upsert.Fields, schema, _relationLocalToPageId);
            var blocks = NotionBlockConverter.ToBlocks(upsert.Body);

            if (upsert.ExternalId == null)
            {
                var page = _client.CreatePage(new NotionPageCreateRequest
                {
                    Parent = new NotionParent { Type = "data_source_id", DataSourceId = _dataSourceId },
                    Properties = properties,
                    Icon = NotionIcon.Of(_icon),
                    Children = blocks.Count > 0 ? blocks : null,
                });
                assigned[upsert.LocalId] = page.Id;
            }
            else
            {
                _client.UpdatePage(upsert.ExternalId, new NotionPageUpdateRequest { Properties = properties });
                ReplaceBody(upsert.ExternalId, blocks);
            }
        }

        foreach (var externalId in changes.Deletes)
            _client.UpdatePage(externalId, new NotionPageUpdateRequest { Archived = true });

        return assigned;
    }

    /// <summary>Replace a page's body: archive existing children, then append the new blocks. Notion
    /// has no atomic body replace, so this two-step is the honest equivalent for the MVP.</summary>
    private void ReplaceBody(string pageId, List<NotionBlock> blocks)
    {
        // Snapshot the ids before deleting: a delete may invalidate the children collection.
        var existingIds = _client.GetBlockChildren(pageId)
            .Select(b => b.Id)
            .Where(id => id != null)
            .ToList();
        foreach (var id in existingIds)
            _client.DeleteBlock(id!);

        if (blocks.Count > 0)
            _client.AppendBlockChildren(pageId, new NotionAppendChildrenRequest { Children = blocks });
    }

    /// <summary>Apply may run before a read in principle; resolve the schema once if so.</summary>
    private IReadOnlyDictionary<string, string> EnsureSchema()
    {
        if (_schema.Count == 0)
            _schema = _explicitSchema ?? NotionPropertyMapper.InferSchema(_client.QueryDataSource(_dataSourceId));
        return _schema;
    }
}
