---
title: 040 — Spine Completion Shapes: Changelog Record Type, FutureFeature Confirmed, Default Pitfall DB, Sprint Vocab Encoding, Model Regen Path
area: project
type: decision
status: accepted
date: 2026-07-09
participants: [balazs, Brian]
---

# 040 — Spine Completion Shapes: Changelog Record Type, FutureFeature Confirmed, Default Pitfall DB, Sprint Vocab Encoding, Model Regen Path

Rulings from the spine-types-completion co-think (balazs at the terminal, 2026-07-09), triggered
by the live docs smoke: the mirror correctly excludes whatever `sync-model` declares as a DB
([DR 033](./033-docs-notion-nested-page-mirror.md) §5), but the model has only 7 types, so ~750 of
the ~831 mirrored pages are PM-shaped records living as doc pages. This record fixes the shapes the
M0 sprint implements; it **amends [DR 034](./034-pm-record-taxonomy.md) §4** and confirms §5.

## Context

Charlie's 2026-07-09 smoke report, balazs's classification: decisions (42), changelog entries
(670), backlog items, and pitfalls still live as mirror pages instead of queryable spine rows.
balazs's shape constraint, verbatim in substance: *"backlog + future-features were meant as
status/horizon properties, decisions + changelog as their own records."* That collided with two
things DR 034 wrote — `FutureFeature` as a record type (§5) and changelog read as `Task` rows at
`status: done` (§4) — so both went back to him.

## Decision

### 1. FutureFeature stays its own record type (DR 034 §5 confirmed)
The horizon distinction is the *type*, not a property on `Task`. balazs: backlog items are small,
well-defined, sprint-foldable — queued work that just didn't make the current sprint. Future
features are top-of-funnel ideas that need hashing out and are usually sprint- or campaign-sized.
Different altitude, different perspective, different DB. (Mechanically this also avoids a second
folders-map dimension competing with `status` to place one file.) Backlog remains exactly what
DR 034 §4 made it: a `Task` at `status: backlog`.

### 2. Changelog becomes its own record type — **amends DR 034 §4**
DR 034 §4 read the changelog as `Task` rows at `status: done` pooled into the Task DB. Overruled:
`Changelog` is its own object type over the existing `project/changelog/` tree (date-nesting pools
recursively for free). balazs's model of the lifecycle:

> `done` is a **Task state** while the work is fresh — the task is just marked done. **Archival**
> is a separate, later event that *generates* the Changelog record and removes the task from the
> task view. The changelog serves a different discovery use-case ("what shipped when") without
> polluting the live task board.

Consequences:
- The entire DR 034 §4 archive-relocation question (move archive under `tasks/` vs teach `Task` a
  second dir — "Brian's mechanism call") is **dissolved** — the archive stays at
  `project/changelog/`, owned by the `Changelog` type.
- Olivia's M1 deferral of changelog-done-rows wiring is **replaced** by M0's changelog slices.
- Schema is minimal: `title`, `date`, `area`. **No status property** — a changelog entry is
  inherently shipped.
- The 13 duplicate filename stems inside the changelog (the loader keys rows by stem; duplicates
  crash `SyncRunner`) are resolved by a **one-time date-suffix rename** of the older colliding
  files, plus the already-approved M1-S2a approve-time collision guard so the stock never regrows.
  Renaming history files is sanctioned — git history is the archive (balazs's 2026-07-08 ruling).
- The archive-generation path (today `TaskApproveHandler`, post-DR-036 `dydo task archive`) must
  emit the Changelog schema: `title:` (fixes the blank-Notion-title trap, live constraint #4 in
  [notion-sync.md](../../reference/notion-sync.md)), `date`, `area`, `type: changelog`.

### 3. Decision record type — schema as surveyed
`status: proposed | accepted | superseded`, `area` (corpus uses `platform` — the area enum for
this type extends beyond the Issue enum), `date`, `participants` as rich_text, `title` backfilled
from each H1 (none of the 42 records carries `title:` today). No supersedes relation in v1 —
wikilinks cover it.

### 4. Pitfall record type ships by default
The dir is empty in this repo (two `_` meta files only), but the type is added to the template
anyway: uniform "`project/` = records" rule, and other dydo projects do have pitfalls. Minimal
schema: `title`, `area`, `date`.

### 5. DR 039 sprint status vocabulary is encoded in this model change
[DR 039](./039-planner-role-review-target-subskills-and-the-plan-gate.md) §3 fixed
`planning → plan-review → active → audit → done` and gave SprintTask `ready` its meaning. M0 is
the sync-model sprint and issue 0252 makes model changes expensive to propagate — encode the new
vocab now rather than touching the model again in P1. `escalated` is retained as an off-path
state (run-sprint's escalation circuit-breaker still needs a word); `gate-result` stays.

### 6. Sanctioned live-model regen: `dydo notion model-update` (issue 0252)
A new subcommand regenerates `_system/sync-model.json` from the template, shows a diff, and
requires confirmation — never a blind overwrite (`SyncModelLoader`'s on-disk-is-source-of-truth
philosophy stands; projects may customize the live file). It warns when the diff implies
re-provisioning. Rejected alternative: folding into `dydo template update` — that machinery is
markdown/hash-oriented and explicitly skips the model today.

## Consequences

- The docs mirror shrinks from ~831 pages to roughly the ~40 true docs
  (`understand`/`guides`/`reference`), entirely by construction via DR 033 §5.
- Two corpus-conformance passes are required before the new types can pool: decision titles (42
  files) and changelog titles + stem renames (670 files, ~15 renames).
- Olivia's M1 plan is amended, not forked: S2a's `TaskApproveHandler` spec now targets the
  Changelog schema above (single owner of that file stays M1-S2a); her S3 proceeds unchanged
  (FutureFeature confirmed); her deferred-decision section is superseded by this record.

## Related
- [DR 033](./033-docs-notion-nested-page-mirror.md) — mirror exclusion derived from sync-model.
- [DR 034](./034-pm-record-taxonomy.md) — the taxonomy this completes; §4 amended, §5 confirmed.
- [DR 036](./036-task-approval-reform.md) — the archive event's future CLI shape.
- [DR 039](./039-planner-role-review-target-subskills-and-the-plan-gate.md) — sprint vocab, plan gate.
- Issues 0252 (regen path — resolved by §6's implementation), 0249 (validator debt, partly mooted).
