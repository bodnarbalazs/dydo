namespace DynaDocs.Sync.Notion.Provisioning;

using System.Text.Json;
using DynaDocs.Serialization;
using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// Ensures a sync model's databases exist in Notion, idempotently (slice brief §3). State (object type
/// → {databaseId, dataSourceId}) is persisted under the gitignored <c>dydo/_system/.local/notion/</c>
/// tree, mirroring <c>BaseSnapshotStore</c>'s source-generated JSON pattern. A recorded type is reused
/// only if its database still exists and still owns the recorded data source; otherwise it is created.
/// Databases are created in dependency order, so a child's relation to a <em>parent</em> resolves in the
/// single create. Edges that cannot exist at create time run a post-create PATCH pass: self-relations
/// (the type's own data source id is not known until it is created), rollups (they read a reverse relation
/// that exists only after the child's dual-property relation), and formulas that read a rollup, a
/// self-relation, or another formula. Generic over the model: no object-type names appear here.
/// </summary>
public sealed class NotionProvisioner
{
    private readonly INotionClient _client;
    private readonly string _statePath;
    private readonly Dictionary<string, NotionProvisionedType> _state;

    public NotionProvisioner(INotionClient client, string statePath)
    {
        _client = client;
        _statePath = statePath;
        _state = Load(statePath).Types.ToDictionary(t => t.ObjectType);
    }

    /// <summary>The provision-state file path under the gitignored .local/ tree.</summary>
    public static string PathFor(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ".local", "notion", "provision.json");

    /// <summary>The recorded, still-valid ids for a type, or null if it must be (re)created.</summary>
    public NotionProvisionedType? Lookup(string objectType) =>
        _state.TryGetValue(objectType, out var rec) && StillValid(rec) ? rec : null;

    /// <summary>Create the database for a model object type and record its ids. Relation properties
    /// resolve their target via <paramref name="resolvedDataSourceIds"/> (parent type → data source id).
    /// A self-relation (target == this type) cannot be declared at create time — the type's own data
    /// source id does not exist yet — so it is deferred to the recorded-but-not-yet-passed post-pass
    /// (<see cref="AddSelfRelations"/>), alongside rollups and deferred formulas. Non-self relations keep
    /// the single-pass path.</summary>
    public NotionProvisionedType Create(SyncObjectType type, string parentPageId, IReadOnlyDictionary<string, string> resolvedDataSourceIds)
    {
        var selfRelations = SelfRelations(type);
        // Rollups reference a reverse relation that only exists once the CHILD's dual-property relation is
        // created — later in the run — so they are deferred entirely to the post-create pass (AddRollups),
        // never emitted here.
        var rollups = type.Properties.Where(p => p.Value.Type == "rollup").ToList();
        // A formula that reads a rollup or another formula (health/attention, DR 030 §2/§4) can only be
        // created once those exist, so it is deferred to AddFormulas after the rollup pass. A leaf formula
        // that reads only stored properties (e.g. done) stays in the single-pass create.
        var deferredFormulas = DeferredFormulaNames(type);
        var firstPass = type.Properties.Where(p =>
            !selfRelations.Contains(p) && !rollups.Contains(p) && !deferredFormulas.Contains(p.Key));

        var db = Push($"provisioning {type.Type} (create database)", () => _client.CreateDatabase(new NotionDatabaseCreateRequest
        {
            Parent = new NotionDatabaseParent { PageId = parentPageId },
            Title = NotionRichText.Of(type.NotionTitle),
            Icon = NotionIcon.Of(type.Icon),
            InitialDataSource = new NotionInitialDataSource { Properties = BuildSchema(firstPass, resolvedDataSourceIds) },
        }));

        var record = new NotionProvisionedType
        {
            ObjectType = type.Type,
            DatabaseId = db.Id,
            DataSourceId = db.DataSources.Count > 0 ? db.DataSources[0].Id : "",
        };
        _state[type.Type] = record;
        // Persist the instant this database exists (finding 3): the self-relation PATCH, rollups, and deferred
        // formulas all run in the recorded-but-not-yet-passed post-pass, so a throw in ANY of them — including
        // the self-relation PATCH that used to sit between CreateDatabase and this Save — leaves the id
        // recorded. The retry reuses the database (never duplicates the whole board) and completes the pending
        // post-pass with the same idempotent PATCH semantics as rollups/deferred formulas.
        Save();
        return record;
    }

