---
title: Notion Sync Daemon
area: general
name: notion-sync-daemon
status: done
created: 2026-07-16T00:00:00.0000000Z
assigned: unassigned
---

# Task: notion-sync-daemon

Rebuild the stripped `WatchdogService` stub as the ~15s Notion-sync daemon per
[DR-041](../decisions/041-dydo-cedes-orchestration-becomes-authoring-knowledge-layer.md)
resolved-3 (executed by the 2.1.0 simplification campaign; the stub + this scope note live in
`Services/WatchdogService.cs`).

Scope:
- Sync loop (~15s): reconcile dydo docs/PM records with Notion (reuse `NotionSyncAdapter`).
- Self-start: the dydo CLI, on guard trigger, checks whether the daemon is running and starts
  it if not (mirror the throttled-stamp pattern used by daily validation and model-cap restore).
- Bonus per DR-041: live sync doubles as collaborator file-sync between commits.
- Research first: scan open-source Obsidian↔Notion sync repos for patterns/tricks (conflict
  handling, rate limiting, debounce) and fold the good ones into the sync logic.

Runs parallel to balazs's Rail B prompt-file pass (balazs, 2026-07-16).

## Progress

- [x] Delivered as slice [ns-13](../slices/ns-13-sync-daemon.md): `dydo watchdog` repurposed from an inert stub into the Notion-sync daemon — pid-file lifecycle, single-flight loop, cheap O(changes) delta ticks, hourly census, docs.

## Descopes (recorded per ns-13 §6)

- **Guard-trigger self-start — dropped for v1.** The original scope had the dydo CLI auto-start the daemon on a guard trigger (mirroring the throttled-stamp pattern). Deliberately not built: a **manually started** daemon (`dydo watchdog start`) is the v1. A daemon multiplies whatever behaviour exists, so it must be started on purpose, not silently by a hook. Revisit only if the manual start proves a friction point.
- **Interval — 15s default, 5s floor** (as the task originally sketched "~15s"; balazs 2026-07-22 confirmed 15s over the sprint plan's 60s guess — board updates should feel instant on context switch). Not a 60s default.
- **Collaborator file-sync between commits (DR-041 bonus)** — delivered implicitly: a running daemon reconciles both directions every tick, so a colleague's board edits land in the repo between commits. No separate feature was needed.
- **OSS pattern research** — done and recorded separately in [notion-oss-survey.md](../../reference/notion-oss-survey.md); its conflict-handling / rate-limiting / debounce findings are folded into the engine (3 req/s throttle, mass-delete fuse, shadow-tree conflict diversion) and the delta cursor.

## Backlog (descoped but still worth doing)

- **FileSystemWatcher push path.** The tick stat-walks the corpus for changed files (O(corpus) stats, trivial constants — fine at 100×). A watcher-driven push would make even the stat-walk O(changes). Future optimization, not v1.
- **Local-parse scaling seam.** Each tick still loads every type's base snapshot and stat-walks the whole corpus (both O(corpus)-local, trivial constants — well under a second warm at 100×). A push-based `FileSystemWatcher` and an incremental base-snapshot index would make even those O(changes). Future optimization, not v1.
- **Recency-window boundary re-reads.** A page edited within the recency window (~2 min) is re-read each tick during that window — the F1 same-minute-safety. A bulk push landing many pages at once makes them all hits for a couple of minutes — a cost, not a correctness issue; the serial reconcile degrades gracefully. A per-page seen-stamp ledger (re-read once per distinct stamp) would trim it if ever hot, and would also let the recency check drop its dependence on the local clock. Residual correctness sliver (bounded, reviewer-confirmed): a same-minute re-edit made while the daemon is down for longer than the window is deferred until the page's next edit or a manual sync — the seen-stamp ledger would eliminate this too.
- **Delta-path promotion reads the whole board.** A human-resolved shadow's base alignment currently triggers a full `ReadExternalState` (rare — only when a resolved shadow exists). A targeted single-page read would keep it O(1).

## Files Changed

See ns-13 for the full list. Core: `Services/WatchdogService.cs`, `Commands/WatchdogCommand.cs`, `Sync/Notion/NotionSpineDelta.cs`, `Sync/Notion/NotionDeltaState.cs`, `Sync/Notion/NotionSyncService.cs` (daemon entry points), `Sync/SyncRunner.cs` (delta overload).

## Review Summary

Delivered under ns-13. Live 15s / under-5s quiet-tick measurement against the real ~400-record board is performed by the orchestrator after review (no token in the implementing environment).
