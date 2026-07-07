---
area: project
type: decision
status: accepted
date: 2026-07-06
participants: [balazs, Dexter]
---

# 034 — PM Record Taxonomy: `project/` = Records, the Two-Altitude Work Funnel, the Partition Convention

`dydo/project/**` is the home of **PM records** — typed rows that live as `.md` files, sync to Notion
as filterable **spine databases**, and are excluded from the [docs mirror](./033-docs-notion-nested-page-mirror.md)
**by construction** (the mirror derives its exclusion set from `sync-model`, so declaring a dir a DB
auto-removes it from the mirror). Everything **outside** `project/` (`understand/`, `guides/`,
`reference/`, …) is browsable reference prose → the mirror. This record makes that rule uniform: it
**classifies every `project/` dir**, collapses the folder-as-status warts (`backlog/`,
`future-features/`) into a **status property / record type**, and formalizes the good
**subfolder-as-property-partition** pattern (`issues/resolved/`) as a general, data-driven convention.

Supersedes the doc-category framing of [DR 023](./023-backlog-doc-category.md); consumes the
sync-model exclusion contract of [DR 033](./033-docs-notion-nested-page-mirror.md); builds on the
spine/mirror split of [DR 025](./025-notion-sync-architecture.md).

## Context

`project/` grew dir-by-dir into an inconsistent mix. Three shapes coexist:

1. **True record types** — `issues/`, `campaigns/`, `sprints/`, … : each `.md` is a queryable row.
   These are right.
2. **Folder-as-status warts** — `backlog/` and `future-features/` encode an item's *horizon /
   commitment*, not its *type*. [DR 023](./023-backlog-doc-category.md) shipped them as convention-only
   "doc categories" keyed by `type: context` where *"the folder location is the discriminator"* — i.e.
   status smuggled into a path. Many things carry a "backlog" marker; it is a **property value**, not
   a category.
3. **Subfolder-as-property-partition** — `issues/resolved/`: within one record type, a subfolder
   partitions rows by one status **value**. The loader (`NotionSpineSync.LoadDocs`) flattens and pools
   every `.md` under a type recursively, so the subfolder is **pure filesystem legibility** (you can
   see status without opening frontmatter or Notion), not a separate row space. This is the *good*
   pattern — it just wasn't applied consistently.

Two facts fix the meaning of "record" here. A record is a **file** (frontmatter = Notion properties,
body = the page) — the only thing you can link to, detail, mark done individually, and mirror as a
Notion page; a text list can be none of those. And "is it a spine DB" is **orthogonal** to "does it
use subfolder partitions": `issues/` is a DB *and* partitions; a DB may have no partitions; partitions
never define DB-ness.

## Decision

### 1. The rule
`project/**` = PM **records** → spine object types in `sync-model` → Notion databases, and thereby
**excluded from the docs mirror** (DR 033 §5 derives the exclusion from `sync-model`, never a
hardcoded list). Non-`project/` = browsable **docs** → the mirror. Every dir is exactly one of: a
spine DB, a docs-mirror page, or excluded machinery. No dir is both.

### 2. Classification of every `project/` dir

| Dir | Class | Notes |
|---|---|---|
| `releases/` | **DB** — `Release` | unchanged |
| `campaigns/` | **DB** — `Campaign` | unchanged |
| `sprints/` | **DB** — `Sprint` | unchanged |
| `sprint-tasks/` | **DB** — `SprintTask` | unchanged; §6 |
| `issues/` | **DB** — `Issue` | unchanged; canonical partition example |
| `decisions/` | **DB** | Brian's track (in flight) |
| `pitfalls/` | **DB** | Brian's track (in flight) |
| `future-features/` | **DB** — `FutureFeature` (**new**) | strategic-altitude intake; §5 |
| `tasks/` | **DB** — `Task` (**new**) | tactical-altitude work unit; §4 |
| `backlog/` | **partition of `Task`** | → `tasks/` at `status: backlog`; §4 |
| `changelog/` | **`done` archive of `Task`** | date-partitioned; §4 |
| `inquisitions/` | **Yanked** | removed for now; §7 |
| `_index.md`, `_*.md` folder-meta | **Excluded machinery** | `dydo fix` output; already `_`-skipped by the loader and gitignored patterns |

### 3. The work funnel has two intakes at two altitudes
`backlog` and `future-features` *feel* alike ("uncommitted, not now") but differ in **altitude** and
**origin**, so they are different things:

```
STRATEGIC intake:  FutureFeature ──promote──▶ Campaign / Sprint (via wikilink)
                   (born top-down from vision; feature/campaign-sized; may never ship)

TACTICAL intake:   backlog(Task) ──pick up──▶ Task ──schedule──▶ SprintTask ──▶ changelog
                   (spun off during work; task-sized; scoped, should get picked up)
```

- **Backlog is task-sized** → it is a **`Task` at `status: backlog`**, not its own type.
- **Future-features are campaign-sized visions** → their own **`FutureFeature`** type; folding them
  into "a Task with status=idea" is a granularity mismatch.