    /// <summary>Whether a recorded type still owes its rollup/formula post-pass — created (so present in the
    /// state) but not yet post-passed. A mid-provision throw records the first N-1 databases without running
    /// their post-pass; a retry reuses them, so the post-pass must re-run for every recorded-but-unpassed
    /// type or their attention-layer rollups/formulas would be silently missing forever (review R2-1). An
    /// unrecorded type returns false — it will be created and post-passed in the same run.</summary>
    public bool PostPassPending(string objectType) =>
        _state.TryGetValue(objectType, out var rec) && !rec.PostPassDone;

    /// <summary>Record that a type's post-pass has completed, persisting immediately so a later throw in the
    /// same run does not lose the fact (mirrors the per-create Save).</summary>
    public void MarkPostPassDone(string objectType)
    {
        if (_state.TryGetValue(objectType, out var rec) && !rec.PostPassDone)
        {
            rec.PostPassDone = true;
            Save();
        }
    }

    /// <summary>Whether this type carries a self-relation the post-create pass must PATCH on once its own
    /// data source id is known.</summary>
    public static bool HasSelfRelations(SyncObjectType type) => SelfRelations(type).Count > 0;

    /// <summary>Post-create pass (finding 3): PATCH this type's self-relations onto its OWN data source, now
    /// that its data source id exists. Runs before rollups/formulas so a deferred formula reading a
    /// self-relation finds it present. Idempotent — a retry re-PATCHes the same schema, same semantics as
    /// <see cref="AddRollups"/>/<see cref="AddFormulas"/>.</summary>
    public void AddSelfRelations(SyncObjectType type)
    {
        var selfRelations = SelfRelations(type);
        if (selfRelations.Count == 0)
            return;
        var dataSourceId = _state[type.Type].DataSourceId;
        var withSelf = new Dictionary<string, string> { [type.Type] = dataSourceId };
        Push($"provisioning {type.Type} self-relation properties", () =>
            _client.UpdateDataSource(dataSourceId, new NotionDataSourceUpdateRequest
            {
                Properties = BuildSchema(selfRelations, withSelf),
            }));
    }

    private static List<KeyValuePair<string, SyncPropertyDef>> SelfRelations(SyncObjectType type) =>
        type.Properties.Where(p => p.Value.Type == "relation" && p.Value.To == type.Type).ToList();

    /// <summary>Whether this type carries rollup properties the post-create pass must PATCH on once every
    /// database (and its dual-property reverse relations) exists.</summary>
    public static bool HasRollups(SyncObjectType type) =>
        type.Properties.Values.Any(p => p.Type == "rollup");

    /// <summary>Post-create pass (DR 029 §5): PATCH this type's rollup properties onto its data source, now
    /// that the children whose dual-property relations create the reverse relations exist. Extends the
    /// self-relation two-pass machinery one level up — from within-type to cross-type deferral.</summary>
    public void AddRollups(SyncObjectType type)
    {
        var rollups = type.Properties.Where(p => p.Value.Type == "rollup").ToList();
        if (rollups.Count == 0)
            return;
        Push($"provisioning {type.Type} rollup properties", () =>
            _client.UpdateDataSource(_state[type.Type].DataSourceId, new NotionDataSourceUpdateRequest
            {
                Properties = BuildSchema(rollups, EmptyResolved),
            }));
    }

    /// <summary>Whether this type carries formulas the post-create pass must PATCH on after the rollups they
    /// (or the formulas they read) depend on exist.</summary>
    public static bool HasDeferredFormulas(SyncObjectType type) => DeferredFormulaNames(type).Count > 0;

