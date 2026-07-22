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
    /// <param name="repoOwnedStructure">When true (DR 033 §2 — the docs mirror), a page absent from the
    /// external read while its repo doc is present is re-created from the repo, never treated as a deletion:
    /// a present repo doc's page is never archived and its file is never deleted. Default false keeps the
    /// spine's bidirectional delete/modify semantics.</param>
    /// <param name="staleConverterEcho">Whether the external body is the PREVIOUS converter version's degraded
    /// projection of the base body — a one-time migration artifact, not a board edit (ns-7). When it returns true
    /// the engine treats the body as unchanged and force-pushes the repo body to upgrade the board. Default: never.</param>
    /// <param name="representableScalarKeys">The adapter's schema-mapped scalar keys (issue 0299): keys it can
    /// round-trip regardless of value, so the overlay must NOT restore the repo's value over the external one even
    /// when the field normalizer drops a false/empty. Null/empty keeps the docs-mirror semantics (every field
    /// adapter-invisible), so a plain page's frontmatter is still preserved on the repo.</param>
    public static ReconcileResult Reconcile(
        SyncDoc? baseDoc, SyncDoc? repo, SyncDoc? external,
        Func<string, string>? bodyNormalizer = null, Func<SyncDoc, SyncDoc>? fieldNormalizer = null,
        bool repoOwnedStructure = false, Func<string, string, bool>? staleConverterEcho = null,
        IReadOnlySet<string>? representableScalarKeys = null)
    {
        var norm = bodyNormalizer ?? (static s => s);
        var fieldNorm = fieldNormalizer ?? (static d => d);
        var representable = NonNull(representableScalarKeys);

        if (repo == null && external == null)
            return BothGone(baseDoc);

        if (baseDoc == null)
            return ReconcileNew(repo, external, fieldNorm, representable);

        if (repo == null || external == null)
            return DeleteOne(baseDoc, repo, external, norm, fieldNorm, repoOwnedStructure);

        return ReconcileExisting(baseDoc, repo, external, norm, fieldNorm, staleConverterEcho, representable);
    }

    private static readonly IReadOnlySet<string> EmptyKeys = new HashSet<string>();

    private static IReadOnlySet<string> NonNull(IReadOnlySet<string>? keys) => keys ?? EmptyKeys;

    /// <summary>Gone from both sides. A lingering base entry is retired (slice brief §2): left as None the
    /// stale entry (archived-page ExternalId + last-activity) would live forever — a git-restored file equal to
    /// it hits DeleteOne's unchanged branch and is silently deleted, its id leaks into children's relation maps,
    /// and the snapshot grows unbounded. With no base entry there is nothing to retire, so it stays None.</summary>
    private static ReconcileResult BothGone(SyncDoc? baseDoc) =>
        Simple(baseDoc?.LocalId ?? "", baseDoc == null ? ReconcileAction.None : ReconcileAction.Retire);

    /// <summary>Nothing in base, present on at least one side: create on the missing side, or — new
    /// on both at once — treat any divergence as a conflict against an empty synthetic base.</summary>
    private static ReconcileResult ReconcileNew(SyncDoc? repo, SyncDoc? external, Func<SyncDoc, SyncDoc> fieldNorm, IReadOnlySet<string> representable) =>
        external == null ? CreateToExternal(repo!, fieldNorm)
        : repo == null ? CreateToRepo(external)
        : MergeBoth(SyntheticBase(repo, external), repo, external, fieldNorm, representable);

    /// <summary>Present in base, gone on exactly one side now (slice brief §1). If the surviving side
    /// is unchanged since base, the deletion is intentional and propagates to the other side. If the
    /// surviving side was edited since base, this is a delete/modify CONFLICT: the edit wins (never a
    /// silent clobber, Decision 025 §3) — resurrect the deleted side with the surviving edits, advance
    /// base to the survivor, and report a conflict rather than deleting.</summary>
    private static ReconcileResult DeleteOne(SyncDoc baseDoc, SyncDoc? repo, SyncDoc? external, Func<string, string> norm, Func<SyncDoc, SyncDoc> fieldNorm, bool repoOwnedStructure)
    {
        var externalId = baseDoc.ExternalId ?? repo?.ExternalId ?? external?.ExternalId;

        if (repo == null)
        {
            // Repo side deleted. External unchanged -> propagate the delete; external edited -> conflict.
            if (EqualRawFields(baseDoc, external!, norm))
                return new ReconcileResult
                {
                    LocalId = baseDoc.LocalId,
                    Action = ReconcileAction.Delete,
                    ExternalDelete = externalId,
                };

            // Resurrect the repo file from the surviving external edits — an external-driven write, not repo
            // activity. Sanitize through the field normalizer first (finding 3): this is a FOURTH overlay-ingestion
            // path the wave-7 resolvable-subset fix missed (it covered union/pending/pass-through), so a relation the
            // board points at an unmapped/archived page — rendered as a raw Notion page id the write map can never
            // resolve — must be reduced to its resolvable subset, never planted verbatim into frontmatter or the base.
            var survivor = fieldNorm(WithExternalId(external!, externalId));
            return new ReconcileResult
            {
                LocalId = baseDoc.LocalId,
                Action = ReconcileAction.Conflict,
                RepoWrite = survivor,
                NewBase = survivor,
            };
        }

        return ExternalDeleted(baseDoc, repo, norm, fieldNorm, repoOwnedStructure);
    }

    /// <summary>External side deleted. Repo unchanged -> propagate the delete; repo edited -> conflict. "Unchanged"
    /// is the NORMALIZED compare (Equal), so a permanently-local, out-of-schema frontmatter key the adapter never
    /// pushes — area/type on a sprint-task, id/found-by/date on an issue — does not, by its mere presence, block
    /// an intentional board-archive from deleting the file (a raw compare would, since the base is recorded
    /// normalized and so never holds those keys). It is guarded by <see cref="HasUnpushedRelation"/> so no un-pushed
    /// RELATION entry is ever lost: a partially-resolvable relation with a pending entry, or an all-entries-
    /// unresolvable relation the normalizer drops whole (absent from base), counts as CHANGED and resurrects the
    /// page rather than silently deleting the file and losing the entry forever (finding 1b). A genuine unchanged
    /// file — no un-pushed relation content — matches base normalized and still deletes.</summary>
    private static ReconcileResult ExternalDeleted(SyncDoc baseDoc, SyncDoc repo, Func<string, string> norm, Func<SyncDoc, SyncDoc> fieldNorm, bool repoOwnedStructure)
    {
        // Structure is repo-owned (DR 033 §2): a page missing from the external read while its repo doc is
        // still present is never a deletion — it is Notion listing eventual-consistency after a bulk create, or
        // a colleague's stray archive. Re-create it from the repo (a fresh page, id assigned on Apply) rather
        // than deleting the repo file or archiving the page. This is the hard invariant that a present repo
        // doc's page is NEVER archived; archive fires only when the repo doc is genuinely gone (the repo==null
        // branch above). A spurious duplicate from an eventual-consistency miss is recoverable; a mass repo
        // deletion is not — the same data-preserving tradeoff the spine's fresh-mint reset already accepts.
        if (repoOwnedStructure)
            return CreateToExternal(repo, fieldNorm);

        if (Equal(baseDoc, repo, norm, fieldNorm) && !HasUnpushedRelation(baseDoc, repo, fieldNorm))
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
            NewBase = fieldNorm(resurrected),
            RepoChanged = true,
        };
    }

    /// <summary>Both sides present: propagate a one-sided change, else 3-way merge.</summary>
    private static ReconcileResult ReconcileExisting(SyncDoc baseDoc, SyncDoc repo, SyncDoc external, Func<string, string> norm, Func<SyncDoc, SyncDoc> fieldNorm, Func<string, string, bool>? staleEcho, IReadOnlySet<string> representable)
    {
        // Converter-migration shim (ns-7 blocker): a board synced before the converter upgrade holds the OLD
        // converter's degraded projection of the base body. Read back and normalized under the new converter it
        // diverges, which the checks below would misread as an external edit and use to overwrite the canonical file.
        // ApplyMigrationShim detects that, swaps the base body in (so it neither trips change-detection nor pollutes
        // the 3-way merge), and flags a forced repo→board push so the board re-renders in one tick — after which the
        // echo matches and this stops firing.
        var (shimmed, upgradeBody) = ApplyMigrationShim(external, baseDoc, norm, staleEcho);
        external = shimmed;

        // A parent-type re-provision re-creates its pages with NEW ids (wave 7), so a child's relation on the board
        // still points at the OLD, now-abandoned parent page. That renders as a raw page id the field normalizer
        // drops, making the external's resolvable subset for the key EMPTY — yet the repo value still resolves (the
        // write map holds the new page id) and equals the base's recorded value. That is a STALE board echo, not a
        // clear: the repo wins and the relation must be RE-PUSHED so the board points at the new page (finding 1).
        // De-stale the external view here so neither change-detection nor the 3-way merge misreads the stale echo as
        // a board-side clear and collapses the still-valid repo relation to "" (the finding-1 data loss).
        //
        // SCOPE BOUNDARY (wave 8, item 1): this preserves the child's repo relation DATA and re-pushes it so the
        // board points at the re-minted parent PAGE — but it does NOT re-point the child relation PROPERTY's SCHEMA,
        // still pinned to the parent's original data_source_id at child-create time (NotionProvisioner.RelationSchema).
        // On a real parent-only re-mint the re-push writes new-parent page ids into a relation whose schema targets
        // the deleted data source; live Notion MAY reject that, wedging this child's sync loudly mid-tick. That is
        // strictly non-destructive: an aborted tick advances no base and deletes no repo file, so worst case is a
        // loud wedge, never the pre-wave-8 silent clear. Full convergence needs schema re-pointing plus reverse-
        // relation/rollup re-synthesis, deferred to the retro-provisioning work pending live-Notion verification.
        var stale = StaleRelationKeys(baseDoc, repo, external, fieldNorm);
        var hasStale = stale.Count > 0;
        var externalView = hasStale ? DeStaleRelations(external, repo, stale, fieldNorm) : external;

        var repoChanged = !Equal(baseDoc, repo, norm, fieldNorm);
        var extChanged = !Equal(baseDoc, externalView, norm, fieldNorm);
        var externalId = baseDoc.ExternalId ?? external.ExternalId;

        if (extChanged)
            // A one-sided external change with no stale echo writes to the repo; anything else — two-sided, or an
            // external change alongside a stale relation to re-push — 3-way merges against the DE-STALED external so
            // the stale relation is preserved from the repo AND re-pushed with the merge, never read as a clear the
            // merge would archive on the board (finding 1, concurrent-edit variant).
            return !repoChanged && !hasStale
                ? WriteToRepoResult(external, repo, baseDoc, externalId, norm, fieldNorm, representable)
                : MergeBoth(baseDoc, repo, externalView, fieldNorm, representable);

        // repoChanged is a real repo edit; upgradeBody is only the board holding the old converter's projection of an
        // otherwise-unchanged body — both push the repo body to the board, but only the former is a repo-side edit.
        // A repo-side edit that emptied a scalar the base recorded must explicitly CLEAR it on the board (F5).
        // ClearedScalarKeys returns empty whenever base and repo agree (repoChanged false / stale re-push / body
        // upgrade), so it is safe to compute unconditionally on every push branch.
        if (repoChanged || upgradeBody)
            return PushToExternalResult(repo, externalId, fieldNorm, repoChanged,
                ClearedScalarKeys(baseDoc, repo, representable, externalId));

        // Neither side changed. A lingering stale echo means the board still points at the abandoned parent page —
        // re-push the repo relation to resolve it to the new page id (finding 1; folds in finding 4's no-converge
        // churn), not a repo-side edit (RepoChanged stays false). Otherwise a genuine no-op.
        return hasStale
            ? PushToExternalResult(repo, externalId, fieldNorm, repoChanged: false,
                ClearedScalarKeys(baseDoc, repo, representable, externalId))
            : Simple(repo.LocalId, ReconcileAction.None);
    }

    /// <summary>The converter-migration shim (ns-7 blocker). When the external body is the previous converter
    /// version's degraded echo of the base — it genuinely drifts under the new normalizer AND that drift is exactly
    /// the old converter's echo — swap the base body in (so change-detection and the merge see no body edit) and
    /// flag a forced repo→board push. A board already on the new converter normalizes equal to base, so nothing
    /// fires and there is no churn; a null predicate (adapters with no migration) is a no-op.</summary>
    private static (SyncDoc External, bool Upgrade) ApplyMigrationShim(SyncDoc external, SyncDoc baseDoc, Func<string, string> norm, Func<string, string, bool>? staleEcho)
    {
        var upgrade = staleEcho != null
            && !string.Equals(norm(external.Body.Replace("\r\n", "\n")), norm(baseDoc.Body.Replace("\r\n", "\n")), StringComparison.Ordinal)
            && staleEcho(external.Body, baseDoc.Body);
        return upgrade ? (WithBody(external, baseDoc.Body), true) : (external, false);
    }

    /// <summary>Push the repo doc to the external side, recording only the round-trippable subset in the base (a
    /// field the external drops — an as-yet-unresolvable relation — reads back absent, so an un-normalized base would
    /// misread that absence next tick as a deletion and blank the repo value, slice brief §1). <paramref name="repoChanged"/>
    /// is false only for a stale-echo re-push (finding 1): the repo already equals the base, so no activity bump.</summary>
    private static ReconcileResult PushToExternalResult(SyncDoc repo, string? externalId, Func<SyncDoc, SyncDoc> fieldNorm, bool repoChanged, IReadOnlyList<string> clearedKeys) => new()
    {
        LocalId = repo.LocalId,
        Action = ReconcileAction.PushToExternal,
        ExternalWrite = WithExternalId(repo, externalId),
        NewBase = fieldNorm(WithExternalId(repo, externalId)),
        RepoChanged = repoChanged,
        ClearedKeys = clearedKeys,
    };

    /// <summary>Scalar keys this push must explicitly CLEAR on the external side (issue 0299, F5): a representable
    /// scalar the base recorded NON-EMPTY that the repo now carries empty-or-absent, on an UPDATE only (a create
    /// omits blanks — "blank means unset"). Relations and out-of-schema keys are excluded (not representable
    /// scalars). Without this the update's PATCH omits the blank property, the board keeps the old value, and the
    /// non-empty echo re-imports it next tick — a silent revert of the local clear. Caveat for adapter authors:
    /// for a title-less doc the base records the adapter's synthesized title while the raw repo lacks the key, so
    /// the returned list can carry the title key — the Notion adapter filters it in BuildClears; any future
    /// adapter consuming ClearedKeys must do the same or it will emit a phantom title clear.</summary>
    private static IReadOnlyList<string> ClearedScalarKeys(SyncDoc baseDoc, SyncDoc pushed, IReadOnlySet<string> representable, string? externalId)
    {
        if (externalId == null)
            return [];
        var pushedMap = FirstWins(pushed.Fields);
        return FirstWins(baseDoc.Fields)
            .Where(kv => representable.Contains(kv.Key) && kv.Value.Length > 0
                && (!pushedMap.TryGetValue(kv.Key, out var v) || v.Length == 0))
            .Select(kv => kv.Key)
            .ToList();
    }

    /// <summary>Write a one-sided external change back to the repo, overlaying the repo's adapter-invisible fields
    /// so the external side's inability to round-trip them does not blank the file, and recording only the
    /// round-trippable subset in the base so those fields are not read as an external deletion next tick (§1).
    /// When the external change is FIELD-only — the bodies are equal modulo the adapter's body round-trip — the
    /// repo's own body is carried through unchanged, so a field edit never collateral-rewrites the file body into
    /// the external's normalized dialect (blank-line-stripped, marker-swapped); the external body is written only
    /// when it genuinely differs (issue 0299, body class).</summary>
    private static ReconcileResult WriteToRepoResult(SyncDoc external, SyncDoc repo, SyncDoc baseDoc, string? externalId, Func<string, string> norm, Func<SyncDoc, SyncDoc> fieldNorm, IReadOnlySet<string> representable)
    {
        var bodyUnchanged = norm(external.Body.Replace("\r\n", "\n")) == norm(repo.Body.Replace("\r\n", "\n"));
        var source = WithSourcePath(bodyUnchanged ? WithBody(external, repo.Body) : external, repo.SourcePath);
        var toRepo = OverlayAdapterInvisibleFields(source, repo, baseDoc, fieldNorm, representable);
        return new ReconcileResult
        {
            LocalId = repo.LocalId,
            Action = ReconcileAction.WriteToRepo,
            RepoWrite = toRepo,
            NewBase = fieldNorm(WithExternalId(toRepo, externalId)),
        };
    }

    /// <summary>Relation keys whose external echo is STALE rather than a board clear (slice brief finding 1): the
    /// repo still resolves the key to a value that EQUALS the base's recorded value, but the external's resolvable
    /// subset for it is EMPTY because the board points at an id the read cannot resolve — a re-provisioned parent's
    /// abandoned page, rendered as a raw page id the normalizer drops whole. Such a key must not be read as a clear:
    /// the repo wins and the relation is re-pushed. Excluded so the genuine cases still apply: a key the repo value
    /// itself CHANGED from base (a real repo edit — pushed normally); a key the base never recorded (the wave-7
    /// empty-echo, treated as absent); and a genuine board CLEAR, where the external echoes the key EMPTY ("") and so
    /// its resolvable subset is present (just empty) rather than dropping out.</summary>
    private static HashSet<string> StaleRelationKeys(SyncDoc baseDoc, SyncDoc repo, SyncDoc external, Func<SyncDoc, SyncDoc> fieldNorm)
    {
        var repoVisible = FirstWins(fieldNorm(repo).Fields);
        var externalVisible = FirstWins(fieldNorm(external).Fields);
        var baseRecorded = FirstWins(baseDoc.Fields);
        var externalRaw = FirstWins(external.Fields);

        var stale = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, repoValue) in repoVisible)
        {
            if (externalVisible.ContainsKey(key))
                continue; // external still resolves it (or echoes a genuine empty clear) — not a stale drop
            if (!baseRecorded.TryGetValue(key, out var baseValue) || baseValue != repoValue)
                continue; // base never recorded it, or the repo value itself changed — not the finding-1 shape
            if (!externalRaw.TryGetValue(key, out var rawValue) || rawValue.Length == 0)
                continue; // external genuinely lacks/cleared the key — a real absence, not a stale raw-id echo
            if (IsRelationKey(key, repo, fieldNorm))
                stale.Add(key);
        }
        return stale;
    }

    /// <summary>Replace each stale relation echo in the external doc with the repo's RESOLVABLE value (slice brief
    /// finding 1). The two are equal by construction (a stale key is one whose repo resolvable value equals the
    /// base), so this only swaps the board's unresolvable raw-id rendering for the resolvable local id: change-
    /// detection and the 3-way merge then see the key as unchanged from base and the overlay keeps it resolvable,
    /// never collapsing it to "". Every other field is untouched, so a genuine board edit to another field applies.</summary>
    private static SyncDoc DeStaleRelations(SyncDoc external, SyncDoc repo, HashSet<string> staleKeys, Func<SyncDoc, SyncDoc> fieldNorm)
    {
        var repoResolvable = FirstWins(fieldNorm(repo).Fields);
        return new SyncDoc
        {
            LocalId = external.LocalId,
            ExternalId = external.ExternalId,
            Fields = external.Fields
                .Select(f => staleKeys.Contains(f.Key) ? new SyncField { Key = f.Key, Value = repoResolvable[f.Key] } : f)
                .ToList(),
            Body = external.Body,
            SourcePath = external.SourcePath,
        };
    }

    private static ReconcileResult MergeBoth(SyncDoc baseDoc, SyncDoc repo, SyncDoc external, Func<SyncDoc, SyncDoc> fieldNorm, IReadOnlySet<string> representable)
    {
        // Filter the external's empty-relation echoes the base never recorded before the 3-way merge (finding 2):
        // an all-unresolvable relation reads back empty from real Notion, and FieldMerge would otherwise see it
        // as an external "" against a repo pending value and manufacture a phantom conflict every tick. A genuine
        // clear (base RECORDED the key, external now empty) is kept, so it still merges and applies.
        var externalFields = WithoutEmptyEchoesAbsentFrom(external.Fields, RecordedKeys(baseDoc));
        var fields = FieldMerge.Merge(baseDoc.Fields, repo.Fields, externalFields);
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
        }, repo, baseDoc, fieldNorm, representable);

        return new ReconcileResult
        {
            LocalId = repo.LocalId,
            Action = conflicted ? ReconcileAction.Conflict : ReconcileAction.Merged,
            RepoWrite = merged,
            ExternalWrite = merged,
            // The merged doc is pushed whole, but the base records only its round-trippable subset so an
            // adapter-invisible field is not misread as an external deletion next tick (slice brief §1).
            NewBase = fieldNorm(merged),
            RepoChanged = true,
            // A scalar the base recorded that the merge resolved to empty must be explicitly cleared on push (F5).
            ClearedKeys = ClearedScalarKeys(baseDoc, merged, representable, externalId),
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
    private static SyncDoc OverlayAdapterInvisibleFields(SyncDoc written, SyncDoc repo, SyncDoc baseDoc, Func<SyncDoc, SyncDoc> fieldNorm, IReadOnlySet<string> representable)
    {
        var visible = FirstWins(fieldNorm(repo).Fields);
        // What the base RECORDED per key (raw). A whole-field-invisible relation preserves only the repo entries
        // the base never recorded (un-pushed pending work); a recorded entry the board no longer shows was
        // cleared/retired there, so it is NOT resurrected — a genuine board clear of a since-retired target
        // applies rather than being swallowed (finding 4).
        var recordedEntries = FirstWins(baseDoc.Fields);

        // Whole-field invisible: a key the normalizer drops ENTIRELY — an out-of-schema/local-only frontmatter
        // key, or a relation that resolves to nothing — reads back absent, so the external doc would blank it.
        // A schema-mapped SCALAR (issue 0299, F1) is NEVER invisible even when the normalizer drops its false/empty
        // value: the adapter faithfully round-trips false/empty, so restoring the repo's value here would clobber a
        // genuine board edit (a checkbox checked, a date set) and loop forever. Excluding representable scalars means
        // the external value passes through for them; relations keep value-based visibility (representable excludes them).
        var invisible = FirstWins(repo.Fields.Where(f => !visible.ContainsKey(f.Key) && !representable.Contains(f.Key)));
        // The repo's un-pushed pending entries: a relation target the normalizer DROPS (not yet syncable) AND the
        // base never recorded. An entry the base DID record but the board no longer shows was cleared/retired
        // there, not un-pushed work, so it is excluded — it must not be re-appended to the file nor block the
        // doc's delete (finding 4), mirroring the whole-field-invisible branch's UnpushedEntries.
        var pending = PendingRelationEntries(repo, visible, recordedEntries);
        // A whole-field-invisible RELATION merges entry-granular rather than replacing: its raw entries are all
        // unresolvable, so a board edit lives only in the external value — replacing it wholesale would discard a
        // genuine board edit and later clobber it on push (finding 2). A non-relation local-only key still takes
        // the repo value wholesale (the external never holds it).
        var invisibleRelations = invisible.Keys
            .Where(k => IsRelationKey(k, repo, fieldNorm))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // The external field values reduced to entries that RESOLVE to a local id — the normalizer keeps only ids
        // in the write map (finding 3). EVERY relation value the overlay ingests is taken from THIS subset, never
        // the raw external value: RenderRelation's honest fallback for an unmapped/archived page is the raw Notion
        // page id, which the write map (keyed by local ids) can never resolve, so writing it would plant an
        // immortal raw-id entry that re-appears every tick and blocks the doc's delete forever. Presentation, not
        // a pending local edit. A non-relation field's normalized value equals its raw value, so this is a no-op
        // there; the divergent early-return that let a raw id pass through untouched in the pending and
        // pass-through branches (round-3 defect) is gone — the loop below sanitizes every ingestion path.
        var resolvableExternal = FirstWins(fieldNorm(written).Fields);

        // External relations the normalizer drops WHOLE — every entry unresolvable, i.e. a pure raw-page-id
        // fallback — that the repo does not also carry as an invisible key. Told apart from an out-of-schema
        // dropped key via IsRelationKey (reliable for dropped keys), so pass-through collapses such a field to
        // empty rather than planting the raw id (finding 3, pass-through case).
        var externalRelationKeys = written.Fields.Select(f => f.Key)
            .Where(k => !resolvableExternal.ContainsKey(k) && IsRelationKey(k, written, fieldNorm))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // F3 (issue 0299): a schema-default scalar echo — a false checkbox or empty scalar the normalizer drops —
        // the file never carried (absent from BOTH repo and base) must not be planted into frontmatter on a genuine
        // WriteToRepo/merge, or it seeds finding-1's explicit-false/empty clobber shape. Drop those keys from the
        // written doc. A key repo or base DID carry is kept (an explicit local false, or a recorded value that may
        // be a genuine board clear).
        var repoKeys = repo.Fields.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var schemaDefaultEchoes = written.Fields
            .Where(f => representable.Contains(f.Key) && !resolvableExternal.ContainsKey(f.Key)
                && !repoKeys.Contains(f.Key) && !recordedEntries.ContainsKey(f.Key))
            .Select(f => f.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var overlaid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fields = written.Fields
            .Where(f => !schemaDefaultEchoes.Contains(f.Key))
            .Select(f => OverlayOne(f, invisible, invisibleRelations, pending, resolvableExternal, recordedEntries, externalRelationKeys, overlaid))
            .ToList();
        AppendUnseen(fields, invisible, invisibleRelations, pending, recordedEntries, overlaid);

        return new SyncDoc
        {
            LocalId = written.LocalId,
            ExternalId = written.ExternalId,
            Fields = fields,
            Body = written.Body,
            SourcePath = written.SourcePath,
        };
    }

    /// <summary>Overlay one external field: a whole-field-invisible key takes the repo's value; a
    /// whole-field-invisible relation unions the external's RESOLVABLE entries with the repo's unresolvable ones;
    /// a partially-invisible relation takes the external's resolvable value with the repo's pending entries
    /// merged back; every other RELATION passes through as its resolvable subset (never a raw page id); every
    /// other field passes through unchanged. Records overlaid keys so the caller can re-append the ones the
    /// external dropped entirely.</summary>
    private static SyncField OverlayOne(
        SyncField written,
        IReadOnlyDictionary<string, string> invisible,
        HashSet<string> invisibleRelations,
        IReadOnlyDictionary<string, List<string>> pending,
        IReadOnlyDictionary<string, string> resolvableExternal,
        IReadOnlyDictionary<string, string> recordedEntries,
        HashSet<string> externalRelationKeys,
        HashSet<string> overlaid)
    {
        if (invisible.TryGetValue(written.Key, out var repoValue))
        {
            overlaid.Add(written.Key);
            // A whole-field-invisible relation UNIONs the board's RESOLVABLE (local-id) entries with the repo's
            // UN-PUSHED ones, so a board edit survives and a repo pending entry is never lost (finding 2), while
            // two things are excluded: a raw external page-id fallback (it lives only in written.Value, not the
            // normalized subset, finding 3) and a repo entry the base already recorded but the board no longer
            // shows — that entry was cleared/retired on the board, so a genuine clear applies (finding 4). A
            // non-relation local-only key takes the repo value wholesale.
            if (!invisibleRelations.Contains(written.Key))
                return new SyncField { Key = written.Key, Value = repoValue };
            resolvableExternal.TryGetValue(written.Key, out var resolvable);
            return new SyncField { Key = written.Key, Value = MergeEntries(resolvable ?? "", UnpushedEntries(repoValue, written.Key, recordedEntries)) };
        }
        if (pending.TryGetValue(written.Key, out var entries))
        {
            overlaid.Add(written.Key);
            // Merge the repo's pending entries onto the external's RESOLVABLE subset, NOT its raw value: a raw
            // external page-id fallback lives only in written.Value, and unioning it here (the round-3 defect,
            // production-reachable when one target of a multi-value relation retires while the board still
            // references its archived page) would plant an immortal raw-id entry that re-unions every tick and
            // blocks the doc's delete forever (finding 3). `pending` already excludes base-recorded entries, so a
            // retired-but-recorded target is not re-appended either (finding 4).
            resolvableExternal.TryGetValue(written.Key, out var resolvable);
            return new SyncField { Key = written.Key, Value = MergeEntries(resolvable ?? "", entries) };
        }
        // Pass-through. A field the normalizer keeps takes its NORMALIZED value: identical to the raw value for a
        // non-relation field, but the RESOLVABLE (local-id) subset for a relation, so a board reference to an
        // unmapped/archived page — rendered as a raw Notion page id — is stripped before it reaches frontmatter
        // (finding 3, pass-through case: repo relation fully resolvable, the board edits another field while
        // referencing an archived page). A relation the normalizer drops WHOLE (all entries raw) collapses to
        // empty for the same reason; any other dropped key (out-of-schema) is passed through untouched.
        if (resolvableExternal.TryGetValue(written.Key, out var normalized))
            return new SyncField { Key = written.Key, Value = normalized };
        if (externalRelationKeys.Contains(written.Key))
            return new SyncField { Key = written.Key, Value = "" };
        return written;
    }

    /// <summary>The repo's entries for a whole-field-invisible relation that the base never recorded — its
    /// un-pushed, still-pending local edits. Entries the base DID record are omitted: a recorded entry the board
    /// no longer shows was cleared or retired there, so it must not be resurrected (finding 4).</summary>
    private static List<string> UnpushedEntries(string repoValue, string key, IReadOnlyDictionary<string, string> recordedEntries)
    {
        var recorded = recordedEntries.TryGetValue(key, out var value) ? SplitEntries(value) : [];
        return SplitEntries(repoValue).Where(e => !recorded.Contains(e)).ToList();
    }

    /// <summary>Re-append invisible/pending keys the external side dropped entirely (not merely blanked): a
    /// whole-field-invisible local-only key with its repo value; a whole-field-invisible RELATION with only its
    /// un-pushed entries (a recorded entry the board dropped was cleared there, finding 4); a partially-invisible
    /// relation with just its pending entries.</summary>
    private static void AppendUnseen(
        List<SyncField> fields,
        IReadOnlyDictionary<string, string> invisible,
        HashSet<string> invisibleRelations,
        IReadOnlyDictionary<string, List<string>> pending,
        IReadOnlyDictionary<string, string> recordedEntries,
        HashSet<string> overlaid)
    {
        foreach (var (key, value) in invisible)
            if (!overlaid.Contains(key))
            {
                if (!invisibleRelations.Contains(key))
                    fields.Add(new SyncField { Key = key, Value = value });
                else
                {
                    var unpushed = UnpushedEntries(value, key, recordedEntries);
                    if (unpushed.Count > 0)
                        fields.Add(new SyncField { Key = key, Value = string.Join(", ", unpushed) });
                }
            }
        foreach (var (key, entries) in pending)
            if (!overlaid.Contains(key))
                fields.Add(new SyncField { Key = key, Value = string.Join(", ", entries) });
    }

    /// <summary>Whether the repo doc carries relation content it has not yet been able to push — the guard on
    /// delete-propagation for the external-deleted branch (finding 1b, entry-granular). Two shapes must count as
    /// un-pushed work: a partially-resolvable relation with a pending entry (<see cref="PendingRelationEntries"/>),
    /// and an all-entries-unresolvable relation the normalizer drops WHOLE — invisible to PendingRelationEntries,
    /// which inspects only keys that survive normalization — with a non-empty raw value absent from base. A
    /// permanently-local, out-of-schema key (area/type/id/...) is never pushable, so it is not un-pushed work and
    /// must not block the delete; it is told apart from a dropped relation via <see cref="IsRelationKey"/>.</summary>
    private static bool HasUnpushedRelation(SyncDoc baseDoc, SyncDoc repo, Func<SyncDoc, SyncDoc> fieldNorm)
    {
        var visible = FirstWins(fieldNorm(repo).Fields);
        var baseEntriesByKey = FirstWins(baseDoc.Fields);
        // A partially-resolvable relation with a pending entry the base never recorded is un-pushed work and must
        // block the delete (finding 1b). An entry the base DID record but the normalizer now drops is a retired
        // target, not pending work, so PendingRelationEntries excludes it — it no longer blocks the delete
        // (finding 4), coherent with the overlay re-append so a partial-shape board archive of a doc whose one
        // retired blocker was already recorded propagates as a genuine delete instead of resurrecting forever.
        if (PendingRelationEntries(repo, visible, baseEntriesByKey).Count > 0)
            return true;

        // Entry-granular against the BASE, not merely key-granular (finding 3): compare the repo's RAW entries
        // for a whole-field-invisible relation against the entries the base RECORDED for that key. Any repo entry
        // absent from the base is un-pushed. The old key-presence check was defeated when the base already held
        // the key under a now-retired (unresolvable) value — 'blocked-by: a' pushed, repo 'a, b' with b pending,
        // a's target retired so the whole key drops from both normalized sides — letting the delete lose b. An
        // empty repo value and a permanently-local, out-of-schema key (told apart via IsRelationKey) are not
        // un-pushed work and never block the delete.
        // First-wins on a duplicate repo key (review R2-2): consider only the FIRST occurrence of each key, exactly
        // as every other consumer (visible/FirstWins, FieldMerge, ToProperties, ParseFields, UpsertField, GetField)
        // does. A NON-first duplicate occurrence's entries are invisible to all of them — no reader will ever push
        // or preserve them — so counting them as un-pushed work would let a duplicate-key doc block a delete on
        // entries no one else can see.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return repo.Fields.Where(f => seen.Add(f.Key)).Any(f =>
            !string.IsNullOrEmpty(f.Value)
            && !visible.ContainsKey(f.Key)
            && IsRelationKey(f.Key, repo, fieldNorm)
            && SplitEntries(f.Value).Any(e => !EntryRecordedInBase(baseEntriesByKey, f.Key, e)));
    }

    /// <summary>Whether the base recorded <paramref name="entry"/> for <paramref name="key"/> — i.e. that entry
    /// was already pushed. A key the base never held, or an entry absent from its recorded value, is un-pushed.</summary>
    private static bool EntryRecordedInBase(IReadOnlyDictionary<string, string> baseEntriesByKey, string key, string entry) =>
        baseEntriesByKey.TryGetValue(key, out var value) && SplitEntries(value).Contains(entry);

    /// <summary>Whether a whole-field-invisible key is a schema RELATION (its entries are all currently
    /// unresolvable) rather than a permanently-local, out-of-schema key — read straight from the same normalizer
    /// seam the overlay trusts, inventing no parallel schema. A relation keeps its key under an EMPTY value (a
    /// valid clear normalizes to "", as <c>NotionSyncAdapter.ResolveRelationSubset</c> does), whereas an
    /// out-of-schema or computed key is dropped for every value; so probing the normalizer with an empty value
    /// reveals the relation-typed fact without the engine ever learning the schema.</summary>
    private static bool IsRelationKey(string key, SyncDoc doc, Func<SyncDoc, SyncDoc> fieldNorm)
    {
        var probe = new SyncDoc
        {
            LocalId = doc.LocalId,
            ExternalId = doc.ExternalId,
            Fields = [new SyncField { Key = key, Value = "" }],
            Body = doc.Body,
            SourcePath = doc.SourcePath,
        };
        return fieldNorm(probe).Fields.Any(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The repo's per-key entries a partially-lossy field normalizer DROPPED that are genuinely
    /// un-pushed — comma-joined entries present in the repo's raw value, absent from its normalized value (a
    /// relation target not yet syncable), AND never recorded by the base. An entry the base DID record is a
    /// retired/cleared target, not pending work, so it is excluded (finding 4): the overlay must not re-append it
    /// and it must not block the doc's delete, mirroring the whole-field-invisible branch's <see cref="UnpushedEntries"/>.
    /// A key the normalizer drops entirely is not here (it is whole-field invisible), and a field with no
    /// remaining dropped entry is omitted (finding 1a).</summary>
    private static Dictionary<string, List<string>> PendingRelationEntries(SyncDoc repo, Dictionary<string, string> visible, IReadOnlyDictionary<string, string> recordedEntries)
    {
        var pending = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in repo.Fields)
        {
            // First-wins on a duplicate repo key (review R2-2): every other consumer — visible/FirstWins,
            // FieldMerge.ToMap, NotionPropertyMapper.ToProperties, FrontmatterParser.ParseFields, UpsertField,
            // SyncDoc.GetField — resolves the FIRST occurrence, and `visible` is itself first-wins. Pairing a
            // LATER occurrence's raw value with the first occurrence's kept value would union phantom pending
            // entries no reader will ever push or preserve, so only the first occurrence of each key is considered.
            if (!seen.Add(f.Key))
                continue;
            if (!visible.TryGetValue(f.Key, out var keptValue))
                continue;
            var kept = SplitEntries(keptValue);
            var dropped = SplitEntries(f.Value)
                .Where(e => !kept.Contains(e) && !EntryRecordedInBase(recordedEntries, f.Key, e))
                .ToList();
            if (dropped.Count > 0)
                pending[f.Key] = dropped;
        }
        return pending;
    }

    /// <summary>A first-wins key→value map tolerant of a doc carrying a DUPLICATE frontmatter key — mirroring
    /// <see cref="SyncDoc.GetField"/>'s first-match semantics and the pre-wave overlay's <c>FirstOrDefault</c>
    /// lookup. A bare <c>ToDictionary</c> throws on a duplicate key, which would crash the entire reconcile
    /// tick over one malformed doc (finding 4).</summary>
    private static Dictionary<string, string> FirstWins(IEnumerable<SyncField> fields)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in fields)
            map.TryAdd(f.Key, f.Value);
        return map;
    }

    /// <summary>Split a comma-joined field value into its trimmed, non-empty entries — the same split
    /// <c>NotionSyncAdapter</c> uses for a relation, so per-entry overlay matches the adapter's echo.</summary>
    private static List<string> SplitEntries(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>Append the pending entries not already present to the external value, re-joined with ", ".</summary>
    private static string MergeEntries(string externalValue, List<string> pending)
    {
        var entries = SplitEntries(externalValue);
        foreach (var p in pending)
            if (!entries.Contains(p))
                entries.Add(p);
        return string.Join(", ", entries);
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

    private static SyncDoc WithBody(SyncDoc doc, string body) => new()
    {
        LocalId = doc.LocalId,
        ExternalId = doc.ExternalId,
        Fields = doc.Fields,
        Body = body,
        SourcePath = doc.SourcePath,
    };

    /// <summary>Equality with fields compared RAW (identity normalizer), the body still modulo the adapter's
    /// body round-trip. Used only by the repo-deleted branch, comparing base against the surviving EXTERNAL doc:
    /// both are already free of local-only and un-pushed fields (base is recorded normalized; the external view is
    /// schema-filtered board state), so a raw compare is exact there and needs no normalization.</summary>
    private static bool EqualRawFields(SyncDoc a, SyncDoc b, Func<string, string> norm) =>
        Equal(a, b, norm, static d => d);

    private static bool Equal(SyncDoc a, SyncDoc b, Func<string, string> norm, Func<SyncDoc, SyncDoc> fieldNorm)
    {
        if (norm(a.Body.Replace("\r\n", "\n")) != norm(b.Body.Replace("\r\n", "\n")))
            return false;
        // Order-INSENSITIVE field compare (issue 0299). Field order is cosmetic — the repo keeps a doc's authored
        // frontmatter order, while the external echo comes back in NotionPropertyMapper.ToFields' canonical order
        // (title first, then alphabetical). A pure reorder must not read as a change, or a record whose frontmatter
        // order differs from that canonical order (e.g. `status` before `needs-human`) churns WriteToRepo every tick
        // — reordering the file and dragging in the title/empty-key rewrite side-effects. Sort both sides by
        // (key, value) so the same field set compares equal regardless of order, while a genuine value/key
        // difference still registers.
        var fa = Sorted(fieldNorm(a).Fields);
        var fb = Sorted(WithoutEmptyEchoesAbsentFrom(fieldNorm(b).Fields, RecordedKeys(a)));
        return fa.Count == fb.Count
            && fa.Zip(fb).All(p => p.First.Key == p.Second.Key && p.First.Value == p.Second.Value);
    }

    private static List<SyncField> Sorted(IEnumerable<SyncField> fields) =>
        fields.OrderBy(f => f.Key, StringComparer.Ordinal).ThenBy(f => f.Value, StringComparer.Ordinal).ToList();

    /// <summary>The keys the base actually RECORDED — read from its RAW snapshot fields, not a re-normalization
    /// against the CURRENT relation maps (finding 4). The base is stored already-normalized, so `a.Fields` is
    /// the literal record of what was externalized; re-normalizing it would drop a recorded relation whose target
    /// has since retired, and a genuine board CLEAR of that key would then be swallowed as a phantom empty echo.
    /// `a` is always the base at every call site.</summary>
    private static HashSet<string> RecordedKeys(SyncDoc a) =>
        a.Fields.Select(f => f.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Drop from the compared side any EMPTY-valued field whose key the base never recorded. Real Notion
    /// returns every schema property, so an all-unresolvable relation — dropped from the normalized base — echoes
    /// back as an empty string, a phantom "clear" that would churn WriteToRepo every tick and provoke a spurious
    /// merge conflict once the entry resolves (finding 6). A clear is meaningful only when the base RECORDED the
    /// key, which keeps that key here and still registers as a genuine change (finding 4).</summary>
    private static List<SyncField> WithoutEmptyEchoesAbsentFrom(List<SyncField> fields, HashSet<string> recordedKeys) =>
        fields.Where(f => f.Value.Length != 0 || recordedKeys.Contains(f.Key)).ToList();
}
