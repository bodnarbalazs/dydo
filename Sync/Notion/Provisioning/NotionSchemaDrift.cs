namespace DynaDocs.Sync.Notion.Provisioning;

using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion.Dtos;

/// <summary>
/// Schema-shape ownership enforcement (DR 029 §6): the project's sync model owns the shape, one-way
/// project → Notion. This compares a provisioned data source's live schema against the model and reports
/// <em>rogue</em> additions — a property or a select option present in Notion but absent from the model.
/// The default is <strong>warn + leave</strong>: reverting would silently delete a colleague's column and
/// its data. With <c>--prune</c> the revert is performed deliberately — rogue properties are deleted and a
/// select's options are reset to the model's set (dropping rogue options). A rogue option's <em>value</em>
/// still round-trips as data; only the schema option is pruned. Reverse relations that a dual-property
/// relation synthesises on this type (e.g. "Blocks", "Sprints") are legitimate, so they are never rogue.
/// </summary>
public static class NotionSchemaDrift
{
    public static void Check(
        SyncModel model, SyncObjectType type, string dataSourceId,
        INotionClient client, bool prune, TextWriter output, NotionDataSource? live = null)
    {
        // The additive pass (ns-11) already fetched the live schema for a reused type this tick; reuse it so the
        // two model-vs-live passes cost one read, not two. A freshly minted type passes null and reads here.
        live ??= client.RetrieveDataSource(dataSourceId);
        var known = KnownNames(model, type);
        var patch = new Dictionary<string, NotionPropertySchema>();

        foreach (var name in live.Properties.Keys)
        {
            if (known.Contains(name))
                continue;
            output.WriteLine(prune
                ? $"  drift      {type.Type,-9} PRUNE rogue property \"{name}\""
                : $"  drift      {type.Type,-9} WARN rogue property \"{name}\" — left untouched (--prune to remove)");
            if (prune)
                patch[name] = null!; // a null property body deletes it (PATCH /v1/data_sources)
        }

        foreach (var (name, def) in type.Properties)
        {
            if (def.Type != "select" || !live.Properties.TryGetValue(name, out var liveProp) || liveProp.Select == null)
                continue;
            var modelOptions = new HashSet<string>(def.Options ?? []);
            var rogue = liveProp.Select.Options.Select(o => o.Name).Where(o => !modelOptions.Contains(o)).ToList();
            foreach (var option in rogue)
                output.WriteLine(prune
                    ? $"  drift      {type.Type,-9} PRUNE rogue option \"{option}\" on \"{name}\""
                    : $"  drift      {type.Type,-9} WARN rogue option \"{option}\" on \"{name}\" — value still round-trips (--prune to remove)");
            if (prune && rogue.Count > 0)
                patch[name] = new NotionPropertySchema { Select = new NotionSelectSchema { Options = ModelOptions(def) } };
        }

        if (patch.Count > 0)
            client.UpdateDataSource(dataSourceId, new NotionDataSourceUpdateRequest { Properties = patch });
    }

    /// <summary>Every property name the model legitimises for this type: its own declared properties plus
    /// the synced reverse-relation names any dual-property relation targeting this type creates.</summary>
    private static HashSet<string> KnownNames(SyncModel model, SyncObjectType type)
    {
        var known = new HashSet<string>(type.Properties.Keys);
        foreach (var other in model.Objects)
            foreach (var prop in other.Properties.Values)
                if (prop.Type == "relation" && prop.To == type.Type && prop.Reverse != null)
                    known.Add(prop.Reverse);
        return known;
    }

    private static List<NotionSelectOption> ModelOptions(SyncPropertyDef def) =>
        (def.Options ?? []).Select(o => new NotionSelectOption
        {
            Name = o,
            Color = def.Colors != null && def.Colors.TryGetValue(o, out var color) ? color : null,
        }).ToList();
}
