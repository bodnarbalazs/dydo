namespace DynaDocs.Sync;

using DynaDocs.Models;

/// <summary>
/// 3-way merge of ordered frontmatter field maps, key by key (Decision 025 §3). A key changed
/// on only one side takes that side's value; a key both sides changed to different values is a
/// conflict — the repo side wins deterministically and the overlap is reported. Key order
/// follows the repo side, with external-only new keys appended.
/// </summary>
public static class FieldMerge
{
    public sealed class Result
    {
        public required List<SyncField> Fields { get; init; }
        public required bool Conflicted { get; init; }
    }

    public static Result Merge(List<SyncField> baseFields, List<SyncField> repo, List<SyncField> external)
    {
        var baseMap = ToMap(baseFields);
        var repoMap = ToMap(repo);
        var extMap = ToMap(external);

        var conflicted = false;
        var merged = new List<SyncField>();
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Walk repo order first so the repo's field ordering is preserved.
        foreach (var field in repo)
        {
            taken.Add(field.Key);
            merged.Add(Resolve(field.Key, baseMap, repoMap, extMap, ref conflicted));
        }

        // Append keys that exist only on the external side (external-only additions).
        foreach (var field in external)
        {
            if (taken.Contains(field.Key)) continue;
            taken.Add(field.Key);
            // External added a key the repo never had; if base also lacked it, it's a clean add.
            merged.Add(new SyncField { Key = field.Key, Value = field.Value });
        }

        return new Result { Fields = merged, Conflicted = conflicted };
    }

    private static SyncField Resolve(
        string key,
        Dictionary<string, string> baseMap,
        Dictionary<string, string> repoMap,
        Dictionary<string, string> extMap,
        ref bool conflicted)
    {
        baseMap.TryGetValue(key, out var b);
        repoMap.TryGetValue(key, out var r);
        var hasExt = extMap.TryGetValue(key, out var e);

        // Repo holds this key. If external also has it and both diverge from base differently,
        // that's a true overlap.
        if (hasExt && r != e)
        {
            var repoChanged = r != b;
            var extChanged = e != b;
            if (repoChanged && extChanged)
            {
                conflicted = true;
                return new SyncField { Key = key, Value = r! }; // repo wins deterministically
            }
            // Only one side changed — take the changed side.
            return new SyncField { Key = key, Value = repoChanged ? r! : e! };
        }

        return new SyncField { Key = key, Value = r! };
    }

    private static Dictionary<string, string> ToMap(List<SyncField> fields)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in fields)
            map[f.Key] = f.Value;
        return map;
    }
}
