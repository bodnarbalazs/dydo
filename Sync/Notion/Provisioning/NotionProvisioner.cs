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
    /// resolve their target via <paramref name="resolvedDataSourceIds"/> (parent type → data source id).</summary>
    public NotionProvisionedType Create(SyncObjectType type, string parentPageId, IReadOnlyDictionary<string, string> resolvedDataSourceIds)
    {
        var db = _client.CreateDatabase(new NotionDatabaseCreateRequest
        {
            Parent = new NotionDatabaseParent { PageId = parentPageId },
            Title = NotionRichText.Of(type.NotionTitle),
            Icon = NotionIcon.Of(type.Icon),
            InitialDataSource = new NotionInitialDataSource { Properties = BuildSchema(type, resolvedDataSourceIds) },
        });

        var record = new NotionProvisionedType
        {
            ObjectType = type.Type,
            DatabaseId = db.Id,
            DataSourceId = db.DataSources.Count > 0 ? db.DataSources[0].Id : "",
        };
        _state[type.Type] = record;
        return record;
    }

    /// <summary>The create-database <c>properties</c> map for a model object type — the one place a
    /// model property's type is translated to its Notion schema body. Relation properties resolve their
    /// target's data source id from <paramref name="resolvedDataSourceIds"/> (the parent is provisioned
    /// first, so its id is present).</summary>
    private static Dictionary<string, NotionPropertySchema> BuildSchema(
        SyncObjectType type, IReadOnlyDictionary<string, string> resolvedDataSourceIds) =>
        type.Properties.ToDictionary(p => p.Key, p => ToSchema(p.Value, resolvedDataSourceIds));

    private static NotionPropertySchema ToSchema(SyncPropertyDef prop, IReadOnlyDictionary<string, string> resolvedDataSourceIds) => prop.Type switch
    {
        "title" => new() { Title = new NotionEmptyConfig() },
        "rich_text" => new() { RichText = new NotionEmptyConfig() },
        "number" => new() { Number = new NotionEmptyConfig() },
        "date" => new() { Date = new NotionEmptyConfig() },
        "select" => new() { Select = new NotionSelectSchema { Options = (prop.Options ?? []).Select(o => new NotionSelectOption { Name = o }).ToList() } },
        "relation" => new() { Relation = new NotionRelationSchema { DataSourceId = resolvedDataSourceIds[prop.To!] } },
        _ => throw new SyncModelException($"unsupported property type '{prop.Type}'"),
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
