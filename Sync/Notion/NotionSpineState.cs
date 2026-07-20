namespace DynaDocs.Sync.Notion;

using DynaDocs.Sync.Model;
using DynaDocs.Sync.Notion.Provisioning;

/// <summary>
/// The single decision point (issue 0257) that resolves ALL parent-scoped spine state — the provision-state
/// file and every per-type base snapshot — for both <c>dydo notion sync</c> and <c>dydo notion reset</c>.
/// Every path is scoped by <see cref="ParentPageKey.Hash8"/>, the same convention
/// <see cref="DocsTreeSync.SnapshotAdapterName"/> uses for the docs mirror, so a scratch <c>--parent-page</c>
/// target's state is strictly disjoint from the configured board's: resetting a scratch page can neither
/// archive the real board nor poison its snapshots. Sync and reset resolving state independently — and reset
/// then wiping the real board — was the reverted attempt's bug (CRITICAL 1/2); they now share this one
/// function, and nothing else computes a state path.
/// </summary>
public sealed class NotionSpineState
{
    public string DydoRoot { get; }
    public string ParentPageId { get; }
    private readonly string _hash;
    private readonly string _scopedProvision;
    // A dry-run preview of the CONFIGURED board in the pre-migration upgrade window must READ the legacy files a
    // real run would migrate first (issue 0257): otherwise LoadTracked reads a not-yet-created scoped file and a
    // reset --dry-run understates "would archive 0" while the real run would migrate + archive every real database.
    private readonly bool _previewLegacy;

    private NotionSpineState(string dydoRoot, string parentPageId, bool previewLegacy)
    {
        DydoRoot = dydoRoot;
        ParentPageId = parentPageId;
        _hash = ParentPageKey.Hash8(parentPageId);
        _scopedProvision = Path.Combine(
            Path.GetDirectoryName(NotionProvisioner.PathFor(dydoRoot))!, $"provision-{_hash}.json");
        _previewLegacy = previewLegacy;
    }

    public string ProvisionPath => ReadPath(_scopedProvision, NotionProvisioner.PathFor(DydoRoot));

    /// <summary>This parent's base-snapshot adapter name for an object type: <c>notion-&lt;hash8&gt;-&lt;type&gt;</c>.</summary>
    public string SnapshotAdapterName(string objectType) =>
        "notion-" + _hash + "-" + objectType.ToLowerInvariant();

    public string SnapshotPath(string objectType) =>
        ReadPath(
            BaseSnapshotStore.PathFor(DydoRoot, SnapshotAdapterName(objectType)),
            BaseSnapshotStore.PathFor(DydoRoot, "notion-" + objectType.ToLowerInvariant()));

    /// <summary>The scoped path always — except a dry-run preview of the configured board in the pre-migration
    /// window, where the real run would first rename the legacy file into the scoped name, so a read must see the
    /// legacy content to preview the real plan. A real (non-preview) run always resolves the scoped path.</summary>
    private string ReadPath(string scoped, string legacy) =>
        _previewLegacy && !File.Exists(scoped) && File.Exists(legacy) ? legacy : scoped;

    /// <summary>Resolve the parent-scoped spine state for a run. The effective parent is the explicit
    /// <paramref name="parentPageOverride"/> when present, else the configured page. On the first non-dry run for
    /// the CONFIGURED board, legacy project-scoped files are migrated into their scoped names; a dry-run previews
    /// that migration (and reads the legacy files) without renaming anything. An override to a DIFFERENT parent
    /// starts clean and never migrates; an override EQUAL to the configured parent — compared on the canonical
    /// <see cref="ParentPageKey.Normalize"/> form — counts as non-override, so resetting the configured board by
    /// explicit id migrates instead of orphaning legacy state and re-minting a duplicate (issue 0257 MEDIUM 3).</summary>
    public static NotionSpineState Resolve(
        string dydoRoot, string? configuredParentPageId, string? parentPageOverride, bool dryRun, TextWriter output)
    {
        var configured = NotionParentResolver.Resolve(configuredParentPageId);
        var effective = !string.IsNullOrWhiteSpace(parentPageOverride) ? parentPageOverride : configured;
        var isConfigured = string.IsNullOrWhiteSpace(parentPageOverride)
            || (configured != null && ParentPageKey.Normalize(parentPageOverride) == ParentPageKey.Normalize(configured));
        var state = new NotionSpineState(dydoRoot, effective ?? "", previewLegacy: dryRun && isConfigured);

        if (!string.IsNullOrWhiteSpace(effective) && isConfigured)
        {
            if (dryRun)
                state.ForEachPendingMigration((legacyPath, scopedPath, name) =>
                    output.WriteLine($"notion: would migrate legacy state -> {name}"));
            else
                state.ForEachPendingMigration((legacyPath, scopedPath, name) =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(scopedPath)!);
                    File.Move(legacyPath, scopedPath);
                    output.WriteLine($"notion: migrated legacy state -> {name}");
                });
        }
        return state;
    }

    /// <summary>Invoke <paramref name="onPending"/> once per legacy project-scoped file (<c>provision.json</c>, each
    /// <c>notion-&lt;type&gt;</c> snapshot) that still needs renaming into this parent's scoped name — a scoped file
    /// already present means a prior run migrated it, so it is skipped. The one traversal both a real migration and a
    /// dry-run preview drive, so the preview lists exactly the files the real run would rename.</summary>
    private void ForEachPendingMigration(Action<string, string, string> onPending)
    {
        var legacyProvision = NotionProvisioner.PathFor(DydoRoot);
        if (File.Exists(legacyProvision) && !File.Exists(_scopedProvision))
            onPending(legacyProvision, _scopedProvision, Path.GetFileName(_scopedProvision));

        foreach (var type in SyncModelLoader.Load(DydoRoot).Objects)
        {
            var legacySnapshot = BaseSnapshotStore.PathFor(DydoRoot, "notion-" + type.Type.ToLowerInvariant());
            var scopedSnapshot = BaseSnapshotStore.PathFor(DydoRoot, SnapshotAdapterName(type.Type));
            if (File.Exists(legacySnapshot) && !File.Exists(scopedSnapshot))
                onPending(legacySnapshot, scopedSnapshot, SnapshotAdapterName(type.Type));
        }
    }
}
