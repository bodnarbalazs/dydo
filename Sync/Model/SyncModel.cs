namespace DynaDocs.Sync.Model;

using System.Text.Json.Serialization;

/// <summary>
/// The sync model (slice brief §1): a project's object types and the relations between them, loaded
/// from <c>dydo/_system/sync-model.json</c>. Data-driven exactly like dydo roles — any project edits
/// the file to define its own types, and the engine provisions/maps/relates from it with no hardcoded
/// type names. The default model (Release, Campaign, Sprint, SprintTask, Issue) ships as a template and auto-seeds.
/// <para>
/// The default leaf type <c>SprintTask</c> (canonical dir <c>dydo/project/sprint-tasks/</c>) is the PM
/// board's work item synced to Notion. It is named distinctly on purpose: it is NOT dydo's runtime agent
/// task-tracker (<c>dydo task</c> over <c>dydo/project/tasks/</c>, schema name/assigned/status). Those are
/// separate systems with different schemas and directories.
/// </para>
/// </summary>
public sealed class SyncModel
{
    [JsonPropertyName("objects")]
    public List<SyncObjectType> Objects { get; set; } = new();

    /// <summary>The object type with the given key, or a clear error if the model does not define it.</summary>
    public SyncObjectType Object(string type) =>
        Objects.FirstOrDefault(o => o.Type == type)
        ?? throw new SyncModelException($"sync model has no object type '{type}'");

    /// <summary>
    /// The object types in dependency order: a type's relation targets always precede it, so a child's
    /// relation can reference an already-provisioned parent data source and resolve a parent page id
    /// from the parent's just-advanced base. Throws on an unknown relation target or a relation cycle —
    /// the model must be a DAG.
    /// </summary>
    public IReadOnlyList<SyncObjectType> InDependencyOrder()
    {
        var byType = Objects.ToDictionary(o => o.Type);
        var ordered = new List<SyncObjectType>();
        var state = new Dictionary<string, bool>(); // present+false = visiting, true = done

        void Visit(SyncObjectType obj)
        {
            if (state.TryGetValue(obj.Type, out var done))
            {
                if (!done)
                    throw new SyncModelException($"sync model has a relation cycle through '{obj.Type}'");
                return;
            }
            state[obj.Type] = false;
            foreach (var target in obj.RelationTargets())
            {
                if (!byType.TryGetValue(target, out var parent))
                    throw new SyncModelException($"object '{obj.Type}' relates to unknown type '{target}'");
                Visit(parent);
            }
            state[obj.Type] = true;
            ordered.Add(obj);
        }

        foreach (var obj in Objects)
            Visit(obj);
        return ordered;
    }
}
