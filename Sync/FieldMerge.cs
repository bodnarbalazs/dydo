namespace DynaDocs.Sync;

using DynaDocs.Models;

/// <summary>
/// 3-way merge of ordered frontmatter field maps, key by key (Decision 025 §3). A key changed
/// on only one side takes that side's value; a key both sides changed to different values is a
/// conflict — the repo side wins deterministically and the overlap is reported. Deletions are
/// honored against base: a key present in base but dropped on one side is a real deletion and is
/// not resurrected from the unchanged other side (a delete overlapping the other side's edit is a
/// delete/modify conflict, and the edit is kept). Key order follows the repo side, with genuinely
/// new external-only keys appended.
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
            var resolved = ResolveRepoKey(field.Key, baseMap, repoMap, extMap, ref conflicted);
            if (resolved != null)
                merged.Add(resolved);
        }

        // Then any key present only on the external side.
        foreach (var field in external)
        {
            if (taken.Contains(field.Key)) continue;
            taken.Add(field.Key);
            var resolved = ResolveExternalOnlyKey(field.Key, baseMap, extMap, ref conflicted);
            if (resolved != null)
                merged.Add(resolved);
        }

        return new Result { Fields = merged, Conflicted = conflicted };
    }

    /// <summary>Resolve a key the repo holds. If external also holds it, it's a value-level 3-way merge.
    /// If external lacks it, it's either an external-side DELETION (present in base, repo unchanged →
    /// drop) or a genuine repo-side add (absent in base → keep). A key the repo changed while external
    /// deleted it is a delete/modify conflict — keep the repo edit.</summary>
    private static SyncField? ResolveRepoKey(
        string key,
        Dictionary<string, string> baseMap,
        Dictionary<string, string> repoMap,
        Dictionary<string, string> extMap,
        ref bool conflicted)
    {
        var hasBase = baseMap.TryGetValue(key, out var b);
        repoMap.TryGetValue(key, out var r);
        var hasExt = extMap.TryGetValue(key, out var e);

        if (hasExt)
        {
            if (r == e)
                return new SyncField { Key = key, Value = r! };
            var repoChanged = r != b;
            var extChanged = e != b;
            if (repoChanged && extChanged)
            {
                conflicted = true;
                return new SyncField { Key = key, Value = r! }; // repo wins deterministically
            }
            return new SyncField { Key = key, Value = repoChanged ? r! : e! };
        }

        // External does not hold the key.
        if (hasBase)
        {
            if (r == b)
                return null; // external deleted an unchanged key — honor the deletion
            conflicted = true;   // repo changed it, external deleted it — keep the edit
            return new SyncField { Key = key, Value = r! };
        }

        return new SyncField { Key = key, Value = r! }; // genuinely new on the repo side
    }

    /// <summary>Resolve a key only the external side holds. In base and unchanged there → repo deleted
    /// it, so drop it. Changed there while repo deleted it → delete/modify conflict, keep the external
    /// edit. Absent from base → a genuine external-side add.</summary>
    private static SyncField? ResolveExternalOnlyKey(
        string key,
        Dictionary<string, string> baseMap,
        Dictionary<string, string> extMap,
        ref bool conflicted)
    {
        var hasBase = baseMap.TryGetValue(key, out var b);
        extMap.TryGetValue(key, out var e);

        if (hasBase)
        {
            if (e == b)
                return null; // repo deleted an unchanged key — honor the deletion
            conflicted = true;   // repo deleted it, external changed it — keep the edit
            return new SyncField { Key = key, Value = e! };
        }

        return new SyncField { Key = key, Value = e! }; // genuinely new on the external side
    }

    private static Dictionary<string, string> ToMap(List<SyncField> fields)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in fields)
            map[f.Key] = f.Value;
        return map;
    }
}
