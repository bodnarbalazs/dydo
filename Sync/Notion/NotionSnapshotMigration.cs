namespace DynaDocs.Sync.Notion;

using DynaDocs.Models;

/// <summary>Classifies legacy single-body snapshot entries before projected reconciliation. A legacy base is only
/// upgraded when the present local/remote pair gives an unambiguous projected baseline; every other case stays v1
/// and is diverted to the same operator-visible shadow workflow as an ordinary projected conflict.</summary>
internal static class NotionSnapshotMigration
{
    public static NotionSnapshotMigrationPlan Classify(
        BaseSnapshotStore store, IReadOnlyList<SyncDoc> docs, IReadOnlyList<SyncRecord> records,
        NotionSyncAdapter adapter, string shadowDir, string docsDir, TextWriter output)
    {
        var plan = new NotionSnapshotMigrationPlan();
        var byLocalId = docs.ToDictionary(doc => doc.LocalId);
        foreach (var localId in store.LocalIds.Where(id => !store.IsV2(id)).ToList())
        {
            var legacy = store.Get(localId)!;
            byLocalId.TryGetValue(localId, out var repo);
            var matches = legacy.ExternalId == null
                ? [] : records.Where(record => record.ExternalId == legacy.ExternalId).ToList();
            if (repo == null)
            {
                Block(plan, localId, null, matches.Count == 1 ? matches[0] : null, legacy,
                    matches.Count == 1 ? "canonical file is unavailable" : "migration identity is ambiguous",
                    shadowDir, docsDir, output);
                continue;
            }
            if (matches.Count != 1)
            {
                Block(plan, localId, repo, null, legacy, "migration identity is ambiguous", shadowDir, docsDir, output);
                continue;
            }

            var external = matches[0];
            if (external.BodyReadStatus == SyncBodyReadStatus.Truncated)
            {
                Block(plan, localId, repo, external, legacy, "external body unavailable: truncated export", shadowDir, docsDir, output);
                continue;
            }

            var localUnchanged = Equivalent(adapter, repo.Body, legacy.Body);
            var externalUnchanged = Equivalent(adapter, external.Body, legacy.Body)
                || adapter.IsStaleConverterEcho(external.Body, legacy.Body);
            if (!localUnchanged && !externalUnchanged)
            {
                Block(plan, localId, repo, external, legacy, "migration has two-sided body edits", shadowDir, docsDir, output);
                continue;
            }

            var baseDoc = new SyncDoc
            {
                LocalId = legacy.LocalId,
                ExternalId = legacy.ExternalId,
                Fields = legacy.Fields,
                Body = localUnchanged && externalUnchanged ? repo.Body : legacy.Body,
                SourcePath = repo.SourcePath,
            };
            var bodyBase = localUnchanged && externalUnchanged
                ? new Projection.DualBodyBase(repo.Body, external.Body)
                : localUnchanged
                    ? new Projection.DualBodyBase(legacy.Body, legacy.Body)
                    : new Projection.DualBodyBase(legacy.Body, external.Body);
            plan.Adoptions[localId] = (baseDoc, bodyBase);
            output.WriteLine($"             migration {localId} adopted projected body base");
        }
        return plan;
    }

    public static void ApplyShadows(NotionSnapshotMigrationPlan plan, string shadowDir)
    {
        foreach (var (localId, shadow) in plan.Shadows)
        {
            var path = Path.Combine(shadowDir, localId + ".md");
            if (!File.Exists(path))
                SyncDocFile.Write(path, shadow);
        }
    }

    private static bool Equivalent(NotionSyncAdapter adapter, string left, string right) =>
        adapter.NormalizeBody(left) == adapter.NormalizeBody(right);

    private static void Block(NotionSnapshotMigrationPlan plan, string localId, SyncDoc? repo, SyncRecord? external, SyncDoc legacy, string reason,
        string shadowDir, string docsDir, TextWriter output)
    {
        var canonical = repo?.SourcePath is { Length: > 0 } path ? path : Path.Combine(docsDir, localId + ".md");
        var shadow = Path.Combine(shadowDir, localId + ".md");
        plan.Shadows.TryAdd(localId, new SyncDoc
            {
                LocalId = localId,
                ExternalId = legacy.ExternalId,
                Fields = repo?.Fields ?? external?.Fields ?? legacy.Fields,
                Body = $"<!-- migration local-id: {localId}; external-id: {legacy.ExternalId ?? "(none)"}; reason: {reason} -->\n"
                    + $"<<<<<<< repo\n{repo?.Body ?? "(canonical file unavailable)"}\n=======\n{external?.Body ?? "(external candidate unavailable)"}\n>>>>>>> external",
                SourcePath = shadow,
            });
        output.WriteLine($"             migration {localId} {reason}: {Path.GetFullPath(canonical)} -> {Path.GetFullPath(shadow)}");
    }
}
