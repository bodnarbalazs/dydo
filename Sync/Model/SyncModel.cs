namespace DynaDocs.Sync.Model;

using System.Text.Json.Serialization;

/// <summary>
/// The sync model (slice brief §1): a project's object types and the relations between them, loaded
/// from <c>dydo/_system/sync-model.json</c>. Data-driven exactly like dydo roles — any project edits
/// the file to define its own types, and the engine provisions/maps/relates from it with no hardcoded
/// type names. The default model (Release, Campaign, Sprint, Slice, Issue) ships as a template and auto-seeds.
/// <para>
/// The default leaf type <c>Slice</c> (canonical dir <c>dydo/project/slices/</c>) is the PM
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
    /// from the parent's just-advanced base. Throws on an unknown relation target or a genuine
    /// multi-type relation cycle — the cross-type graph must be a DAG. A self-relation (a type that
    /// relates to itself, e.g. a blocked-by edge between rows of the same type) is legal and does not
    /// count as a cycle: it is a within-type edge the provisioner adds in a second pass.
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
                // A self-relation is a within-type edge, not a cross-type dependency: a type does not
                // depend on itself being ordered first, so skip it. Recursing would re-enter a node
                // that is still "visiting" and falsely report a cycle.
                if (parent == obj)
                    continue;
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