    /// <summary>Post-rollup pass (DR 030 §2/§4): PATCH this type's deferred formulas — health, attention —
    /// onto its data source now that the rollups and formulas they read exist. Emitted one at a time in
    /// dependency order (a formula after the formulas it references), so each formula's referents are already
    /// present when Notion validates its expression.</summary>
    public void AddFormulas(SyncObjectType type)
    {
        foreach (var formula in OrderedDeferredFormulas(type))
            Push($"provisioning {type.Type}.{formula.Key} (formula: {formula.Value.Expression})", () =>
                _client.UpdateDataSource(_state[type.Type].DataSourceId, new NotionDataSourceUpdateRequest
                {
                    Properties = BuildSchema([formula], EmptyResolved),
                }));
    }

    /// <summary>Whether this type declares views to provision beyond Notion's auto-created default.</summary>
    public static bool HasViews(SyncObjectType type) => type.Views is { Count: > 0 };

    /// <summary>Post-pass (board-views feature): create this type's declared views on its database, now that
    /// every property/rollup/formula they reference by id exists. Runs last in the per-type post-pass so the
    /// live schema — fetched here for the name→id map a view config needs — is complete. Views ride the same
    /// once-per-database <c>PostPassDone</c> idempotency as rollups/formulas: created on a fresh mint, not
    /// re-created on a reuse tick (a deleted view is not restored, mirroring schema-drift's create-only policy).</summary>
    public void AddViews(SyncObjectType type)
    {
        if (type.Views is not { Count: > 0 } views)
            return;
        var record = _state[type.Type];
        // The auto-created default view(s) present BEFORE we add ours — captured so they can be removed after,
        // once the declared views exist (Notion requires a database keep at least one view). The default is a
        // worse-ordered duplicate of the "All" view, so the board reads cleaner without it.
        var defaultViews = _client.ListViewIds(record.DatabaseId);
        var idByName = _client.RetrieveDataSource(record.DataSourceId).Properties
            .ToDictionary(p => p.Key, p => p.Value.Id ?? "");
        foreach (var view in views)
            Push($"provisioning {type.Type} view \"{view.Name}\"",
                () => _client.CreateView(BuildView(type, record, view, idByName)));
        foreach (var viewId in defaultViews)
            Push($"provisioning {type.Type} (remove default view)", () => _client.DeleteView(viewId));
    }

    private static NotionViewCreateRequest BuildView(
        SyncObjectType type, NotionProvisionedType record, SyncViewDef view, IReadOnlyDictionary<string, string> idByName) => new()
    {
        DatabaseId = record.DatabaseId,
        DataSourceId = record.DataSourceId,
        Name = view.Name,
        Type = view.Type,
        Filter = BuildFilter(view.Filter, type),
        Sorts = view.Sort?.Select(s => new NotionViewSortBody { Property = s.Property, Direction = s.Direction }).ToList(),
        Configuration = BuildConfig(type, view, idByName),
    };

    private static NotionViewConfiguration BuildConfig(
        SyncObjectType type, SyncViewDef view, IReadOnlyDictionary<string, string> idByName)
    {
        var config = new NotionViewConfiguration { Type = view.Type, Properties = BuildColumns(type, view, idByName) };
        ApplyGroupingAndDates(config, view, idByName);
        return config;
    }

