namespace DynaDocs.Sync;

using DynaDocs.Models;

/// <summary>
/// The Notion-agnostic reconcile core (Decision 025 §3). Given the per-object base snapshot, the
/// current repo doc, and the current external doc, it decides one <see cref="ReconcileResult"/>:
/// one-sided change propagates; two-sided change 3-way-merges fields key-by-key and the body as
/// text; a true overlap picks a deterministic winner (repo) and records a visible conflict;
/// creates and deletes follow from presence in base vs. now. Pure: it touches no I/O, so it is
/// fully unit-testable and the same logic drives any adapter.
/// </summary>
public static class ReconcileEngine
{
    /// <param name="bodyNormalizer">Maps a body to the form the external view echoes back for it (e.g.
    /// Notion's block round-trip drops blank lines). Bodies are compared modulo this map so a
    /// normalization-only difference is not mistaken for an edit — keeping an untouched doc idempotent
    /// across ticks (slice brief §4). Defaults to identity for views that store bodies verbatim.</param>
    /// <param name="fieldNormalizer">Maps a doc to the fields the external view echoes back for it (e.g.
    /// Notion drops an unresolvable relation, which reads back empty). Fields are compared modulo this map
    /// so adapter-lossiness is not mistaken for a real external edit — which would silently blank the repo
    /// value (slice brief §1). Defaults to identity for views that round-trip fields verbatim.</param>
    public static ReconcileResult Reconcile(
        SyncDoc? baseDoc, SyncDoc? repo, SyncDoc? external,
        Func<string, string>? bodyNormalizer = null, Func<SyncDoc, SyncDoc>? fieldNormalizer = null)
    {
        var norm = bodyNormalizer ?? (static s => s);
        var fieldNorm = fieldNormalizer ?? (static d => d);

        if (repo == null && external == null)
            return Simple(LocalIdOf(baseDoc, repo, external), ReconcileAction.None);

        if (baseDoc == null)
            return ReconcileNew(repo, external, fieldNorm);

        if (repo == null || external == null)
            return DeleteOne(baseDoc, repo, external, norm, fieldNorm);

        return ReconcileExisting(baseDoc, repo, external, norm, fieldNorm);
    }

    /// <summary>Nothing in base, present on at least one side: create on the missing side, or — new
    /// on both at once — treat any divergence as a conflict against an empty synthetic base.</summary>
    private static ReconcileResult ReconcileNew(SyncDoc? repo, SyncDoc? external, Func<SyncDoc, SyncDoc> fieldNorm) =>
        external == null ? CreateToExternal(repo!, fieldNorm)
        : repo == null ? CreateToRepo(external)
        : MergeBoth(SyntheticBase(repo, external), repo, external, fieldNorm);

    /// <summary>Present in base, gone on exactly one side now (slice brief §1). If the surviving side
    /// is unchanged since base, the deletion is intentional and propagates to the other side. If the
    /// surviving side was edited since base, this is a delete/modify CONFLICT: the edit wins (never a
    /// silent clobber, Decision 025 §3) — resurrect the deleted side with the surviving edits, advance
    /// base to the survivor, and report a conflict rather than deleting.</summary>
    private static ReconcileResult DeleteOne(SyncDoc baseDoc, SyncDoc? repo, SyncDoc? external, Func<string, string> norm, Func<SyncDoc, SyncDoc> fieldNorm)
    {
        var externalId = baseDoc.ExternalId ?? repo?.ExternalId ?? external?.ExternalId;

        if (repo == null)
        {
            // Repo side deleted. External unchanged -> propagate the delete; external edited -> conflict.
            if (Equal(baseDoc, external!, norm, fieldNorm))
                return new ReconcileResult
                {
                    LocalId = baseDoc.LocalId,
                    Action = ReconcileAction.Delete,
                    ExternalDelete = externalId,
                };

            // Resurrect the repo file from the surviving external edits — an external-driven write, not
            // repo activity.
            var survivor = WithExternalId(external!, externalId);
            return new ReconcileResult
            {
                LocalId = baseDoc.LocalId,
                Action = ReconcileAction.Conflict,
                RepoWrite = survivor,
                NewBase = survivor,
            };
        }

        // External side deleted. Repo unchanged -> propagate the delete; repo edited -> conflict.
        if (Equal(baseDoc, repo, norm, fieldNorm))
            return new ReconcileResult
            {
                LocalId = baseDoc.LocalId,
                Action = ReconcileAction.Delete,
                RepoDelete = repo.SourcePath,
            };

        // Re-create the external page from the surviving repo edits: no external id so a fresh page is
        // created (the old one was deleted); base advances once the new id is assigned.
        var resurrected = WithExternalId(repo, null);
        return new ReconcileResult
        {
            LocalId = baseDoc.LocalId,
            Action = ReconcileAction.Conflict,
            ExternalWrite = resurrected,
            NewBase = resurrected,
            RepoChanged = true,
        };
    }

