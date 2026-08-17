namespace DynaDocs.Sync;

using DynaDocs.Models;
using DynaDocs.Sync.Projection;

/// <summary>
/// The per-object outcome of reconciliation. Carries the decided action and, where relevant,
/// the merged doc to write to repo and/or push externally, plus whether a conflict was recorded.
/// </summary>
public sealed class ReconcileResult
{
    public required string LocalId { get; init; }
    public required ReconcileAction Action { get; init; }

    /// <summary>The doc to write back to the repo (WriteToRepo / Merged / Conflict / Create-from-external).</summary>
    public SyncDoc? RepoWrite { get; init; }

    /// <summary>The doc to push to the external view (PushToExternal / Merged / Conflict / Create-to-external).</summary>
    public SyncDoc? ExternalWrite { get; init; }

    /// <summary>The new base to persist for this object after applying the action.</summary>
    public SyncDoc? NewBase { get; init; }

    /// <summary>External id to delete (Delete action targeting the external side).</summary>
    public string? ExternalDelete { get; init; }

    /// <summary>Repo source path to delete (Delete action targeting the repo side).</summary>
    public string? RepoDelete { get; init; }

    /// <summary>Whether the repo side genuinely changed since base this tick (DR 030 §3). True for a
    /// repo-origin create and any reconcile where the repo content/fields moved (push, clean merge, or a
    /// repo-edit-wins conflict); false for a purely external-driven write-to-repo, a delete, or a no-op —
    /// the signal the engine bumps last-activity on, so an engine-performed external-to-repo write never
    /// counts as activity.</summary>
    public bool RepoChanged { get; init; }

    /// <summary>Scalar keys this push must explicitly CLEAR on the external side (issue 0299, F5): a value the base
    /// recorded non-empty that the repo now carries empty-or-absent, on an UPDATE (not a create). Carried through
    /// to the upsert so the adapter emits the wire clear shape instead of omitting the property and reverting.</summary>
    public IReadOnlyList<string> ClearedKeys { get; init; } = [];

    /// <summary>Whether the canonical file needs its body span replaced.</summary>
    public bool PatchBody { get; init; }

    /// <summary>Whether the canonical file needs scalar frontmatter lines patched.</summary>
    public bool PatchFields { get; init; }

    /// <summary>Whether the external upsert must write its body rather than properties only.</summary>
    public bool WriteBody { get; init; }

    /// <summary>The independent local/external body base to persist after a projected decision.</summary>
    public DualBodyBase? NewBodyBase { get; init; }

    /// <summary>The durable mutation kind used when <see cref="WriteBody"/> is true.</summary>
    public BodyWriteOperationKind? BodyWriteKind { get; init; }

    /// <summary>A projection failure that must be reported in a shadow, never embedded outside its sentinels.</summary>
    public string? StructuredConflictReason { get; init; }

    /// <summary>True when a delta caller must retain its cursor because the record was not safely reconciled.</summary>
    public bool UnhandledProjection { get; init; }

    public bool Conflicted => Action == ReconcileAction.Conflict;
}