### 4. `Task` (new record type; dir `project/tasks/`)
The atomic work unit an agent claims (`dydo agent role --task X`), now a first-class board record.

- **Status vocabulary:** `backlog → in-progress → in-review → done`. No `ready` — if it's picked up
  it's `in-progress`; if not it's `backlog`; nothing sits between.
- **`backlog/` becomes a subfolder partition** of `tasks/` (`tasks/backlog/`, `status: backlog`),
  flattened+pooled like `issues/resolved/`. Active states live at the `tasks/` root.
- **`changelog/` becomes the `done` archive, read as `Task` rows at `status: done`.**
  `TaskApproveHandler` already snapshots an approved task into `changelog/YYYY/YYYY-MM-DD/<task>.md`
  with `type: changelog` — that *is* the "done" partition, kept on a **date** axis (a legitimately
  different axis from status: "what shipped when"). The model **reads** these as `Task` rows at
  `status: done` (decided; not read-only history). Three facts make this clean:
  - **Reading date-nesting is free.** `LoadDocs` pools every `*.md` under a type's dir *recursively
    across all subfolders*, skipping `_`-prefixed — so `YYYY/YYYY-MM-DD/` nesting and the per-day
    `_index.md` hubs are handled with no new code.
  - **`done` is a *handler-placed*, **unmapped** value.** Date-nesting is a placement the approve
    handler owns; it is **not** expressible by the flat status→subfolder `folders` map
    (`RepoFolderLayout` yields one flat name). So `done` is **left out of the `folders` map** (root
    convention: unmapped ⇒ the sync never moves it), and placement stays with `TaskApproveHandler`.
    Requirement on the handler: it must set/keep **`status: done`** on the snapshot (today it only adds
    `type: changelog`).
  - **The archive must sit under the `Task` type's `dir`.** Simplest: relocate the archive under
    `tasks/` (e.g. `tasks/changelog/YYYY/…`) so a single `dir` + recursive pool covers it — a
    `TaskApproveHandler` target-path change (+ `WorktreeCommand.JunctionSubpaths`, doc refs).
    Alternative: teach the `Task` object type to read a second archive dir. **Brian's mechanism call.**
  - **Known edge:** the pool keys rows by filename **stem**; the same task name recurring on two
    different days = two `<task>.md` under different date folders = a duplicate stem, which
    `SyncRunner` fails on (naming both paths). The migration/handler must disambiguate (e.g. date-suffix
    the stem on archive).
- **Files-Changed follow-on:** the changelog's `## Files Changed` section is currently a dead
  `(None yet)` placeholder — the audit-derived auto-fill was removed with the audit teardown
  ([DR 024](./024-dydo-2-native-pivot.md)) and the git-derived replacement (`git diff --name-only` vs
  the task base ref) is **planned but unbuilt** (`backlog/dydo-2-hardening.md`). Out of scope here;
  the migration references it.

### 5. `FutureFeature` (new record type; dir `project/future-features/`)
Kept name (descriptive, zero learning curve). Strategic-intake record.

- **Status:** `raw → shaping → promoted → dropped`.
- **Promotion is a wikilink, not a formal relation, in v1.** A future-feature may become a Campaign, a
  Sprint, or *part of* a campaign — a Notion relation targets exactly one database and cannot express
  that fuzziness. So v1 records where it went with a **body `[[wikilink]]`**. An optional single
  nullable `Campaign` relation may be added later *iff* board-level "which ideas promoted where"
  filtering is wanted. No subfolder partition (low volume).

### 6. `SprintTask` stays a DB — rows are files, materialized at plan-time
Interrogated for a fold-to-checklist simplification and **kept**, because it is the **leaf of the
rollup chain**: `Sprint.progress` rolls up `SprintTask.done`, up to `Campaign`, up to `Release`, and
the `health`/`attention` formulas all sit on `progress` (DR 029). And because they must be
**linkable** (`blocked-by` DAG, `sprint` relation) and **individually markable** — capabilities only a
file/row provides.

The gap this exposes: `run-sprint` does **not** materialize sprint-task rows today — its `Slice` phase
is `normalizeSlices(args)`, consuming pre-made in-memory slices; the 4 existing files are hand-authored.
So the target shape, declared here and **wired by the "Runtime → board bridge" backlog item**
(`backlog/notion-board-followups.md` §A), is:

> **The plan *is* the rows.** The upstream **planner** (the higher-level agent — code-writers
> implement briefs, they do not design; ambiguity is raised *before* the implementation loop, exactly
> the existing `raiseHand` circuit-breaker) emits **one sprint-task file per slice** (brief in the
> body) *before* `run-sprint` runs. `run-sprint` consumes those rows; each worker flips its row
> `in-progress → done` at the natural point — *"mark it done when you're there, don't obsess over it
> mid-implementation."* That drives the board rollup for free.

Wiring is the bridge sprint's job, not this DR's. This DR fixes the *shape*.

### 7. Partition convention (general, data-driven)
- **Root = the default value; only non-default values get a subfolder.** Matches `issues/` exactly
  (`open` + `triage` at root, only `resolved/` subfoldered). Minimal churn — the migration does **not**
  create `issues/open/`, `issues/triage/`, etc.
