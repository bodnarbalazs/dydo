---
title: Projected Reconcile and Surgical File Patch
sprint: notion-body-fidelity
seq: 3
status: ready
area: backend
type: context
---

# Slice 3 — Projected Reconcile and Surgical File Patch

Integrate representation-local decisions into reconciliation and apply only the actual body or
frontmatter deltas to canonical files.

## Spec fragment

Integrate dual-projection decisions into the generic engine and apply them surgically to canonical
files. Acceptance: representation-local change detection, exact body/frontmatter preservation,
receipt-gated base advancement, durable crash intent, and permanent shadow routing for structured
projection conflicts in both full and delta runners.

## Implementation detail

Extend `ReconcileResult` with independent body/field patch information and a structured projection
conflict reason; keep existing actions for reporting compatibility. Add a projected path in
`ReconcileEngine` which consumes `DualBodyBase` and `ProjectedMarkdownMerge`. Existing identity adapters
continue through the current path. Remove raw `ThreeWayTextMerge` only from projected-body decisions.

In `SyncRunner`, make `Run` and `RunDelta` share one per-object projected reconciliation/apply/commit
path. Before a body upsert, persist `BodyWriteIntent`; call `ApplyWithReceipts`; commit local/external
bases only for confirmed receipts; leave intent/base safe on throws. Recovery resolves a pending intent
against prior bases/current repo/current external. For `Create`, it may bind the nullable external id only
from Slice 4's exact operation-identity match; `Update` and `Resolution` require their journaled id.
`Resolution` remains a distinct recovery path and is never demoted to an ordinary two-sided edit. Shadow
ambiguity. Route structured conflicts via
the existing shadow resolver by rendering the complete local and external candidates inside
`<<<<<<< repo`/`=======`/`>>>>>>> external` sentinels, with the structured reason in the report rather
than the body. This preserves the existing invariant that marker-bearing means unresolved and
marker-free means human-resolved; never create a marker-free unresolved shadow.

When constructing `SyncUpsert`, set `WriteBody=false` for field-only decisions and `true` only for a
body create/change/resolution. Identity adapters may ignore it until their own integration, but the
projected Notion adapter in Slice 4 must honor it and receive the pending `OperationId`. Keep field/body
decisions separate in `ReconcileResult`.

If `SyncRecord.BodyReadStatus` is `Truncated`, do not call the projection merge. For an existing mapped
record, emit a marker-bearing structured shadow containing the full local candidate and an explicit
`external body unavailable: truncated export` candidate/diagnostic; leave canonical, remote, and base
unchanged. For an unmapped externally-created record, do not create a canonical file; emit the same loud
conflict artifact under the sanitized prospective local id. Delta cursors must not classify this as handled.

Add `SyncDocFile.PatchExisting` using `FrontmatterParser.Bounds` and source line spans. Replace only the
body span for body-only results. For field deltas, replace/delete the matching one-line scalar or insert a
new encoded scalar before the closer while retaining every untouched line, order, comment, newline, and
body byte. Compose field/body edits in memory and perform one existing atomic sibling rename. Continue
using `Render` for new files/shadows.

Add exact test classes `ProjectedReconcileTests` and `SyncDocFilePatchTests`. Exercise no-op, repo-only, external-only, disjoint/overlap, body-only, field-only, combined,
shadow/base behavior, apply/readback failure, restart recovery, and byte-level preservation. Run the
existing generic sync/reconcile/file suites as regression.

## Out of scope for this slice

Notion-specific transport and legacy snapshot classification.

## Gate

```powershell
$listed = dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --list-tests --filter "FullyQualifiedName~ProjectedReconcile|FullyQualifiedName~SyncDocFilePatch"
if (($listed | Select-String 'ProjectedReconcile|SyncDocFilePatch').Count -lt 12) { throw 'Engine gate matched fewer than 12 new tests.' }
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj --no-restore --filter "FullyQualifiedName~ProjectedReconcile|FullyQualifiedName~SyncDocFilePatch|FullyQualifiedName~SyncRunnerTests|FullyQualifiedName~ReconcileEngineTests|FullyQualifiedName~SyncDocFileTests"
dotnet build DynaDocs.csproj --no-restore
```
