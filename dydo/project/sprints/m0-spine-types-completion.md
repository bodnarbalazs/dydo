---
title: M0 — Spine Object-Type Completion
campaign:
end:
gate-result:
seq: 8
start:
status: plan-review
area: project
type: context
---

# M0 — Spine Object-Type Completion

Complete the DR-034 spine so `sync-model` declares every `project/` dir, per the shapes fixed in
[DR 040](../decisions/040-spine-completion-shapes.md): `Decision`, `Changelog`, and `Pitfall`
become object types; the two corpora are conformed so they can pool (titles, stem renames); the
DR-039 sprint vocabulary is encoded in the same model edit; and `dydo notion model-update` gives
model changes a sanctioned path to provisioned boards (issue 0252). Origin: the 2026-07-09 live
docs smoke — the mirror correctly excludes model-declared DBs, but with only 7 types ~750 of ~831
mirrored pages are PM records living as doc pages.

**Planner:** Brian (co-think with balazs 2026-07-09 → DR 040 → this plan). **Gate:** plan-review
by a fresh-eyes reviewer per DR 039 §2 — route through Adele. **No implementation before the
green light and the smoke phase completing.**

## Slices (rows in `sprint-tasks/`, each born `ready`)

| Row | What | Kind | Isolation |
|---|---|---|---|
| m0-1-template-model-completion | 3 new object types + DR-039 sprint vocab in the template | code | worktree-safe |
| m0-2-decision-title-backfill | `title:` on 42 decision records | docs | in-branch |
| m0-3-changelog-conformance | stem renames (13 dup stems) + `title:` on 670 records | docs | in-branch |
| m0-4-notion-model-update-command | `dydo notion model-update` (issue 0252) | code | worktree-safe |
| m0-5-docs-reconciliation | DR-033/034 prose, folder metas, format docs | docs | in-branch |
| m0-6-live-smoke | provision + verify on live board | human-gated | n/a |

## Dependency order

```
m0-1 ∥ m0-2 ∥ m0-3 ∥ m0-4      (disjoint by file; docs slices sequenced in-branch by convention)
        m0-5  (after m0-1 — prose describes the final model)
        m0-6  (after everything merged; pairs with M1's S6 as one live session)
```

## Full file footprint (cross-sprint disjointness, per the pipeline posture)

- `Templates/sync-model.template.json` + `DynaDocs.Tests/Sync/**` model/schema tests — m0-1 only;
  no other queued sprint touches it.
- `Commands/NotionCommand.cs`, `Services/CompletionProvider.cs`, `Sync/Model/SyncModelLoader.cs`,
  command-doc surfaces — m0-4. Disjoint from C1 (dispatch/vendor files) and M1-S2a (task
  lifecycle handlers). **Watch-item for plan review:** if C1 also edits `CompletionProvider.cs`,
  sequence those two landings.
- `dydo/project/decisions/**` (frontmatter-only edits) — m0-2/m0-5.
- `dydo/project/changelog/**` (renames + frontmatter) — m0-3.
- **Deliberately NOT touched:** `Commands/TaskApproveHandler.cs` — the archive-emit spec change
  (DR 040 §2) is handed to M1-S2a, which already owns that file. Single ownership preserved.

## Reconciliation with Olivia's M1 plan (one taxonomy, no fork)

Amendments to `dydo/agents/Olivia/plan-dr034-migration-slices.md`, per DR 040:

1. **S2a spec amended:** the archive snapshot now emits the `Changelog` schema — `title:`
   (from the task name), `date`, `area`, `type: changelog`; **no `status: done` line** (the
   Changelog type has no status property — DR 034 §4's "snapshot must carry `status: done`" is
   superseded). The date-suffix-on-collision guard and all Task-vocab work stand unchanged.
2. **"Explicitly deferred" section superseded:** the changelog-done-rows wiring question is
   dissolved by Changelog-as-own-type (DR 040 §2); m0-3 does the stem renames and title backfill
   that section listed as blockers. The archive stays at `project/changelog/` — no relocation,
   no second-dir mechanism.
3. **S3 proceeds unchanged** — FutureFeature confirmed as its own type (DR 040 §1).
4. **S2b unchanged** — backlog as `Task` at `status: backlog` matches balazs's constraint.
5. **S6 and m0-6 merge** into one live-smoke session (same scratch board, same token ceremony).

M0's code slices (m0-1, m0-4) are file-disjoint from M1 entirely and may run in parallel with it;
landings sequence through Adele as usual.

## Out of scope

- Any file move between `project/` dirs (M1 owns migration: backlog partition, future-features
  frontmatter, inquisitions yank).
- `TaskApproveHandler` / task-lifecycle code (M1-S2a) and the approve-gate reform (A1 / DR 036).
- Planner role + reviewer subskills (P1 / DR 039 R1–R4) — this sprint only encodes the *vocab*.
- The 0249 validator-debt residue (H1) — m0 gates only require "not worse than baseline".
- Spine body sync via native markdown (issue 0236 — scheduled decision after DR-035 smoke).

## Watch-outs

- **Live-only API constraints** (reference/notion-sync.md): no formula referencing a formula (new
  types carry no formulas — keep it that way), 2000-char runs, title-from-H1 blanking. The title
  backfills exist precisely because of the last one; m0-6 verifies on live.
- **Stem keying:** the loader keys rows by filename stem; m0-3's gate re-runs the collision check
  over `changelog/**` and must stay green through M1's archive dispositions.
- **Existing sprint-record statuses** must be conformed to the new vocab in m0-1 (two records:
  `notion-sync` = `active` ✓ valid, `runtime-slim` — worker checks and maps `planned → planning`,
  `in-review → audit`, `done` stays).
- **The live model is guard-off-limits and stale by design** until m0-6 runs `model-update` —
  template edits in m0-1 have zero live effect until then. That is the intended 0252 flow.