    /// <summary>The view's column list in the model's declared property order — the source of truth for
    /// display order — each visible unless it is a compute-only helper (<see cref="SyncPropertyDef.Hidden"/>)
    /// or this view hides it. Live-schema properties absent from the model (Notion's auto-created reverse
    /// relations) are appended hidden, so a board never shows a raw "Sprints"/"Tasks" back-reference column.</summary>
    private static List<NotionViewPropertyRef> BuildColumns(
        SyncObjectType type, SyncViewDef view, IReadOnlyDictionary<string, string> idByName)
    {
        var hide = view.Hide is { } h ? new HashSet<string>(h, StringComparer.Ordinal) : [];
        var props = new List<NotionViewPropertyRef>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, def) in type.Properties)
            if (idByName.TryGetValue(name, out var id) && id.Length > 0)
            {
                props.Add(new NotionViewPropertyRef { PropertyId = id, Visible = !(def.Hidden || hide.Contains(name)) });
                placed.Add(name);
            }
        foreach (var (name, id) in idByName)
            if (!placed.Contains(name) && id.Length > 0)
                props.Add(new NotionViewPropertyRef { PropertyId = id, Visible = false });
        return props;
    }

    /// <summary>Layer the view-type-specific layout onto <paramref name="config"/>: a board's group-by column
    /// and a timeline's start/end date columns, each resolved by name against the live schema and applied only
    /// when the model declares it and the id exists.</summary>
    private static void ApplyGroupingAndDates(
        NotionViewConfiguration config, SyncViewDef view, IReadOnlyDictionary<string, string> idByName)
    {
        if (view.Type == "board" && view.GroupBy != null && idByName.TryGetValue(view.GroupBy, out var groupId))
            config.GroupBy = new NotionViewGroupBy { PropertyId = groupId };
        if (view.Type == "timeline")
        {
            if (view.DateStart != null && idByName.TryGetValue(view.DateStart, out var startId))
                config.DatePropertyId = startId;
            if (view.DateEnd != null && idByName.TryGetValue(view.DateEnd, out var endId))
                config.EndDatePropertyId = endId;
        }
    }

    /// <summary>Translate a model filter (property name + operator + value) to Notion's type-matched filter
    /// body, chosen from the property's declared type: a <c>select</c> equals/does_not_equal, a <c>checkbox</c>
    /// equals, or a <c>rollup</c> count &gt; 0. Notion cannot filter a formula column ("formula of unknown
    /// type"), so the Needs-Attention view targets <c>needs-human</c> — a checkbox on the leaf types, a checked
    /// rollup on the parents — the strongest and reliably-filterable human-queue signal. An unknown property
    /// type falls back to a select condition.</summary>
    private static NotionViewFilterBody? BuildFilter(SyncViewFilter? filter, SyncObjectType type)
    {
        if (filter == null)
            return null;
        var body = new NotionViewFilterBody { Property = filter.Property };
        var propType = type.Properties.TryGetValue(filter.Property, out var def) ? def.Type : "select";
        var wantTrue = filter.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
        switch (propType)
        {
            case "checkbox":
                body.Checkbox = new NotionViewCheckboxCondition { EqualsValue = wantTrue };
                break;
            case "rollup":
                body.Rollup = new NotionViewRollupCondition { Number = new NotionViewNumberCondition { GreaterThan = 0 } };
                break;
            default:
                body.Select = filter.Operator == "does_not_equal"
                    ? new NotionViewTextCondition { DoesNotEqual = filter.Value }
                    : new NotionViewTextCondition { EqualsValue = filter.Value };
                break;
        }
        return body;
    }

    /// <summary>The names of formulas that must be deferred past the create: a formula that reads a rollup,
    /// a self-relation, or any other formula. A formula reading only stored properties is created inline.</summary>
    private static HashSet<string> DeferredFormulaNames(SyncObjectType type)
    {
        var late = type.Properties
            .Where(p => p.Value.Type == "rollup" || (p.Value.Type == "relation" && p.Value.To == type.Type))
            .Select(p => p.Key)
            .Concat(type.Properties.Where(p => p.Value.Type == "formula").Select(p => p.Key))
            .ToHashSet();

        var deferred = new HashSet<string>();
        foreach (var (name, def) in type.Properties)
            if (def.Type == "formula" && def.Expression != null
                && late.Any(other => other != name && Reads(def.Expression, other)))
                deferred.Add(name);
        return deferred;
    }

    /// <summary>The deferred formulas in dependency order — a formula after every deferred formula it reads —
    /// so emitting them in sequence never forward-references a not-yet-created formula.</summary>
    private static List<KeyValuePair<string, SyncPropertyDef>> OrderedDeferredFormulas(SyncObjectType type)
    {
        var deferred = DeferredFormulaNames(type);
        var pending = type.Properties.Where(p => deferred.Contains(p.Key)).ToList();
        var ordered = new List<KeyValuePair<string, SyncPropertyDef>>();
        var placed = new HashSet<string>();
        while (pending.Count > 0)
        {
            var ready = pending
                .Where(p => !pending.Any(q => q.Key != p.Key && Reads(p.Value.Expression!, q.Key)))
                .ToList();
            // A residual cycle (should never occur in a valid model) would leave nothing ready — emit the
            // remainder as-is rather than loop forever.
            if (ready.Count == 0)
                ready = pending;
            foreach (var p in ready)
            {
                ordered.Add(p);
                placed.Add(p.Key);
            }
            pending = pending.Where(p => !placed.Contains(p.Key)).ToList();
        }
        return ordered;
    }

    private static bool Reads(string expression, string propertyName) =>
        expression.Contains($"prop(\"{propertyName}\")");

    private static readonly Dictionary<string, string> EmptyResolved = new();

    /// <summary>Run a schema push, re-throwing any Notion rejection tagged with what was being provisioned.
    /// A Notion schema error — most opaquely <c>"Type error with formula"</c> — names neither the object type
    /// nor the property, so a bare failure is unactionable; the context turns it into
    /// <c>"provisioning Sprint.health (formula: …) — Notion API returned 400: …"</c>. The
    /// <c>Context == null</c> guard tags only the innermost push, so an already-annotated exception passes
    /// through unwrapped.</summary>
    private static T Push<T>(string context, Func<T> push)
    {
        try
        {
            return push();
        }
        catch (NotionApiException e) when (e.Context == null)
        {
            throw new NotionApiException(e.StatusCode, e.Body, context);
        }
    }

    private static void Push(string context, Action push) =>
        Push(context, () => { push(); return true; });

    /// <summary>The <c>properties</c> schema map for a set of model properties — the one place a model
    /// property's type is translated to its Notion schema body. Relation properties resolve their
    /// target's data source id from <paramref name="resolvedDataSourceIds"/>.</summary>
    private static Dictionary<string, NotionPropertySchema> BuildSchema(
        IEnumerable<KeyValuePair<string, SyncPropertyDef>> properties,
        IReadOnlyDictionary<string, string> resolvedDataSourceIds) =>
        properties.ToDictionary(p => p.Key, p => ToSchema(p.Value, resolvedDataSourceIds));

    /// <summary>Type key → schema builder. Table-driven (like <c>NotionPropertyMapper</c>'s reader/writer
    /// tables) so each property type is a small, independently-testable body and a string switch's inflated
    /// branch complexity is avoided. A relation resolves its target's data source id from the passed map.</summary>
    private static readonly Dictionary<string, Func<SyncPropertyDef, IReadOnlyDictionary<string, string>, NotionPropertySchema>> Builders = new()
    {
        ["title"] = (_, _) => new() { Title = new NotionEmptyConfig() },
        ["rich_text"] = (_, _) => new() { RichText = new NotionEmptyConfig() },
        ["number"] = (_, _) => new() { Number = new NotionEmptyConfig() },
        ["date"] = (_, _) => new() { Date = new NotionEmptyConfig() },
        ["checkbox"] = (_, _) => new() { Checkbox = new NotionEmptyConfig() },
        ["select"] = (p, _) => new() { Select = new NotionSelectSchema { Options = SelectOptions(p) } },
        ["relation"] = (p, resolved) => new() { Relation = RelationSchema(p, resolved) },
        ["formula"] = (p, _) => new() { Formula = new NotionFormulaSchema { Expression = p.Expression! } },
        ["rollup"] = (p, _) => new() { Rollup = RollupSchema(p) },
    };

    private static NotionPropertySchema ToSchema(SyncPropertyDef prop, IReadOnlyDictionary<string, string> resolvedDataSourceIds) =>
        Builders.TryGetValue(prop.Type, out var build)
            ? build(prop, resolvedDataSourceIds)
            : throw new SyncModelException($"unsupported property type '{prop.Type}'");

    /// <summary>A select's options, each tagged with its palette color from the model's colors map (DR 029's
    /// color language). An option absent from the map provisions with Notion's default color.</summary>
    private static List<NotionSelectOption> SelectOptions(SyncPropertyDef prop) =>
        (prop.Options ?? []).Select(o => new NotionSelectOption
        {
            Name = o,
            Color = prop.Colors != null && prop.Colors.TryGetValue(o, out var color) ? color : null,
        }).ToList();

    /// <summary>A relation schema, dual-property when the model names a <see cref="SyncPropertyDef.Reverse"/>
    /// back-reference (so a rollup or derived column can read the reverse), single-property otherwise.</summary>
    private static NotionRelationSchema RelationSchema(SyncPropertyDef prop, IReadOnlyDictionary<string, string> resolvedDataSourceIds)
    {
        var schema = new NotionRelationSchema { DataSourceId = resolvedDataSourceIds[prop.To!] };
        if (prop.Reverse != null)
        {
            schema.Type = "dual_property";
            schema.SingleProperty = null;
            schema.DualProperty = new NotionDualPropertyConfig { SyncedPropertyName = prop.Reverse };
        }
        return schema;
    }

    private static NotionRollupSchema RollupSchema(SyncPropertyDef prop) => new()
    {
        RelationPropertyName = prop.RollupRelation!,
        RollupPropertyName = prop.RollupProperty!,
        Function = prop.RollupFunction!,
    };

    /// <summary>Persist provision state. Private: every fact is written the instant it is established — a
    /// database's ids in <see cref="Create"/>, a completed post-pass in <see cref="MarkPostPassDone"/> — so there
    /// is no external caller (wave 6 removed the last one, finding 9). A standalone Save would only re-serialize
    /// identical state.</summary>
    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        var file = new NotionProvisionState { Types = _state.Values.OrderBy(t => t.ObjectType).ToList() };
        File.WriteAllText(_statePath, JsonSerializer.Serialize(file, NotionProvisionJsonContext.Default.NotionProvisionState));
    }

    /// <summary>A recorded type is valid only if its database still exists and still owns the recorded
    /// data source — so a deleted or replaced database is detected and re-provisioned. Only a DEFINITIVE
    /// not-found (HTTP 404 / Notion <c>object_not_found</c>) counts as gone: a transient probe failure
    /// (429 rate-limit, 5xx) must propagate and abort the tick, never be misread as a deleted database —
    /// else the provisioner re-creates an empty data source and the same tick mass-deletes every repo doc
    /// whose base points at the now-absent pages (finding 1).</summary>
    private bool StillValid(NotionProvisionedType record)
    {
        try
        {
            return _client.RetrieveDatabase(record.DatabaseId).DataSources.Any(d => d.Id == record.DataSourceId);
        }
        catch (NotionApiException e) when (IsDefinitiveNotFound(e))
        {
            return false;
        }
    }

    /// <summary>Whether a Notion error definitively means the object is gone — HTTP 404, or the API's
    /// <c>object_not_found</c> error code — as opposed to a transient failure (rate-limit, server error)
    /// that must not be mistaken for a deleted database.</summary>
    private static bool IsDefinitiveNotFound(NotionApiException e) =>
        e.StatusCode == 404 || e.Message.Contains("object_not_found", StringComparison.Ordinal);

    private static NotionProvisionState Load(string path)
    {
        if (!File.Exists(path))
            return new NotionProvisionState();
        return JsonSerializer.Deserialize(File.ReadAllText(path), NotionProvisionJsonContext.Default.NotionProvisionState)
            ?? new NotionProvisionState();
    }
}
