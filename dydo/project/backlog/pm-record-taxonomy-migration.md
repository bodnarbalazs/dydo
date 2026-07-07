---
area: project
type: context
status: open
created: 2026-07-06
created-by: Dexter
origin: DR 034 — PM Record Taxonomy; follow-on migration sprint
---

# PM Record Taxonomy Migration — follow-on sprint

Execution of [DR 034](../decisions/034-pm-record-taxonomy.md). Disjoint, ordered slices. Slice 1 gates
the rest (the file moves need the object types to exist). Keep `[[wikilink]]` / relative-link integrity
across every move — the spine loader keys rows by **filename stem**, so a duplicated stem across
subfolders crashes the sync (`SyncRunner` fails naming both paths); the migration must not create one.

## Slices

1. **sync-model object types (Brian, gating).** Add `Task` (`status: backlog → in-progress →
   in-review → done`, `folders: { backlog: backlog }`) and `FutureFeature` (`status: raw → shaping →
   promoted → dropped`). Optional: prune `ready` from `SprintTask`. See
   `dydo/agents/Dexter/brief-brian-sync-model.md`.
2. **Backlog → Task partition.** Move `project/backlog/*.md` → `project/tasks/backlog/` with
   `status: backlog`; backfill `Task` frontmatter (title/status/…) on existing `project/tasks/*.md`;
   reconcile the old `backlog/done/` archive with the changelog `done` archive.
3. **Future-features frontmatter.** Convert `project/future-features/*.md` from `type: context` /
   `type: concept` to `FutureFeature` frontmatter (add `status`, default `raw`/`shaping` as fits).
4. **Yank inquisitions.** Remove `project/inquisitions/`; archive its 24 reports (do not hard-delete);
   drop it from `WorktreeCommand.JunctionSubpaths`.
5. **Doc reconciliation.** Patch DR 033 §5 prose (future-features confirmed DB; inquisitions leaves the
   docs set); add a superseded banner to DR 023 pointing at DR 034; update `_project.md` / any
   folder-meta Contents lists.
6. **Live smoke.** Provision the two new object types against a scratch Notion workspace — the
   `FakeNotionClient` cannot catch the live-only constraints (formula/2000-char/title-from-H1).

## Out of scope (separate backlog items, referenced by DR 034)
- **Git-derived changelog `Files Changed`** — `backlog/dydo-2-hardening.md` (audit auto-fill removed in
  DR 024, git replacement unbuilt).
- **Runtime → board bridge** — `backlog/notion-board-followups.md` §A: `run-sprint` materializing a
  sprint-task **row per slice** at plan-time + worker mark-done. DR 034 §6 fixes the *shape*; this
  bridge does the *wiring*.