- **The default is not hardcoded.** A status property's `folders` map in `sync-model` declares
  `value → subfolder`; the root/default is **any option not in that map** (`RepoFolderLayout` returns
  `null` for an unmapped value → the doc stays put and is never moved). So "default = options minus
  folder-mapped," fully declarative in `sync-model.json` (generated from the template, customizable).
- Two distinct "defaults" stay separated: **presentation-default** (root partition = unmapped value,
  already general) vs **creation-default** (initial status of a new record, set by the create
  command/template). Adding an explicit `"default": true` option flag is a possible small sync-model
  add, but the implicit unmapped rule is retained — it already works.

### 8. Yank `inquisitions/`
Undirected inquisition already "died by neglect" and was superseded by the attention ledger + nudge
([DR 032](./032-attention-ledger-and-housekeeping-nudge.md)); directed inquisition is now the
campaign-end QA gate whose findings flow through the qa-loop / `issues/`. So the folder loses its
concept. **Remove `inquisitions/`; archive the 24 existing reports** (historical visit records,
useful bug-hunt context — never hard-delete). Revisit (likely renamed to a generalized attention
`reports/`/`visits/` type) *if and when* patrol/housekeeping demand proves itself — not before.

## Coordination & ownership

- **Brian (`sync-model`)** — add two object types (`Task`, `FutureFeature`) with the schemas above;
  add `Task`'s `backlog` folder-mapping (`{ "backlog": "backlog" }`); leave `issues`/others'
  root-default values unmapped. Consider (his call, for consistency) pruning `ready` from the existing
  `SprintTask` status vocab. See `dydo/agents/Dexter/brief-brian-sync-model.md`.
- **Charlie ([DR 033](./033-docs-notion-nested-page-mirror.md)) — ✅ done (2026-07-06).** The mirror
  exclusion auto-updates from `sync-model` (no code change); DR 033 §5 prose patched: `future-features/`
  now cited as the **confirmed** `FutureFeature` type, `inquisitions/` dropped from the docs set (and
  §5 states it is not mirrored), plus a latent Context-prose fix (it had wrongly listed
  `pitfalls`/`inquisitions`/`changelog` as browsable docs — now `understand`/`guides`/`reference` +
  non-`project`). See `dydo/agents/Dexter/brief-charlie-dr033.md`.

## Migration mechanics (follow-on sprint)
Captured as `backlog/pm-record-taxonomy-migration.md`. Disjoint slices, in order:
1. `sync-model` object types + folder maps (Brian) — unblocks the rest.
2. Move `backlog/*.md` → `tasks/backlog/` with `status: backlog`; backfill `Task` frontmatter on
   existing `tasks/` files; reconcile `backlog/done/` with the changelog archive.
3. Promote `future-features/` items to `FutureFeature` frontmatter (`status`, from the old
   `type: context`/`type: concept`).
4. Remove `inquisitions/`; archive its 24 reports; drop it from `WorktreeCommand.JunctionSubpaths`.
5. Fix DR 033 §5 prose; supersede-banner DR 023.
6. Keep `[[wikilink]]` / relative-link integrity across every move (the loader keys rows by filename
   stem — a duplicated stem across subfolders crashes the sync; the migration must not create one).

## Supersedes / amends
- **[DR 023](./023-backlog-doc-category.md)** — its "doc category, `type: context`, folder-as-
  discriminator" framing for `backlog/`/`future-features/`, and its §5 backlog-vs-task *separate-record*
  distinction, are superseded: `backlog` is now a `Task` status; `future-features` is the
  `FutureFeature` record type. The intent/assignment split collapses into one Task lifecycle
  (`backlog` intent → `in-progress` assignment → `done` archive).

## Consequences & known limitations
- **Two new DB round-trips.** `Task` and `FutureFeature` gain full Notion presence; the `Task` board
  is the intake/triage funnel the chief-of-staff works.
- **Not a free simplification for SprintTask.** Keeping it costs the row-materialization bridge; the
  payoff is an automatic rollup chain (DR 029) and linkable slices.
- **Live-API constraints still apply.** New object types inherit the spine's live-only limits
  (formula-can't-reference-formula, 2000-char runs, title-from-H1) that the `FakeNotionClient` cannot
  catch — the migration's provisioning slice needs a live smoke.
- **Existing projects** don't auto-migrate; the change is additive to `dydo init` scaffolding + a
  one-time workspace migration (out of scope, as in DR 023).

## Related
- [DR 023](./023-backlog-doc-category.md) — superseded doc-category framing.
- [DR 025](./025-notion-sync-architecture.md) — spine vs mirror; canonical files.
- [DR 029](./029-notion-board-design.md) — the rollup/health chain SprintTask leafs.
- [DR 032](./032-attention-ledger-and-housekeeping-nudge.md) — why inquisition-the-concept dissolved.
- [DR 033](./033-docs-notion-nested-page-mirror.md) — mirror; derives exclusion from `sync-model`.