    /// <summary>Both sides present: propagate a one-sided change, else 3-way merge.</summary>
    private static ReconcileResult ReconcileExisting(SyncDoc baseDoc, SyncDoc repo, SyncDoc external, Func<string, string> norm, Func<SyncDoc, SyncDoc> fieldNorm)
    {
        var repoChanged = !Equal(baseDoc, repo, norm, fieldNorm);
        var extChanged = !Equal(baseDoc, external, norm, fieldNorm);
        var externalId = baseDoc.ExternalId ?? external.ExternalId;

        if (!repoChanged && !extChanged)
            return Simple(repo.LocalId, ReconcileAction.None);

        if (repoChanged && !extChanged)
            return new ReconcileResult
            {
                LocalId = repo.LocalId,
                Action = ReconcileAction.PushToExternal,
                ExternalWrite = WithExternalId(repo, externalId),
                NewBase = WithExternalId(repo, externalId),
                RepoChanged = true,
            };

        if (!repoChanged && extChanged)
        {
            var toRepo = OverlayAdapterInvisibleFields(WithSourcePath(external, repo.SourcePath), repo, fieldNorm);
            return new ReconcileResult
            {
                LocalId = repo.LocalId,
                Action = ReconcileAction.WriteToRepo,
                RepoWrite = toRepo,
                NewBase = WithExternalId(toRepo, externalId),
            };
        }

        return MergeBoth(baseDoc, repo, external, fieldNorm);
    }

    private static ReconcileResult MergeBoth(SyncDoc baseDoc, SyncDoc repo, SyncDoc external, Func<SyncDoc, SyncDoc> fieldNorm)
    {
        var fields = FieldMerge.Merge(baseDoc.Fields, repo.Fields, external.Fields);
        var body = ThreeWayTextMerge.Merge(baseDoc.Body, repo.Body, external.Body);
        var conflicted = fields.Conflicted || body.Conflicted;
        var externalId = baseDoc.ExternalId ?? external.ExternalId ?? repo.ExternalId;

        var merged = OverlayAdapterInvisibleFields(new SyncDoc
        {
            LocalId = repo.LocalId,
            ExternalId = externalId,
            Fields = fields.Fields,
            Body = body.Text,
            SourcePath = repo.SourcePath,
        }, repo, fieldNorm);

        return new ReconcileResult
        {
            LocalId = repo.LocalId,
            Action = conflicted ? ReconcileAction.Conflict : ReconcileAction.Merged,
            RepoWrite = merged,
            ExternalWrite = merged,
            NewBase = merged,
            RepoChanged = true,
        };
    }

