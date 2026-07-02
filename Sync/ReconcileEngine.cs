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
    public static ReconcileResult Reconcile(
        SyncDoc? baseDoc, SyncDoc? repo, SyncDoc? external, Func<string, string>? bodyNormalizer = null)
    {
        var norm = bodyNormalizer ?? (static s => s);

        if (repo == null && external == null)
            return Simple(LocalIdOf(baseDoc, repo, external), ReconcileAction.None);

        if (baseDoc == null)
            return ReconcileNew(repo, external);

        if (repo == null || external == null)
            return DeleteOne(baseDoc, repo, external, norm);

        return ReconcileExisting(baseDoc, repo, external, norm);
    }

    /// <summary>Nothing in base, present on at least one side: create on the missing side, or — new
    /// on both at once — treat any divergence as a conflict against an empty synthetic base.</summary>
    private static ReconcileResult ReconcileNew(SyncDoc? repo, SyncDoc? external) =>
        external == null ? CreateToExternal(repo!)
        : repo == null ? CreateToRepo(external)
        : MergeBoth(SyntheticBase(repo, external), repo, external);

    /// <summary>Present in base, gone on exactly one side now (slice brief §1). If the surviving side
    /// is unchanged since base, the deletion is intentional and propagates to the other side. If the
    /// surviving side was edited since base, this is a delete/modify CONFLICT: the edit wins (never a
    /// silent clobber, Decision 025 §3) — resurrect the deleted side with the surviving edits, advance
    /// base to the survivor, and report a conflict rather than deleting.</summary>
    private static ReconcileResult DeleteOne(SyncDoc baseDoc, SyncDoc? repo, SyncDoc? external, Func<string, string> norm)
    {
        var externalId = baseDoc.ExternalId ?? repo?.ExternalId ?? external?.ExternalId;

        if (repo == null)
        {
            // Repo side deleted. External unchanged -> propagate the delete; external edited -> conflict.
            if (Equal(baseDoc, external!, norm))
                return new ReconcileResult
                {
                    LocalId = baseDoc.LocalId,
                    Action = ReconcileAction.Delete,
                    ExternalDelete = externalId,
                };

            // Resurrect the repo file from the surviving external edits.
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
        if (Equal(baseDoc, repo, norm))
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
        };
    }

    /// <summary>Both sides present: propagate a one-sided change, else 3-way merge.</summary>
    private static ReconcileResult ReconcileExisting(SyncDoc baseDoc, SyncDoc repo, SyncDoc external, Func<string, string> norm)
    {
        var repoChanged = !Equal(baseDoc, repo, norm);
        var extChanged = !Equal(baseDoc, external, norm);
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
            };

        if (!repoChanged && extChanged)
        {
            var toRepo = WithSourcePath(external, repo.SourcePath);
            return new ReconcileResult
            {
                LocalId = repo.LocalId,
                Action = ReconcileAction.WriteToRepo,
                RepoWrite = toRepo,
                NewBase = WithExternalId(toRepo, externalId),
            };
        }

        return MergeBoth(baseDoc, repo, external);
    }

    private static ReconcileResult MergeBoth(SyncDoc baseDoc, SyncDoc repo, SyncDoc external)
    {
        var fields = FieldMerge.Merge(baseDoc.Fields, repo.Fields, external.Fields);
        var body = ThreeWayTextMerge.Merge(baseDoc.Body, repo.Body, external.Body);
        var conflicted = fields.Conflicted || body.Conflicted;
        var externalId = baseDoc.ExternalId ?? external.ExternalId ?? repo.ExternalId;

        var merged = new SyncDoc
        {
            LocalId = repo.LocalId,
            ExternalId = externalId,
            Fields = fields.Fields,
            Body = body.Text,
            SourcePath = repo.SourcePath,
        };

        return new ReconcileResult
        {
            LocalId = repo.LocalId,
            Action = conflicted ? ReconcileAction.Conflict : ReconcileAction.Merged,
            RepoWrite = merged,
            ExternalWrite = merged,
            NewBase = merged,
        };
    }

    private static ReconcileResult CreateToExternal(SyncDoc repo) => new()
    {
        LocalId = repo.LocalId,
        Action = ReconcileAction.Create,
        ExternalWrite = repo,
        NewBase = repo,
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

    private static bool Equal(SyncDoc a, SyncDoc b, Func<string, string> norm) =>
        norm(a.Body.Replace("\r\n", "\n")) == norm(b.Body.Replace("\r\n", "\n"))
        && a.Fields.Count == b.Fields.Count
        && a.Fields.Zip(b.Fields).All(p => p.First.Key == p.Second.Key && p.First.Value == p.Second.Value);

    private static string LocalIdOf(SyncDoc? a, SyncDoc? b, SyncDoc? c) =>
        a?.LocalId ?? b?.LocalId ?? c?.LocalId ?? "";
}
