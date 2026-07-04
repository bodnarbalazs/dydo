---
area: general
type: decision
status: accepted
date: 2026-07-03
participants: [balazs, Charlie]
---

# 029 — Notion Board Design: Visual Schema, PM Semantics, and Schema-Shape Ownership

The Notion PM board gets an intentional visual design — one **color language** across all object types,
a **word-based priority scheme with operational definitions**, and type-level icons — plus three new
board capabilities: **progress rollups**, a **blocked-by relation pair**, and **dates powering a
timeline/Gantt view**. Every property is explicitly tagged **CANONICAL** (stored in frontmatter,
synced two-way per [Decision 025](./025-notion-sync-architecture.md)) or **VIEW-ONLY** (computed in
Notion from canonical data, never stored in the repo). The ownership rule that makes this coherent:
**data values flow two-way; schema shape flows one-way, project → Notion.** Rogue schema edits made
in Notion are **warned about and left alone**, with an explicit `--prune` to yank them deliberately.

## Context

With dydo 2.0 the human is PM of agents, and the Notion board is the management surface (DR 025). The
provisioned defaults were functional but flat: uncolored selects, engineer-only `P0–P3` priorities, no
progress signal, no dependency modeling, no timeline. Separately, Balazs raised the shape question —
*"what happens if we edit the shape of data in Notion? It should be owned by the project, right?"* —
which DR 025 implies but never states. Constraints verified against the Notion API (`2026-03-11`):
select option colors come from a fixed palette (no hex); select options carry no icons or descriptions;
progress bars are number/rollup renderings computed in Notion; Gantt is a timeline view over date
properties.

## Decision

### 1. One color language across every type

Colors carry consistent meaning everywhere, so the board reads as a system:

| Color | Meaning |
|---|---|
| gray | not started |
| purple | queued / ready |
| blue | in motion |
| yellow | awaiting review |
| red | **needs a human** (blocked, escalated) — reserved for attention |
| green | done |
| brown | terminal-negative (abandoned) |

Board-view columns follow select option order, so the left-to-right orderings below are part of the
schema, not presentation preference.

### 2. Priority: words with operational definitions

`P0–P3` is replaced by a self-explanatory scheme, **reused identically** on every type that has a
priority (Campaign, SprintTask, and — aligned with the Release/Issue work — Issue). Each level is
defined by what you do about it, not by an adjective; these definitions are part of the contract
(documented here and in the sync-model docs; optionally mirrored into the Notion property description,
since options themselves cannot carry descriptions):

| Priority | Color | Operational meaning |
|---|---|---|
| **Urgent** | red | Drop everything. Production is broken or all work is blocked until this lands. Never waits for sprint planning. |
| **High** | orange | Must be scheduled into the current or next sprint. Does not interrupt in-flight work, but cannot slip past the next planning. |
| **Normal** | yellow | The default. Flows through regular sprint planning, ordered by judgment. |
| **Low** | gray | Backlog-eligible. May sit unscheduled indefinitely; first candidate to cut from a sprint. |

### 3. Icons: type-level only

One emoji per object type (database + rows) so a page's *kind* is instantly assessable: Campaign 🚀,
Sprint 🏃, SprintTask 📋 (recommended for the incoming types: Release 📦, Issue 🐛). **No per-status
icons**: select options can't carry icons, so per-status icons would mean the sync engine rewriting
page icons on every status change — complexity for a signal the status badge color already carries.

### 4. Per-object-type schema

Every property tagged **C** (canonical: frontmatter, two-way) or **V** (view-only: computed in
Notion, never stored).

**Campaign 🚀** — statuses: proposed `gray` → active `blue` → done `green` → abandoned `brown`

| Property | Type | Tag |
|---|---|---|
| title | title | C |
| goal | rich_text | C |
| status | select (above) | C |
| priority | select (§2) | C |
| progress | rollup: % of related Sprints done, progress-bar render | V |
| dates | rollup: earliest start / latest end across Sprints | V |

**Sprint 🏃** — statuses: planned `gray` → active `blue` → in-review `yellow` → done `green` → escalated `red`

