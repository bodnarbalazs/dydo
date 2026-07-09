---
title: M0 — Spine Object-Type Completion
campaign:
end:
gate-result: plan-review PASS (2026-07-09, fresh-eyes reviewer, 3 rounds)
seq: 8
start:
status: plan-review
area: project
type: context
---

# M0 — Spine Object-Type Completion

> **Plan-review verdict: PASS** (2026-07-09, DR-039 §2 fresh-eyes gate, three rounds:
> FAIL blocker+5sf → FAIL 1-line gate defect → PASS with live-verified fixes and full
> no-regression diff). Green-lit for implementation; launch is sequenced BEHIND sprint C1
> (v2.0.7 gate) per the sprint body's C1-first constraint. Status flips to `active` at launch.

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

## Dependency order & cross-sprint sequencing

```
m0-1 ∥ m0-2 ∥ m0-3 ∥ m0-4      (disjoint by file; docs slices sequenced in-branch by convention)
        m0-5  (after m0-1 — prose describes the final model — AND m0-2: both touch DRs 033/034,
               m0-2's frontmatter insert lands before m0-5's prose edits)
        m0-6  (after everything merged; pairs with M1's S6 as one live session)
```

**Sprint-level ordering (balazs, 2026-07-09 via Adele): C1 implements FIRST and gates v2.0.7;
M0 implementation follows.** Consequence: m0-4's `Commands/NotionCommand.cs` /
`Services/CompletionProvider.cs` edits are post-C1 by construction — the C1 seam on those files
is resolved by ordering, not slicing. Any change to this sequence goes through Adele.

**Rollback story:** every slice is plain git-revertable (file edits/renames on a branch, no
generated state, no live-board mutation) — the only live mutation is m0-6, which runs
`model-update` + `reset` against a scratch workspace and is recoverable by re-running reset.

## Full file footprint (cross-sprint disjointness, per the pipeline posture)

Code:
- `Templates/sync-model.template.json` + `DynaDocs.Tests/Sync/**` model/schema tests — m0-1 only;
  no other queued sprint touches it.
- `Commands/NotionCommand.cs`, `Services/CompletionProvider.cs`, `Sync/Model/SyncModelLoader.cs`,
  new command tests — m0-4. Disjoint from C1's dispatch/vendor files and M1-S2a's task lifecycle
  handlers; C1 collision potential on `CompletionProvider.cs` is moot under the post-C1 ordering
  above.

Docs & records:
- `dydo/project/decisions/**` — m0-2 (frontmatter-only, all files) then m0-5 (prose in DRs
  033/034) — ordering declared above.
- `dydo/project/changelog/**` — m0-3 (renames + frontmatter) + its folder meta (m0-5).
- `dydo/project/sprints/**` — m0-1 (status-vocab conformance of existing records) + this record's
  own lifecycle.
- `dydo/project/issues/**` — m0-4 resolves issue 0252 (Resolution section + `resolved/` move).
- `dydo/project/pitfalls/_pitfalls.md`, `decisions/_index.md`, `_decisions.md` folder metas — m0-5.
- `dydo/understand/work-model.md`, `dydo/understand/architecture.md` (grep-and-fix) — m0-5.
  **DECLARED CROSS-SPRINT OVERLAP: Olivia's M1-S5 edits the same two files** (her plan lines
  152/158). These two slices are NOT parallel-safe; sequencing (m0-5 vs M1-S5 landing order) is
  Adele's call at landing time — each slice's grep-and-fix is idempotent over the other's output,
  but they must land serially.
- `dydo/reference/dydo-commands.md` + the remaining 6-surface command docs for `model-update` —
  m0-4. **Ripple watch:** if `CommandDocConsistencyTests` demands the README-family /
  `about-dynadocs` template-sourced surfaces, that touches `Templates/**` clone-sync — the same
  class M1-S5 flags; worker reports the ripple to Adele before landing rather than absorbing it
  silently.

Deliberately NOT touched:
- `Commands/TaskApproveHandler.cs` — the archive-emit spec change (DR 040 §2) is handed to
  M1-S2a, which already owns that file. Single ownership preserved.

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
4. **S2b amendments (two):** its archive-as-done dispositions create new `changelog/**` files
   post-M0 — they must arrive with `Changelog` frontmatter (routing through the post-S2a approve
   path satisfies this once S2a implements the amended emit spec), and S2b's stem-collision gate
   **extends to `changelog/**`**, not just `tasks/**`. The backlog-as-`Task`-status shape itself
   is unchanged (matches balazs's constraint).
5. **S6 amendments:** merges with m0-6 into one live-smoke session (same scratch board, same
   token ceremony), and its "`done` rows absent (expected)" checklist item is **superseded** —
   the merged smoke expects the ~670-row `Changelog` DB instead.

**Propagation owner (explicit):** Olivia is released, so the amendment list was handed to
**Adele** (message, 2026-07-09), who owns routing it — either to the agent she re-dispatches
onto the M1 plan task or by applying it at M1 plan-refresh. **Adele gates M1 slice-brief
generation on the amended plan**, so no brief can be cut from the unamended text. The full
amendment list is §§1–5 above; the archive-emit spec (item 1) is the one that ships blank-title
snapshots if missed.

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
