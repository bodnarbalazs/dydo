---
area: general
type: decision
status: accepted
date: 2026-07-03
participants: [balazs, Charlie]
---

# 030 — Board Attention Signals and Adopted PM Properties

Extends the [DR 029](./029-notion-board-design.md) board with properties mined from a survey of
established PM systems (Linear, Jira, GitHub Projects, Basecamp/Shape Up, Shortcut, Asana, Notion's
own templates), filtered through one lens: **a solo human PM-ing autonomous AI agents, whose scarce
resource is attention**. Two design rules govern everything adopted: **attention signals must be
machine-written, never dependent on agent discipline** (detection is event-based via hooks and the
watchdog, and the state is *derived* — reconciled against ground truth every tick, so it self-heals);
and **anything a formula can compute is computed, view-only** — health is calculated, not judged,
because the human will not hand-set verdict fields.

## Context

DR 029 settled visuals, priorities, progress, blockers, and dates. A two-sweep research pass over
established PM tools surfaced candidate properties; Balazs accepted seven and challenged the two
attention-bearing ones: *how does the system know a human is needed without trusting agent
discipline (95%+ accuracy, agent-cancelable, drift-proof)?* and *who sets health, because I won't.*
Those challenges reshaped both features from "fields someone maintains" into "signals the system
derives," and are the substance of this record.

## Decision

### 1. `needs-human` — canonical flag, machine-written, derived, self-healing

A checkbox on **SprintTask** and **Issue** (Sprint/Campaign get view-only rollup counts). One board
view filtered on it is the human's entire work queue; everything else is verifiably not blocked on
him. Canonical (frontmatter, two-way) so un-checking it in Notion means something — but its
**writers are mechanisms, not agent judgment**:

| Event | Detector |
|---|---|
| Agent calls `AskUserQuestion` | PreToolUse guard — it is a tool call like any other (~100% precision) |
| Agent asks in plain text and stops | **Stop hook**: a terminal question always ends the turn; rule = *turn ended while status=working with an in-flight task* ⇒ session idle, waiting on a human. Pure session state, no text analysis |
| Raise-hand, 5-round review failure, gate dispute | dydo's existing escalation paths write the flag directly |
| Session crashed mid-task | The watchdog's existing orphan/liveness detection — a dead session mid-task also needs the human |

**Clearing (self-healing, the flag is derived state, not accumulated state):**
- The agent's next guarded tool call (human answered, work resumed) auto-clears it.
- Agents can clear explicitly via a dydo command (not only the human from Notion).
- Every watchdog tick reconciles flag ↔ ground truth (session actually stopped? task still held?
  released?); a flag whose cause disappeared is swept.

Accepted imprecision: a turn ending in "done, what's next?" also flags — but for an in-flight task,
"stopped, waiting for input" is exactly what the queue should show, and it self-expires on resume.
All signals are observable events, not inference; this is what clears the 95% bar.

### 2. Health — computed, view-only, set by no one

`On Track` / `At Risk` / `Off Track` as a **Notion formula** on Sprint, Campaign, and Release. All
inputs already exist on the board (canonical dates, progress rollups, needs-human counts, staleness,
the sprint gate verdict):

- **Off Track** — past its end date and not done; or the gate verdict failed; or blocked /
  needs-human for more than N days.
- **At Risk** — schedule math (elapsed fraction of the date range far ahead of the progress
  rollup); or any child blocked, stale, or waiting on a human.
- **On Track** — otherwise.

Always live (no stale-green problem), zero storage, zero maintenance, cannot drift — recomputed from
canonical facts on every view. A manual override select is deliberately **not** included (add later
if ever needed). The judged-health design (Linear/Asana style status updates) was considered and
rejected: it presumes a human who curates verdict fields.

### 3. Staleness — view-only, derived from real repo activity

"In progress and untouched for N days" flags red: for autonomous agents a motionless task is a
crashed loop or silent failure, not a busy engineer (*"a dot that doesn't move is a raised hand"* —
Shape Up). Notion's `last_edited_time` is falsified by mass syncs/re-provisions, so the sync engine
— which already knows which side genuinely changed on every tick from its base-snapshot diff
(DR 025 §3) — maintains a true **last-activity** date; a trivial formula renders idle-days from it.

### 4. Attention — one view-only composite

A single 🚨 formula column: red when *any* of `needs-human`, status ∈ {blocked, escalated},
stale, or health ≠ On Track. The sortable "should I look at this?" answer; four independent honest
signals underneath, composed at the view layer — deliberately **not** collapsed at the data layer,
so a machine never overwrites a distinct signal's meaning.