| Property | Type | Tag |
|---|---|---|
| title | title | C |
| seq | number | C |
| status | select (above) | C |
| campaign | relation → Campaign | C |
| start / end | date range | C |
| progress | rollup: % of related SprintTasks done, progress-bar render | V |

**SprintTask 📋** — statuses: backlog `gray` → ready `purple` → in-progress `blue` → in-review `yellow` → blocked `red` → done `green`

| Property | Type | Tag |
|---|---|---|
| title | title | C |
| status | select (above) | C |
| priority | select (§2) | C |
| sprint | relation → Sprint | C |
| blocked-by | relation → SprintTask (self), multi-value | C |
| blocks | auto reverse of blocked-by (Notion dual-property) | V (derived) |
| due | date (optional) | C |

**Release / Issue** (Brian's in-flight types): adopt the same color language and the §2 priority
scheme; Release gets a **progress rollup (V)** over its children — Balazs flagged release progress as
possibly the most useful metric on the board. Which relation Release rolls up over depends on Brian's
model design; aligned via coordination, not decided here.

### 5. New capabilities settled

- **Progress bars — yes, VIEW-ONLY, provisioner-created.** Rollups on Sprint, Campaign, and Release,
  computed in Notion, rendered as bars, nothing stored in the repo (DR 025 §4: rollups compute at the
  edge). The **provisioner creates them** — Notion is a provisioned projection and the board must
  survive re-provisioning from scratch with zero hand configuration.
- **Blockers — yes, as a relation pair, not an object type.** Canonical `blocked-by: [task-ids]`
  frontmatter on SprintTask (self-relation, multi-value), synced two-way; Notion's dual-property
  relation derives the reverse "Blocks" column (view-only). DR 025 §7's richer Blocker *entity*
  (detail/severity/raisedBy, auto-materialized from agent raise-hands) is deferred — revisit when
  raise-hand integration lands; the two compose.
- **Dates/Gantt — yes.** Sprint owns canonical `start`/`end`; Campaign dates are a **derived rollup**
  (view-only — an aggregate doesn't belong in frontmatter); SprintTask gets at most an optional `due`.
  The timeline view itself is presentation.

### 6. Schema-shape ownership: values two-way, shape one-way

- **Data values** (a page's status, priority, blocked-by, dates, …) are two-way: edited anywhere,
  3-way merged per DR 025 §3.
- **Schema shape** (which object types exist; which properties; their types, options, colors, icons)
  is owned by the project's sync-model and flows **one-way, project → Notion, at provision time**.
  Notion never teaches the project new columns; the canonical model wins.
- **Rogue schema edits in Notion** (a property added or renamed, a select option added):
  **warn + leave**. The provisioner/sync reports the drift loudly but does not touch it — reverting
  would silently delete a colleague's column and its data, violating "never a silent clobber"; ignoring
  it would let the board and model diverge invisibly. Rogue properties are already inert to sync (the
  mapper skips unknown names). An explicit **`dydo notion provision --prune`** performs the revert
  deliberately, yanking all rogue additions. A rogue select *option* is the same one level down: its
  *value* still round-trips as data, but the option is schema — warned, never adopted into the model.

## Consequences & implementation notes

Implementation is **coordinated with Brian's model-layer track** (Release/Issue types, sync-model.json,
status folder-moves) — folded into his orchestration or sequenced after his slice; not a parallel edit
of the same files. Known gaps the implementation must cover:

- `SyncPropertyDef` options are plain strings — needs a color slot (e.g. `{name, color}` options).
- `SyncModel.InDependencyOrder()` treats a self-relation as a cycle and throws — `blocked-by`
  (SprintTask → SprintTask) requires fixing this false positive.
- `NotionPropertyMapper` round-trips only the **first** entry of a relation — multi-value `blocked-by`
  needs full multi-relation support both directions.
- Date handling maps `start` only — Sprint's date range needs end-date support in mapper + provisioner.
- The provisioner gains rollup + dual-property-relation schema creation, and the warn-on-drift check
  (+ `--prune`).
- The priority rename (`P0–P3` → Urgent/High/Normal/Low) is a data migration for existing frontmatter.

## Status

Accepted (Balazs green-lit 2026-07-03). Design only — no sync-model/mapper/provisioner edits under
this record; implementation sequencing goes through Brian.
