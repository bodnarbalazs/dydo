---
title: Notion Sync Daemon
area: general
name: notion-sync-daemon
status: backlog
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

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

(Pending)
