namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;
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
    private readonly TextWriter? _warnings;
    private IReadOnlyDictionary<string, string> _schema = new Dictionary<string, string>();

    /// <summary>Page id → relation property names that currently hold more than one target. We read only
    /// the first target (single-value model) and must NOT write these back, or the extra targets would be
    /// deleted; the last read records them here so <see cref="Apply"/> omits them from the update.</summary>
    private readonly Dictionary<string, List<string>> _multiTargetRelations = new();

    public NotionSyncAdapter(
        INotionClient client,
        string dataSourceId,
        IReadOnlyDictionary<string, string>? schema = null,
        IReadOnlyDictionary<string, string>? relationLocalToPageId = null,
        IReadOnlyDictionary<string, string>? relationPageIdToLocalId = null,
        string? icon = null,
        TextWriter? warnings = null)
    {
        _client = client;
        _dataSourceId = dataSourceId;
        _explicitSchema = schema;
        _relationLocalToPageId = relationLocalToPageId;
        _relationPageIdToLocalId = relationPageIdToLocalId;
        _icon = icon;
        _warnings = warnings;
    }

    public IReadOnlyList<SyncRecord> ReadExternalState()
    {
        var pages = _client.QueryDataSource(_dataSourceId);
        // A provisioned data source knows its own schema; otherwise infer it from the live pages so
        // we never push a property of the wrong type or one the DB doesn't have.
        _schema = _explicitSchema ?? NotionPropertyMapper.InferSchema(pages);
        _multiTargetRelations.Clear();

        var records = new List<SyncRecord>();
        foreach (var page in pages)
        {
            if (page.Archived)
                continue;
            NoteMultiTargetRelations(page);
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

    public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned)
    {
        var schema = EnsureSchema();

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
                // Record the id the instant the page exists, so a later create in this batch throwing
                // does not lose it (the caller persists the base in a finally) — no duplicate on retry.
                assigned[upsert.LocalId] = page.Id;
            }
            else
            {
                // Leave any multi-target relation untouched: writing our single first-ref back would
                // delete the extra targets, so omit those properties from the update entirely.
                if (_multiTargetRelations.TryGetValue(upsert.ExternalId, out var omit))
                    foreach (var name in omit)
                        properties.Remove(name);

                _client.UpdatePage(upsert.ExternalId, new NotionPageUpdateRequest { Properties = properties });
                ReplaceBody(upsert.ExternalId, blocks);
            }
        }

        foreach (var externalId in changes.Deletes)
            _client.UpdatePage(externalId, new NotionPageUpdateRequest { Archived = true });
    }

    public string NormalizeBody(string body) =>
        NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(body));

    /// <summary>Map a doc's fields to the form Notion echoes back, so the engine does not read this
    /// adapter's write-time losses as an external edit (slice brief §1). A field whose key is not a
    /// property this data source can write is dropped (it is never persisted, so never read back), and a
    /// relation whose local id does not resolve to a known parent page id is dropped too — it is omitted on
    /// write and so reads back empty. A resolvable relation and every other supported field keep their
    /// value verbatim, so a genuine external value change is still detected. When the schema is not yet
    /// known this is identity, to avoid masking every field.</summary>
    public SyncDoc NormalizeFields(SyncDoc doc)
    {
        var schema = EnsureSchema();
        if (schema.Count == 0)
            return doc;

        var normalized = new List<SyncField>();
        foreach (var field in doc.Fields)
        {
            if (!schema.TryGetValue(field.Key, out var type))
                continue;
            if (type == "relation" && !(_relationLocalToPageId?.ContainsKey(field.Value) ?? false))
                continue;
            normalized.Add(field);
        }

        return new SyncDoc
        {
            LocalId = doc.LocalId,
            ExternalId = doc.ExternalId,
            Fields = normalized,
            Body = doc.Body,
            SourcePath = doc.SourcePath,
        };
    }

    /// <summary>Replace a page's body by appending the new blocks FIRST, then deleting the previously
    /// existing ones (their ids snapshotted before the append). Notion has no atomic body replace, so
    /// ordering the append before the delete means a failed append leaves the original body intact — at
    /// worst temporarily duplicated, never empty (slice brief §5).</summary>
    private void ReplaceBody(string pageId, List<NotionBlock> blocks)
    {
        var existingIds = _client.GetBlockChildren(pageId)
            .Select(b => b.Id)
            .Where(id => id != null)
            .ToList();

        if (blocks.Count > 0)
            _client.AppendBlockChildren(pageId, new NotionAppendChildrenRequest { Children = blocks });

        foreach (var id in existingIds)
            _client.DeleteBlock(id!);
    }

    /// <summary>Record — and warn about — relation properties on a page that hold more than one target.
    /// We read only the first (single-value model); flagging them here lets <see cref="Apply"/> omit
    /// them on write-back so the extra targets are never silently truncated (slice brief §7).</summary>
    private void NoteMultiTargetRelations(NotionPage page)
    {
        List<string>? multi = null;
        foreach (var (name, value) in page.Properties)
        {
            if (value.Type == "relation" && value.Relation is { Count: > 1 })
            {
                (multi ??= []).Add(name);
                _warnings?.WriteLine(
                    $"notion sync: relation '{name}' on '{PageDisplayName(page)}' has {value.Relation.Count} targets; "
                    + "reading only the first and leaving Notion's value untouched (multi-target relations are not yet synced).");
            }
        }
        if (multi != null)
            _multiTargetRelations[page.Id] = multi;
    }

    private static string PageDisplayName(NotionPage page)
    {
        foreach (var value in page.Properties.Values)
            if (value.Type == "title")
            {
                var title = NotionRichText.Flatten(value.Title);
                if (title.Length > 0)
                    return title;
            }
        return page.Id;
    }

    /// <summary>Apply may run before a read in principle; resolve the schema once if so.</summary>
    private IReadOnlyDictionary<string, string> EnsureSchema()
    {
        if (_schema.Count == 0)
            _schema = _explicitSchema ?? NotionPropertyMapper.InferSchema(_client.QueryDataSource(_dataSourceId));
        return _schema;
    }
}
