namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync.Notion.Dtos;
using DynaDocs.Utils;

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
    // Write direction is keyed by relation FIELD name so each field resolves against its own target type's
    // local↔page map — a bare-stem merged map would send a colliding id to the wrong database (slice brief §3).
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? _relationLocalToPageIdByField;
    // Read direction stays a single merged page-id→local-id map: Notion page ids are globally unique, so no two
    // target types can collide on a key, and no per-field split is needed.
    private readonly IReadOnlyDictionary<string, string>? _relationPageIdToLocalId;
    private readonly string? _icon;
    private readonly IReadOnlyDictionary<string, string>? _engineComputedSchema;
    private readonly Func<string, string?>? _engineComputedValue;
    // Page ids this type's base snapshot already maps to some local id at tick start — pages an ambiguous-create
    // recovery must NOT adopt, since they belong to another record (ns-5, review major 1). Duplicate titles are
    // legal in PM records, so title alone cannot distinguish an orphaned lost-create from a long-mapped page.
    private readonly IReadOnlySet<string> _mappedExternalIds;
    private IReadOnlyDictionary<string, string> _schema = new Dictionary<string, string>();

    /// <summary>The engine-computed property values (non-empty only) each page currently carries, captured
    /// on the last read and keyed by page id. An engine-computed refresh consults this to skip a write when
    /// the page already holds the target value, keeping a no-op tick a no-op (finding 1).</summary>
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _externalEngineComputed = new();

    /// <param name="engineComputedSchema">Engine-owned property name → Notion type (e.g. <c>last-activity</c> →
    /// <c>date</c>, DR 030 §3). These flow ONE-WAY: written on every upsert from <paramref name="engineComputedValue"/>,
    /// and dropped on read so they never enter a base snapshot or frontmatter (no edit loop). Null when the
    /// type declares none.</param>
    /// <param name="engineComputedValue">Given a local id, the current value for the engine-computed properties
    /// (the engine's derived last-activity date), or null to write nothing for that object this tick.</param>
    /// <param name="mappedExternalIds">Page ids already mapped to some local id in this type's base snapshot at
    /// tick start. An ambiguous-create recovery excludes these so it never adopts a page that belongs to another
    /// record (ns-5). Null/empty on a freshly minted database (nothing mapped yet).</param>
    public NotionSyncAdapter(
        INotionClient client,
        string dataSourceId,
        IReadOnlyDictionary<string, string>? schema = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? relationLocalToPageIdByField = null,
        IReadOnlyDictionary<string, string>? relationPageIdToLocalId = null,
        string? icon = null,
        IReadOnlyDictionary<string, string>? engineComputedSchema = null,
        Func<string, string?>? engineComputedValue = null,
        IReadOnlySet<string>? mappedExternalIds = null)
    {
        _client = client;
        _dataSourceId = dataSourceId;
        _explicitSchema = schema;
        _relationLocalToPageIdByField = relationLocalToPageIdByField;
        _relationPageIdToLocalId = relationPageIdToLocalId;
        _icon = icon;
        _engineComputedSchema = engineComputedSchema is { Count: > 0 } ? engineComputedSchema : null;
        _engineComputedValue = engineComputedValue;
        _mappedExternalIds = mappedExternalIds ?? new HashSet<string>();
    }

    public bool WritesEngineComputed => _engineComputedSchema != null;

    public IReadOnlyList<SyncRecord> ReadExternalState()
    {
        var pages = _client.QueryDataSource(_dataSourceId);
        // A provisioned data source knows its own schema; otherwise infer it from the live pages so
        // we never push a property of the wrong type or one the DB doesn't have.
        _schema = _explicitSchema ?? NotionPropertyMapper.InferSchema(pages);
        _externalEngineComputed.Clear();

        var records = new List<SyncRecord>();
        foreach (var page in pages)
        {
            if (page.Archived)
                continue;
            var allFields = NotionPropertyMapper.ToFields(page.Properties, _relationPageIdToLocalId);
            // Capture the page's current engine-computed values so a refresh can tell whether the one-way
            // last-activity already matches the engine and skip a needless write (finding 1). Empty values
            // are omitted, so a page missing the property reads as "not yet in sync".
            if (_engineComputedSchema != null)
                _externalEngineComputed[page.Id] = allFields
                    .Where(f => _engineComputedSchema.ContainsKey(f.Key) && !string.IsNullOrEmpty(f.Value))
                    .ToDictionary(f => f.Key, f => f.Value);
            var blocks = ReadBlockTree(page.Id);
            records.Add(new SyncRecord
            {
                ExternalId = page.Id,
                // Render only properties the schema knows: a provisioned data source carries reverse
                // relations (the dual-property "Blocks"/"Sprints" columns) and may carry rogue columns,
                // none of which are canonical — filtering to the schema keeps them out of frontmatter
                // (DR 029 §6). An engine-computed property (last-activity) is dropped too: the engine writes
                // it one-way, so reading it back into frontmatter would provoke an edit loop (DR 030 §3).
                // With an inferred schema every page property is a key, so this is a no-op.
                Fields = allFields
                    .Where(f => _schema.ContainsKey(f.Key) && _engineComputedSchema?.ContainsKey(f.Key) != true).ToList(),
                Body = NotionBlockConverter.FromBlocks(blocks),
            });
        }
        return records;
    }

    public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned) =>
        Apply(changes, assigned, new HashSet<string>(), new HashSet<string>());

    public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned, ICollection<string> deleted)
        => Apply(changes, assigned, deleted, new HashSet<string>());

    public void Apply(SyncChangeSet changes, IDictionary<string, string> assigned, ICollection<string> deleted,
        ICollection<string> emptyBodied)
    {
        var schema = EnsureSchema();

        foreach (var upsert in changes.Upserts)
        {
            var properties = NotionPropertyMapper.ToProperties(upsert.Fields, schema, _relationLocalToPageIdByField);
            EnsureTitle(properties, schema, upsert);
            AddEngineComputed(upsert.LocalId, properties);
            var blocks = NotionBlockConverter.ToBlocks(upsert.Body);

            if (upsert.ExternalId == null)
            {
                // The create carries the leading run of top-level blocks whose whole subtree fits one depth-≤2,
                // ≤100-child payload; anything deeper or past 100 is appended after, keeping the emptyBodied
                // classification (ns-5) — a create with only the create-carried body succeeds, and any deferred
                // body-append failure surfaces as a body-sync error that leaves the record's snapshot un-advanced.
                var head = new List<NotionBlock>();
                var index = 0;
                var headElements = 0;
                while (index < blocks.Count && head.Count < NotionBlockAppender.MaxChildrenPerRequest
                    && headElements + NotionBlockAppender.TotalElements(blocks[index]) <= NotionBlockAppender.MaxElementsPerRequest
                    && NotionBlockAppender.IsShallow(blocks[index]))
                {
                    headElements += NotionBlockAppender.TotalElements(blocks[index]);
                    head.Add(blocks[index++]);
                }
                var createChildren = NotionBlockAppender.Cut(head).Payload;
                var tail = blocks.Skip(index).ToList();

                var request = new NotionPageCreateRequest
                {
                    Parent = new NotionParent { Type = "data_source_id", DataSourceId = _dataSourceId },
                    Properties = properties,
                    Icon = NotionIcon.Of(_icon),
                    Children = createChildren.Count > 0 ? createChildren : null,
                };
                var page = CreatePageWithRecovery(request, TitleText(properties, schema), assigned);
                // Record the id the instant the page exists, so a later create in this batch throwing
                // does not lose it (the caller persists the base in a finally) — no duplicate on retry.
                assigned[upsert.LocalId] = page.Id;
                if (tail.Count > 0)
                {
                    emptyBodied.Add(upsert.LocalId);
                    NotionBlockAppender.AppendForest(_client, page.Id, tail);
                    emptyBodied.Remove(upsert.LocalId);
                }
            }
            else
            {
                _client.UpdatePage(upsert.ExternalId, new NotionPageUpdateRequest { Properties = properties });
                ReplaceBody(upsert.ExternalId, blocks);
            }
        }

        // Engine-computed-only refreshes: write last-activity onto an existing page no upsert already
        // carried it to (a no-op, an external-to-repo write, or a page created from Notion). Only the
        // engine-computed properties are sent — the body and every canonical property are untouched — and
        // a page already holding the value is skipped, so subsequent no-op ticks issue no write (finding 1).
        foreach (var refresh in changes.EngineComputedRefreshes)
        {
            if (EngineComputedInSync(refresh.LocalId, refresh.ExternalId))
                continue;
            var properties = new Dictionary<string, NotionPropertyValue>();
            AddEngineComputed(refresh.LocalId, properties);
            if (properties.Count > 0)
                _client.UpdatePage(refresh.ExternalId, new NotionPageUpdateRequest { Properties = properties });
        }

        foreach (var externalId in changes.Deletes)
        {
            _client.UpdatePage(externalId, new NotionPageUpdateRequest { Archived = true });
            // Record the archive the instant it lands, so a later delete in this batch throwing does not
            // make the caller drop this one's base entry (it advances only confirmed archives — issue 0221).
            deleted.Add(externalId);
        }
    }

    /// <summary>Whether a page already carries the engine's current engine-computed value for every
    /// engine-computed property, so a refresh can be skipped (finding 1). A null/empty engine value means
    /// nothing to write (in sync); a page whose captured value is missing or differs is out of sync.</summary>
    private bool EngineComputedInSync(string localId, string externalId)
    {
        var value = _engineComputedValue?.Invoke(localId);
        if (string.IsNullOrEmpty(value))
            return true;
        if (!_externalEngineComputed.TryGetValue(externalId, out var current))
            return false;
        return _engineComputedSchema!.Keys.All(name => current.TryGetValue(name, out var v) && v == value);
    }

    /// <summary>Overlay the engine-owned properties (last-activity, DR 030 §3) onto an upsert's Notion
    /// payload, sourced from the engine — never from the doc's fields — so they are written one-way and can
    /// never round-trip into frontmatter. A null/empty value writes nothing for that object this tick.</summary>
    private void AddEngineComputed(string localId, Dictionary<string, NotionPropertyValue> properties)
    {
        if (_engineComputedSchema == null)
            return;
        var value = _engineComputedValue?.Invoke(localId);
        if (string.IsNullOrEmpty(value))
            return;
        foreach (var (name, type) in _engineComputedSchema)
            foreach (var prop in NotionPropertyMapper.ToProperties(
                         [new SyncField { Key = name, Value = value }],
                         new Dictionary<string, string> { [name] = type }))
                properties[prop.Key] = prop.Value;
    }

    public string NormalizeBody(string body) =>
        NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(body));

    /// <summary>Whether the board's body is just the ns-6 converter's degraded echo of the base — the pre-ns-7
    /// migration case (ns-7 blocker). The board holds blocks the OLD converter pushed; reading them with the new
    /// reader and normalizing diverges from the new normalization of the base (tables/quotes were flat, blank lines
    /// dropped). We recognise that by re-deriving the old echo of the base (<see cref="NotionLegacyEcho"/>) and
    /// normalizing both under the current converter: equal means the board simply hasn't been upgraded yet, not a
    /// real edit. The reconcile then force-pushes the repo body, upgrading the board in one tick, after which the
    /// echo matches <see cref="NormalizeBody"/> of the base and this stops firing.
    /// <para>REMOVAL: delete this method, <see cref="NotionLegacyEcho"/>, and the reconcile shim once ns-10's live
    /// run confirms every board has been re-rendered under the new converter.</para></summary>
    public bool IsStaleConverterEcho(string externalBody, string baseBody) =>
        NormalizeBody(externalBody) == NormalizeBody(NotionLegacyEcho.Render(baseBody));

    /// <summary>Map a doc's fields to the form Notion echoes back, so the engine does not read this
    /// adapter's write-time losses as an external edit (slice brief §1). A field whose key is not a
    /// property this data source can write is dropped (it is never persisted, so never read back). A
    /// relation is normalized PER ENTRY, mirroring <see cref="NotionPropertyMapper"/>'s BuildRelation
    /// echo: the comma-joined local ids are split, the subset this adapter can resolve to a parent page
    /// id is kept (the rest are omitted on write and so read back absent) and re-joined. The whole field
    /// is dropped only when a non-empty value resolves to nothing; an empty relation value stays (a valid
    /// clear the IsRelationKey probe relies on). A non-relation scalar is canonicalized so ABSENT == EMPTY
    /// == DEFAULT (issue 0164): a checkbox keeps only its "true" state and drops false/empty, every other
    /// scalar drops when empty — so Notion's schema-default echoes (false/None on properties the base never
    /// recorded) do not read as external edits. A non-empty scalar and a fully-resolvable relation keep
    /// their value verbatim, so a genuine external value change is still detected. When the schema is not
    /// yet known this is identity, to avoid masking every field.</summary>
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
            // A computed (formula/rollup) or engine-computed key is never round-trippable: ToProperties never
            // writes it and ToFields drops it on read, so recording it in the base would silently delete a
            // frontmatter key colliding with one a tick later (slice brief §4). Drop it from the normalized view.
            if (NotionPropertyMapper.IsComputedType(type) || _engineComputedSchema?.ContainsKey(field.Key) == true)
                continue;
            if (type == "relation")
            {
                var resolved = ResolveRelationSubset(field.Key, field.Value);
                if (resolved == null)
                    continue;
                normalized.Add(new SyncField { Key = field.Key, Value = resolved });
                continue;
            }
            // Canonicalize ABSENT == EMPTY == DEFAULT (issue 0164). Notion returns every schema property, so
            // an unset scalar reads back empty and an unset checkbox reads back "false" — schema-default echoes
            // the base never recorded that must not register as an external edit and churn a phantom WriteToRepo.
            // A checkbox has one meaningful state: keep "true" (canonicalized), drop everything else, so an
            // absent key, a `needs-human: false` frontmatter value, and Notion's false echo all compare equal.
            // Every other scalar drops when empty. The rule is symmetric across base/repo/external (this seam
            // normalizes all three), and the write path is untouched — a checkbox `false` still reaches
            // ToProperties to clear the board because repoChanged fires off the base holding the old value.
            // Relations keep their empty clear above so IsRelationKey's empty probe still identifies them.
            if (type == "checkbox")
            {
                if (field.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    normalized.Add(new SyncField { Key = field.Key, Value = "true" });
                continue;
            }
            if (field.Value.Length == 0)
                continue;
            normalized.Add(field);
        }

        // Reflect the write-side title fallback (issue 0290) in the echo. A doc with no title-property key is
        // pushed with a synthesized title by EnsureTitle, which Notion then returns on read — a title field the
        // title-less local/base doc has no key for, an eternal extChanged (issue 0164, title class). Adding that
        // exact synthesized value here (same helper, so the two can never drift) makes the normalized view honest
        // about what the board holds: base and external now agree and a no-op stays a no-op. A title kept VISIBLE
        // this way (never dropped) means a genuinely human-renamed board title — which differs from the synthesized
        // value — still reads as an external change and writes to the repo through the overlay, rather than being
        // restored from the local file as an adapter-invisible field.
        var titleProperty = schema.FirstOrDefault(entry => entry.Value == "title").Key;
        if (titleProperty != null && doc.Fields.All(f => f.Key != titleProperty))
            // Prepend to mirror NotionPropertyMapper.ToFields, which renders the title property FIRST — keeping the
            // recorded base's field order aligned with the echo (the compare itself is order-insensitive).
            normalized.Insert(0, new SyncField { Key = titleProperty, Value = SynthesizedTitle(doc.Fields, doc.LocalId) });

        return new SyncDoc
        {
            LocalId = doc.LocalId,
            ExternalId = doc.ExternalId,
            Fields = normalized,
            Body = doc.Body,
            SourcePath = doc.SourcePath,
        };
    }

    /// <summary>Write-side title fallback (issue 0290): a spine doc without a <c>title:</c> frontmatter key
    /// would otherwise create/update its page with no title-typed property at all, and the board renders
    /// "New page". When the schema has a title-typed property and the mapped payload carries no non-empty
    /// value for it, inject the <see cref="SynthesizedTitle"/> — frontmatter <c>title</c>, else <c>name</c>,
    /// else the local id, prettified (a raw slug like <c>swarm-0119</c> must never surface as the board
    /// title). <see cref="NormalizeFields"/> mirrors this exact value so the board's echo of it round-trips as
    /// a no-op rather than a phantom external edit (issue 0164).</summary>
    private static void EnsureTitle(
        Dictionary<string, NotionPropertyValue> properties,
        IReadOnlyDictionary<string, string> schema,
        SyncUpsert upsert)
    {
        var titleProperty = schema.FirstOrDefault(entry => entry.Value == "title").Key;
        if (titleProperty == null || properties.TryGetValue(titleProperty, out var value)
            && !string.IsNullOrWhiteSpace(NotionRichText.Flatten(value.Title)))
            return;

        properties[titleProperty] = new NotionPropertyValue
        {
            Type = "title",
            Title = NotionRichText.Of(SynthesizedTitle(upsert.Fields, upsert.LocalId)),
        };
    }

    /// <summary>The board title <see cref="EnsureTitle"/> synthesizes for a doc with no usable title property
    /// (issue 0290): the prettified frontmatter <c>title</c>, else <c>name</c>, else the local id. The single
    /// source of truth for that value, reused by <see cref="NormalizeFields"/> so its echo of the synthesized
    /// title is recognized (never read as an external edit) and the two can never drift apart (issue 0164).</summary>
    private static string SynthesizedTitle(IReadOnlyList<SyncField> fields, string localId) =>
        TitlePrettifier.Prettify(FirstNonEmpty(fields, "title") ?? FirstNonEmpty(fields, "name") ?? localId);

    private static string? FirstNonEmpty(IReadOnlyList<SyncField> fields, string key) =>
        fields.FirstOrDefault(field => field.Key == key && !string.IsNullOrWhiteSpace(field.Value))?.Value;

    /// <summary>Keep the subset of a relation's comma-joined local ids that resolves to a known parent
    /// page id, re-joined with ", " — the exact echo <see cref="NotionPropertyMapper"/> writes and reads
    /// back (same split options as BuildRelation/BuildMultiSelect). Returns "" for an empty value (a valid
    /// clear) and null when a non-empty value resolves to nothing (the adapter writes it as nothing, so
    /// the field must drop out of the normalized view entirely).</summary>
    private string? ResolveRelationSubset(string fieldKey, string value)
    {
        var ids = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ids.Length == 0)
            return "";
        IReadOnlyDictionary<string, string>? map = null;
        _relationLocalToPageIdByField?.TryGetValue(fieldKey, out map);
        var resolved = string.Join(", ", ids.Where(id => map?.ContainsKey(id) ?? false));
        return resolved.Length > 0 ? resolved : null;
    }

    /// <summary>Create a page, recovering from an ambiguous failure (ns-5). A create that dies on a 5xx or a
    /// transport throw may have succeeded server-side — CreatePage no longer blind-retries those — so re-query
    /// the data source for a page carrying the record's title: found (and NOT already claimed by another record
    /// — neither mapped in the base snapshot nor assigned earlier this batch) ⇒ adopt it, avoiding a duplicate
    /// row; not found ⇒ the create truly failed, so re-create. Excluding already-claimed pages is what keeps a
    /// second record sharing a legal duplicate title from stealing an existing page (review major 1). A rate
    /// response (429/529) or a hard 4xx is unambiguous and propagates unchanged.</summary>
    private NotionPage CreatePageWithRecovery(
        NotionPageCreateRequest request, string title, IDictionary<string, string> assigned)
    {
        try
        {
            return _client.CreatePage(request);
        }
        catch (NotionApiException e) when (e.AmbiguousOutcome)
        {
            var adopted = _client.QueryDataSource(_dataSourceId).FirstOrDefault(p =>
                !p.Archived && !_mappedExternalIds.Contains(p.Id) && !assigned.Values.Contains(p.Id)
                && PageTitle(p) == title);
            return adopted ?? _client.CreatePage(request);
        }
    }

    /// <summary>The title text a create is (or was) writing — flattened from the title-typed property in the
    /// mapped payload, so recovery matches an adopted page against exactly what would have been stored.</summary>
    private static string TitleText(
        IReadOnlyDictionary<string, NotionPropertyValue> properties, IReadOnlyDictionary<string, string> schema)
    {
        var titleProperty = schema.FirstOrDefault(entry => entry.Value == "title").Key;
        return titleProperty != null && properties.TryGetValue(titleProperty, out var value)
            ? NotionRichText.Flatten(value.Title)
            : "";
    }

    /// <summary>A page's title text — flattened from its title-typed property, whichever name it carries.</summary>
    private static string PageTitle(NotionPage page) =>
        NotionRichText.Flatten(page.Properties.Values.FirstOrDefault(v => v.Type == "title")?.Title);

    /// <summary>Replace a page's body by appending the new blocks FIRST, then deleting the previously
    /// existing ones (their ids snapshotted before the append). Notion has no atomic body replace, so
    /// ordering the append before the delete means a failed append leaves the original body intact — at
    /// worst temporarily duplicated, never empty (slice brief §5).</summary>
    private void ReplaceBody(string pageId, List<NotionBlock> blocks)
    {
        // Snapshot the existing TOP-LEVEL block ids before appending; archiving a top-level block trashes its
        // descendants too, so deleting the old roots clears the whole prior body.
        var existingIds = _client.GetBlockChildren(pageId)
            .Select(b => b.Id)
            .Where(id => id != null)
            .ToList();

        NotionBlockAppender.AppendForest(_client, pageId, blocks);

        foreach (var id in existingIds)
            _client.DeleteBlock(id!);
    }

    /// <summary>Read a page's body as a nested block tree. Notion returns one level of children per request and
    /// flags a block that has more via <c>has_children</c>, so recurse only into flagged blocks — a flat body then
    /// costs no extra reads, while a nested list is reconstructed so the round-trip matches what was written and an
    /// untouched doc stays idempotent across ticks.</summary>
    private IReadOnlyList<NotionBlock> ReadBlockTree(string blockId)
    {
        var blocks = _client.GetBlockChildren(blockId);
        foreach (var block in blocks)
            // A child_page is a nested sub-page (DR 033), not body — FromBlocks drops it, so never descend into
            // one and pull a whole sub-page's content in as if it were this page's body.
            if (block.HasChildren == true && block.Id != null && block.Type != "child_page")
                block.Children = ReadBlockTree(block.Id).ToList();
        return blocks;
    }

    /// <summary>Apply may run before a read in principle; resolve the schema once if so.</summary>
    private IReadOnlyDictionary<string, string> EnsureSchema()
    {
        if (_schema.Count == 0)
            _schema = _explicitSchema ?? NotionPropertyMapper.InferSchema(_client.QueryDataSource(_dataSourceId));
        return _schema;
    }
}
