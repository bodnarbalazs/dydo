namespace DynaDocs.Sync.Model;

using System.Text.Json.Serialization;

/// <summary>
/// One object type in the sync model (slice brief §1): its <see cref="Type"/> key, the canonical repo
/// <see cref="Dir"/> (relative to the dydo root) its docs live in, the <see cref="NotionTitle"/> for
/// the database it provisions, and its <see cref="Properties"/> by name. The model file — not C# — is
/// the single source of truth for object types, so the engine carries no per-project type knowledge.
/// </summary>
public sealed class SyncObjectType
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("dir")]
    public string Dir { get; set; } = "";

    [JsonPropertyName("notionTitle")]
    public string NotionTitle { get; set; } = "";

    /// <summary>Optional emoji icon for this type's database and its rows (e.g. "🚀"). Purely
    /// presentational; when unset, Notion shows its default page icon.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, SyncPropertyDef> Properties { get; set; } = new();

    /// <summary>The Notion views this type provisions beyond the auto-created default (board views feature).
    /// Null/empty leaves only Notion's default table view. Views are provisioned idempotently by name.</summary>
    [JsonPropertyName("views")]
    public List<SyncViewDef>? Views { get; set; }

    /// <summary>The distinct object types this type's relations point at — its provisioning and sync
    /// dependencies, so a parent is always created and reconciled before its children.</summary>
    public IEnumerable<string> RelationTargets() =>
        Properties.Values.Where(p => p.Type == "relation" && p.To != null).Select(p => p.To!).Distinct();

    /// <summary>Property name → type: the explicit schema the sync adapter writes/reads against, since
    /// a freshly provisioned (empty) database has no rows to infer a schema from.</summary>
    public Dictionary<string, string> FieldSchema() =>
        Properties.ToDictionary(p => p.Key, p => p.Value.Type);

    /// <summary>The select property that routes docs into subfolders and its option → subfolder map, or
    /// null when this type files every doc flat in its dir (slice brief §3). At most one property routes.</summary>
    public (string Field, Dictionary<string, string> Folders)? FolderRouting()
    {
        foreach (var (name, def) in Properties)
            if (def.Folders is { Count: > 0 })
                return (name, def.Folders);
        return null;
    }
}