    /// <summary>
    /// Overlay the REPO's own values for adapter-invisible fields onto a doc built from the external side.
    /// An adapter-invisible field is one the field normalizer DROPS for the repo doc — a value the external
    /// view cannot round-trip (a relation whose local id it cannot resolve, or an out-of-schema/local-only
    /// frontmatter key), so it reads back blank or absent. A RepoWrite taken from the external (or merged)
    /// doc would therefore silently blank it (slice brief §1). Restoring the repo's value for exactly these
    /// keys keeps them intact, while every representable field still takes the external/merged value — so a
    /// genuine external edit is honored and only the fields the adapter cannot represent are protected. A
    /// field the repo genuinely deleted is absent from the repo doc, so it is not in this set and is never
    /// resurrected. With the default identity normalizer this set is empty and the input passes through.
    /// </summary>
    private static SyncDoc OverlayAdapterInvisibleFields(SyncDoc written, SyncDoc repo, Func<SyncDoc, SyncDoc> fieldNorm)
    {
        var visible = new HashSet<string>(fieldNorm(repo).Fields.Select(f => f.Key), StringComparer.OrdinalIgnoreCase);
        var invisible = repo.Fields.Where(f => !visible.Contains(f.Key)).ToList();
        if (invisible.Count == 0)
            return written;

        var overlaid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fields = new List<SyncField>();
        foreach (var f in written.Fields)
        {
            var repoField = invisible.FirstOrDefault(r => string.Equals(r.Key, f.Key, StringComparison.OrdinalIgnoreCase));
            fields.Add(repoField ?? f);
            if (repoField != null)
                overlaid.Add(f.Key);
        }
        // Adapter-invisible fields the external side dropped entirely (not merely blanked) are re-appended.
        foreach (var f in invisible)
            if (!overlaid.Contains(f.Key))
                fields.Add(f);

        return new SyncDoc
        {
            LocalId = written.LocalId,
            ExternalId = written.ExternalId,
            Fields = fields,
            Body = written.Body,
            SourcePath = written.SourcePath,
        };
    }

    /// <summary>Create the object on the external side. The push carries the full repo doc, but the base
    /// records only what the external view can actually round-trip (<paramref name="fieldNorm"/>): a field
    /// the external drops at create time — an as-yet-unresolvable relation such as a self-referential
    /// <c>blocked-by</c> whose target has not been synced — was never externalized, so recording it in the
    /// base would make the next tick misread its absence in the external as a deletion and blank the repo
    /// value. Normalizing here keeps the base consistent with external state, so once the target is synced
    /// the relation is correctly detected as a repo-side addition and pushed (DR 029 §5).</summary>
    private static ReconcileResult CreateToExternal(SyncDoc repo, Func<SyncDoc, SyncDoc> fieldNorm) => new()
    {
        LocalId = repo.LocalId,
        Action = ReconcileAction.Create,
        ExternalWrite = repo,
        NewBase = fieldNorm(repo),
        RepoChanged = true,
    };

    private static ReconcileResult CreateToRepo(SyncDoc external) => new()
    {
        LocalId = external.LocalId,
        Action = ReconcileAction.Create,
        RepoWrite = external,
        NewBase = external,
    };

    private static ReconcileResult Simple(string localId, ReconcileAction action) =>
        new() { LocalId = localId, Action = action };

    /// <summary>An empty base for the "new on both sides simultaneously" edge — diff each side
    /// against nothing so any divergence is treated as a conflict rather than silent loss.</summary>
    private static SyncDoc SyntheticBase(SyncDoc repo, SyncDoc external) => new()
    {
        LocalId = repo.LocalId,
        Fields = [],
        Body = "",
        SourcePath = repo.SourcePath,
    };

    private static SyncDoc WithExternalId(SyncDoc doc, string? externalId) => new()
    {
        LocalId = doc.LocalId,
        ExternalId = externalId,
        Fields = doc.Fields,
        Body = doc.Body,
        SourcePath = doc.SourcePath,
    };

    private static SyncDoc WithSourcePath(SyncDoc doc, string sourcePath) => new()
    {
        LocalId = doc.LocalId,
        ExternalId = doc.ExternalId,
        Fields = doc.Fields,
        Body = doc.Body,
        SourcePath = sourcePath,
    };

    private static bool Equal(SyncDoc a, SyncDoc b, Func<string, string> norm, Func<SyncDoc, SyncDoc> fieldNorm)
    {
        if (norm(a.Body.Replace("\r\n", "\n")) != norm(b.Body.Replace("\r\n", "\n")))
            return false;
        var fa = fieldNorm(a).Fields;
        var fb = fieldNorm(b).Fields;
        return fa.Count == fb.Count
            && fa.Zip(fb).All(p => p.First.Key == p.Second.Key && p.First.Value == p.Second.Value);
    }

    private static string LocalIdOf(SyncDoc? a, SyncDoc? b, SyncDoc? c) =>
        a?.LocalId ?? b?.LocalId ?? c?.LocalId ?? "";
}
