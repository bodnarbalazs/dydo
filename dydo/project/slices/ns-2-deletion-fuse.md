---
title: ns-2 Reconcile Deletion Fuse
blocked-by: ns-1-parent-scoped-state
due:
needs-human: false
priority: High
sprint: notion-stabilization
status: done
work-type: feature
area: backend
type: context
---

# ns-2 Reconcile Deletion Fuse

Defense in depth behind ns-1: even with correctly scoped state, a poisoned or stale snapshot (or a Notion-side mass archive) can make one reconcile pass delete a large share of local records. Today nothing stops it. The fuse makes mass local deletion a loud abort instead of a silent sweep. Dev-mode git recovery exists, so this is a tripwire, not a vault (balazs, 2026-07-20).

## Task

1. In the generic reconcile/apply path (`Sync/SyncRunner.cs` / `Sync/ReconcileEngine.cs` — put the check where local delete actions are materialized, once, so spine and docs both inherit it): before applying, count planned **local deletions** per adapter run. If `deletions > 5 && deletions > 20%` of the tracked records in that adapter's base snapshot, abort that adapter's apply with a clear message listing the would-be-deleted paths (first 20, then "+N more") and the override flag.
2. Add `--allow-mass-delete` to `dydo notion sync` (threaded through `Commands/NotionCommand.cs` → `NotionSyncService`), which disables the fuse for that run.
3. The abort is per-type, and the mechanism is a **result value, not an exception**: the engine/apply path returns a per-adapter result carrying a fuse-tripped flag (plus the would-be-deleted paths); `NotionSpineSync.Run` aggregates results across types (its `void` return changes to a result type), continues with the remaining types, and `NotionSyncService` maps any tripped type to a tool-error exit after all types have run. A thrown exception would wrongly kill the remaining types.

## Files

- `Sync/SyncRunner.cs`, `Sync/ReconcileEngine.cs` — locate the delete materialization point (verify which owns it before editing)
- `Commands/NotionCommand.cs`, `Sync/Notion/NotionSyncService.cs` — flag plumbing
- Tests: `DynaDocs.Tests/Sync/` engine tests + `NotionCommandTests`

## Success criteria

- New tests: 6 deletions of 10 tracked → abort; 6 of 100 → no abort (fails 20% arm); 3 of 4 → no abort (fails >5 arm); `--allow-mass-delete` → applies; other types still reconcile after one type trips.
- Abort message names the flag and lists paths.
- Full ratchet green.
