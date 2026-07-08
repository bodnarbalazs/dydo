---
area: project
type: decision
status: accepted
date: 2026-07-08
participants: [balazs, Kate]
---

# 036 — Task Approval Reform: Verifier Flips Done, the Done Window, and the Archive Sweep

The per-task **human approve gate is retired**. It certified nothing: quality is gated by the
automated review machinery (run-sprint reviewer + sprint-auditor, [DR 031](./031-sprint-auditor-charter-rewrite.md)),
and the human's real review happens asynchronously, at his own pace — so in practice the gate
degenerated into `dydo task approve --all` rubber-stamping, which is how the 2026-07-08 bulk-approve
incident archived all 46 board tasks including claimed in-progress ones
(`backlog/task-approve-workflow-rethink.md`). The replacement rests on three principles:
**the implementer never declares their own work done**, **done ≠ archived** (the gap is the human
scrutiny window), and **human review is asynchronous feedback, never a synchronous gate**.

Builds on the [DR 034](./034-pm-record-taxonomy.md) `Task` status vocabulary
(`backlog → in-progress → in-review → done`); DR-034 S2a lands that vocabulary as the data model,
this record decides the **transition authority** S2a was explicitly told not to hard-wire.

## Decision

### 1. `in-review → done` flips on verification, not on human sign-off

The human-only `approve`/`reject` verbs retire. A task flips to `done` by one of two paths:

- **Review pass.** `dydo review … --status pass` flips the task `in-review → done` directly.
  The reviewer is the verifier; the old `human-reviewed` holding state dies. A failed review
  flips back to `in-progress` (the old `review-failed` state also dies, per the S2a mapping).
- **Dispatcher accept.** For work that doesn't run the review loop (plans, doc rounds, co-thinker
  output), the **dispatching agent** accepts via a new `dydo task done <name>` — but only after the
  acceptance checklist (§2).

**Separation of duties is CLI-enforced, not just prompted:** `dydo task done` (and the review-pass
flip) **refuses when the invoking agent identity equals the task's `assigned` agent**. The
implementer can mark `ready-for-review`; someone else must flip done. Human terminals (no agent
identity) are always allowed.

### 2. The acceptance checklist (skill-level, not CLI)

A dispatcher may accept only after confirming — this lives as prompting in the dispatcher-side
skills (orchestrator, chief-of-staff, run-sprint), not as code:

1. The work was **reviewed by someone other than the implementer** (review loop, or the
   dispatcher's own inspection for non-code deliverables).
2. It was **verified to work in at least two independent ways** (e.g. tests green *and* the flow
   exercised end-to-end; for docs: links/`dydo check` green *and* content read against the brief).
3. Follow-ups spun off (issues/backlog) rather than silently dropped.

### 3. `done` ≠ `archived` — the done window

A `done` task **stays on the board** (`tasks/` root, `status: done`) for a scrutiny window. This is
where balazs's asynchronous review happens: he inspects what interests him, when he's there. As a
side effect the board gets a real Done column *now*, closing the gap DR-034 left while
changelog-as-done-rows wiring is deferred.

Archiving to the changelog is a separate, boring, mechanical sweep: **`dydo task archive`**.

- **Authority: chief-of-staff role or human only.** Any other agent identity is refused.
- Sweeps **only `status: done`** tasks — never anything else, no matter what flags say.
- Default age filter `--older-than 7` (days since `done`); override allowed. Prints the list it
  will archive; `--dry-run` supported.
- **Refuses tasks currently claimed** by a working agent (`--force` to override).
- **Lossless frontmatter**: the changelog snapshot keeps `name`/`assigned`/`created`/`updated`,
  keeps `status: done` (DR-034 §4 requirement), and adds `type: changelog` + `date:`. No more
  destructive stripping — restore never needs hand-reconstruction.
- **Date-suffixes the stem on collision** (rides the S2a hardening) so recurring task names stop
  minting duplicate stems that crash the spine sync.

The old `approve --all` footgun dies with `approve`; its safe successor is this sweep.

### 4. Human review is feedback, not a gate

No synchronous human gate remains — not even opt-in (`gate: human` frontmatter was considered and
rejected: nobody would remember to set it, and the cases that matter already surface through the
existing **raise-hand** mechanism). When async inspection finds a problem, that is **new work**
(an issue or task: "rework X"), never an un-done of the finished task. No `seen` marker in v1;
if a "done since you last looked" digest proves wanted, it plugs into the
[DR 032](./032-attention-ledger-and-housekeeping-nudge.md) attention ledger later.

## Command-surface delta

| Command | Fate |
|---|---|
| `dydo task approve` / `reject` | **Removed** (with `--all`) |
| `dydo task done <name>` | **New** — verifier/dispatcher flip; refuses implementer |
| `dydo task archive` | **New** — CoS/human-only done sweep (rails above) |
| `dydo task ready-for-review` | Stays (implementer's only lifecycle exit) |
| `dydo review … pass/fail` | Stays; pass now flips `done`, fail flips `in-progress` |

Command removals/additions red `CommandDocConsistencyTests` until the six doc surfaces are updated
(HelpCommand, CommandSmokeTests, dydo-commands.md + template, about-dynadocs.md + template).

## Implementation outline (follow-on sprint; after DR-034 S2a lands)

Managers Doctrine: all through `run-sprint`. S2a is the prerequisite (status vocab is the data
model these transitions write).

- **R1 (code, worktree-safe):** lifecycle transitions — review pass → `done`, fail →
  `in-progress`; `dydo task done` with the implementer≠verifier check; remove `approve`/`reject`;
  retire `human-reviewed`/`review-failed` states; tests + the six doc surfaces.
- **R2 (code, worktree-safe):** `dydo task archive` sweep — CoS/human authority check, done-only
  filter, `--older-than`/`--dry-run`, claimed-task guard, lossless frontmatter snapshot
  (subsumes the S2a `TaskApproveHandler` hardening if R2 lands the same sprint; otherwise inherits
  it), stem date-suffixing, changelog hub regen.
- **R3 (docs/skills, in-branch):** acceptance checklist into orchestrator / chief-of-staff /
  run-sprint skill docs; task-lifecycle prose in reference docs and workflow templates; supersede
  notes on the `human-reviewed` mentions.

## Consequences & known limitations

- **Nothing waits for the human.** Task throughput is gated only by the review machinery.
- **Done tasks are board-visible until swept** — deliberate (scrutiny window), bounded (sweep).
- **Self-directed tasks** (agent creates and works its own task) have no natural dispatcher; the
  implementer≠verifier rule still forces a second pair of eyes — any peer/CoS can accept. Accepted
  cost: a nonzero coordination step for a rare case.
- **The checklist is prompting, not enforcement.** Only the identity check (§1) and archive rails
  (§3) are mechanical. If checklist discipline erodes, escalation is a future CLI check, not more
  prose.
- `reject` disappears as a verb; its two meanings split cleanly: review-fail (automated loop) and
  async human feedback (new issue/task).

## Related

- [DR 034](./034-pm-record-taxonomy.md) — `Task` type, status vocab, changelog-as-done-archive.
- [DR 031](./031-sprint-auditor-charter-rewrite.md) / [DR 032](./032-attention-ledger-and-housekeeping-nudge.md)
  — the automated quality gates and the attention machinery the human's async review rides on.
- `backlog/task-approve-workflow-rethink.md` — trigger incident + reframe; superseded by this record.