### 5. Classification properties (all canonical, agent-set at creation)

- **Work Type** — select `Feature` / `Bug` / `Chore` / `Spike` / `Docs` on SprintTask and Issue.
  "Chore" is load-bearing: agents generate much necessary-but-boring work; views collapse it.
  (Shortcut / GitHub issue types.)
- **Resolution** — select `Done` / `Won't Do` / `Duplicate` / `Cannot Reproduce` / `Superseded` on
  Issue. Status says *where* it ended; resolution says *why* — the audit trail when agents close
  issues and the human reviews after the fact. (Jira.)
- **Triage** — a new first status option on Issue (`triage`, gray, before `backlog`), the default
  landing state for agent-filed issues: a human gate on scope without reading every issue at
  creation time. (Linear.)
- **Fix Release** — relation Issue → Release. Queries "what's in this release" / "which release
  fixed it"; the open-fix-issue rollup on Release is a view-only release-readiness signal feeding
  the Release progress metric Balazs prioritized. (Jira Fix Version.)

### 6. Parked and rejected

**Parked** (valuable but not immediately; add when needed): Appetite + circuit breaker (Shape Up),
Severity-vs-priority split on Issue (Shortcut), Hill Phase (Shape Up), Bet Status / "Not Now" on
Campaign, AI check-in autofill summaries, manual health override.

**Rejected as human-team ceremony** (both research sweeps concurred): story points / velocity /
capacity / burndown, time tracking, votes/watchers, SLA rule engines, workload balancing,
requester-vs-owner, hierarchy deeper than Campaign→Sprint→SprintTask, "related"/"duplicate" typed
relations, standup/update-cadence rituals.

**Terminology adopted into the project vocabulary** regardless of fields: *appetite*, *bet* (a
campaign is bet on, not "planned"), *chore*, *at risk*, *Not Now*, *circuit breaker*.

## Consequences & implementation notes

Implemented in the same sprint as DR 029 (orchestrated by Charlie, sequenced after Brian's
Release/Issue slice lands; same coordination contract). Additional surface beyond 029's list:

- dydo currently installs only a PreToolUse hook — the **Stop hook** (turn-end detection) is new
  guard/hook infrastructure and the backbone of `needs-human`; it ships with watchdog
  reconciliation in the same slice, not as a follow-up.
- Escalation paths (raise-hand, review-round cap, gate disputes) gain a flag-write; a
  `dydo` command to lower the hand.
- The sync engine maintains per-object **last-activity** from base-snapshot diffs and exposes it as
  a date property for the staleness/health formulas.
- Provisioner learns formula properties (health, staleness render, attention) and rollup counts —
  extends 029's rollup work.
- Issue status set includes `triage`; Issue gains work-type, resolution, fix-release; SprintTask
  gains work-type, needs-human.

### Implementation notes (wave 3)

Two honest deviations from §2's health design, landed while wiring Release's part-3 inputs:

- **(a) "blocked / needs-human children LINGER → Off Track" is downgraded to At Risk** on Sprint,
  Campaign, and Release. §2 makes a child blocked or waiting on a human *for more than N days* an
  Off-Track signal, but linger-**duration** is not computable in a Notion formula — a formula sees
  only the current board state, never how long a flag has been set (no historical state, and
  `last-activity` tracks repo edits, not flag age). The honest fallback: a live blocked/needs-human
  child raises **At Risk** (via the `attention-count` and `needs-human` rollups), never Off Track.
  Off Track stays reserved for the two signals a formula *can* prove: past-end-date-and-not-done, and
  (Sprint only) a failed gate verdict.
- **(b) Release earns real date inputs via formula projection.** Release could not roll up Campaign's
  `start`/`end` directly — those are themselves rollups, and Notion rejects a rollup-of-rollup — so
  Campaign now exposes view-only formula projections `start-date` / `end-date` (`prop("start")` /
  `prop("end")`), and Release rolls those up with `earliest_date` / `latest_date`. This is the same
  FORMULA-PROJECTION workaround already proven for the numeric `needs-human-count`, extended to dates:
  a Notion formula 2.0 returns a date type, and a rollup aggregates a date-returning formula with the
  earliest/latest-date functions. The projection gives Release an honest earliest-start / latest-end,
  so its health can now return **Off Track** on past-end-date-and-not-done (previously impossible —
  Release carried no dates, so its health could never leave On Track / At Risk). Release also gains a
  `needs-human` rollup (summing Campaign's `needs-human-count` projection) feeding the At Risk branch.

## Status

Accepted (Balazs, 2026-07-03). Design record; implementation sequenced with the DR 029 sprint.
