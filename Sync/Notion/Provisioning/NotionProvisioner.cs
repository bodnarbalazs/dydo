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
/// Databases are created in dependency order, so a child's relation can reference its parent's already
/// resolved data source id in a single create — no second PATCH pass. Generic over the model: no
/// object-type names appear here.
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
    /// source id does not exist yet — so it is deferred to a second pass: the database is created without
    /// its self-referencing relations, then those properties are PATCHed onto the data source once its
    /// id is known. Non-self relations keep the single-pass path.</summary>
    public NotionProvisionedType Create(SyncObjectType type, string parentPageId, IReadOnlyDictionary<string, string> resolvedDataSourceIds)
    {
        var selfRelations = type.Properties
            .Where(p => p.Value.Type == "relation" && p.Value.To == type.Type)
            .ToList();
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

        var db = _client.CreateDatabase(new NotionDatabaseCreateRequest
        {
            Parent = new NotionDatabaseParent { PageId = parentPageId },
            Title = NotionRichText.Of(type.NotionTitle),
            Icon = NotionIcon.Of(type.Icon),
            InitialDataSource = new NotionInitialDataSource { Properties = BuildSchema(firstPass, resolvedDataSourceIds) },
        });

        var dataSourceId = db.DataSources.Count > 0 ? db.DataSources[0].Id : "";

        if (selfRelations.Count > 0)
        {
            var withSelf = new Dictionary<string, string>(resolvedDataSourceIds) { [type.Type] = dataSourceId };
            _client.UpdateDataSource(dataSourceId, new NotionDataSourceUpdateRequest
            {
                Properties = BuildSchema(selfRelations, withSelf),
            });
        }

        var record = new NotionProvisionedType
        {
            ObjectType = type.Type,
            DatabaseId = db.Id,
            DataSourceId = dataSourceId,
        };
        _state[type.Type] = record;
        return record;
    }

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
        _client.UpdateDataSource(_state[type.Type].DataSourceId, new NotionDataSourceUpdateRequest
        {
            Properties = BuildSchema(rollups, EmptyResolved),
        });
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
            _client.UpdateDataSource(_state[type.Type].DataSourceId, new NotionDataSourceUpdateRequest
            {
                Properties = BuildSchema([formula], EmptyResolved),
            });
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

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
        var file = new NotionProvisionState { Types = _state.Values.OrderBy(t => t.ObjectType).ToList() };
        File.WriteAllText(_statePath, JsonSerializer.Serialize(file, NotionProvisionJsonContext.Default.NotionProvisionState));
    }

    /// <summary>A recorded type is valid only if its database still exists and still owns the recorded
    /// data source — so a deleted or replaced database is detected and re-provisioned.</summary>
    private bool StillValid(NotionProvisionedType record)
    {
        try
        {
            return _client.RetrieveDatabase(record.DatabaseId).DataSources.Any(d => d.Id == record.DataSourceId);
        }
        catch (NotionApiException)
        {
            return false;
        }
    }

    private static NotionProvisionState Load(string path)
    {
        if (!File.Exists(path))
            return new NotionProvisionState();
        return JsonSerializer.Deserialize(File.ReadAllText(path), NotionProvisionJsonContext.Default.NotionProvisionState)
            ?? new NotionProvisionState();
    }
}
