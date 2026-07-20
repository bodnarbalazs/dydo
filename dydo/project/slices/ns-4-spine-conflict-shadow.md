---
title: ns-4 Spine Conflict Shadow
blocked-by: ns-2-deletion-fuse
due:
needs-human: false
priority: High
sprint: notion-stabilization
status: backlog
work-type: feature
area: backend
type: context
---

# ns-4 Spine Conflict Shadow

When the spine reconcile detects a both-sides-changed conflict, today it writes conflict markers **into the canonical PM file** (issue 0291's degraded path and the 0235/0236 phantom-conflict class make this worse: a lossy round-trip can manufacture the conflict). The docs mirror already has the right answer: divert the external version to a shadow tree (`dydo/_system/notion_sync/`, DR-035 §4) and leave the canonical file untouched. Give the spine the same protection.

**Where the machinery actually lives (plan-gate verified):** the shadow mechanism is already generic in `Sync/SyncRunner.cs` — a `conflictShadowPathFor` ctor parameter, `RouteConflictToShadow` (~:160-177), and the shadowed-record skip in `CommitBase` (~:193-197). The docs mirror uses it; the spine simply passes **no resolver** (`Sync/Notion/NotionSpineSync.cs` ~:237), which is why spine conflicts fall through to canonical-file markers. Do NOT rebuild any of this in the adapter.

## Task

1. Pass a spine shadow-path resolver where `NotionSpineSync` constructs its `SyncRunner` (~:237): conflicts route to `dydo/_system/notion_sync/spine/<type>/<name>.md`, canonical file untouched, base snapshot not advanced (all of which the existing engine machinery then does for free — verify, don't reimplement).
2. Port a spine equivalent of `DocsTreeSync.PromoteResolvedShadows` (~:131) so a human-resolved conflict (shadow deleted, or shadow content adopted into the canonical file) converges on the next sync instead of re-detecting forever.
3. Report each routed conflict in sync output with both paths (canonical + shadow).
4. Document the resolution flow in `dydo/reference/notion-sync.md` (take local: delete shadow + resync; take remote: copy shadow over canonical + resync).

## Files

- `Sync/Notion/NotionSyncAdapter.cs`, `Sync/Notion/NotionSpineSync.cs`, possibly `Sync/SyncRunner.cs`
- `Sync/Notion/DocsTreeSync.cs` / `DocsPageAdapter.cs` — shadow conventions to mirror
- Tests: `DynaDocs.Tests/Sync/Notion/NotionSyncAdapterTests.cs`, `NotionSpineSyncTests.cs`

## Success criteria

- New tests: dual-edit conflict → canonical file byte-identical, shadow file created with external content, snapshot not advanced, conflict reported; next run re-detects; resolving (delete shadow, align file) syncs clean.
- No remaining code path writes conflict markers into canonical spine files (assert the old marker string is gone from the spine path).
- Full ratchet green.
