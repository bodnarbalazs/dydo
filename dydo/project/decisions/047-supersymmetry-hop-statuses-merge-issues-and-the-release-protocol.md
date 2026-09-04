---
area: project
type: decision
status: proposed
date: 2026-09-04
participants: [balazs, Claude (Fable)]
---

# 047 — Supersymmetry, Hop Statuses, Merge Issues, and the Release Protocol

Reshapes the dydo 3 operating model where the control-flow map found its contracts disagreeing: a
captain's Issue is a Project one level down and the same Types, statuses and chain hold at both
levels; every spawn flips a status; every merge is an Issue with its own review; a captain returns a
key, never a report, and releases its Issue in one way for three reasons; the board is every hat's
inbox; the project-planner becomes the admiral's worker; the inquisition becomes an Issue and its
workflow retires. Settled in the 2026-09-03 grill over
[control-flow.md](../../understand/control-flow.md), with the facts it needed established first.

---

## Context

- The map drew every handoff from DR 045, DR 046 and the workspace standard and found sixteen places
  where the two sides of a contact expected different things: merge review had no FAIL branch, nobody
  resumed a blocked Issue, the spawned project-planner had nobody to ask for approval, the inquisition
  workflow was stale, `Blocked` was used as a status the standard forbids.
- Facts established on both hosts: a sub-agent can spawn sub-agents, to a default depth of three on
  Claude Code and to `agents.max_depth`, default one, on Codex (DYD-86 sets both); no sub-agent on
  either host can talk to the human; a finished background sub-agent wakes an idle session; on Claude
  Code the human can open a running sub-agent's transcript and steer it; a returned sub-agent resumes
  with full context when messaged.
- Linear, team Dydo, as it existed: no `Planning`, no `Waiting for Human`, seven labels, no templates,
  one human user. The standard described a target, so nothing below costs a migration.
- The human's targets, from the map's first version and his answers: several Projects in flight at
  once, one admiral each; his queue never empty and never a wall; agents never waiting on him without
  a record; AFK work running without him, HITL work in sessions he opens himself.

## Decision

### 1. Supersymmetry

A captain's Issue is a Project one level down. The same Types, the same statuses and the same chain
hold at both levels; only the map holder changes: the admiral holds the Project map and writes Issue
contracts, the captain holds the Issue map and writes lane contracts. Each divides only when the work
holds separate parts that can run at the same time, and each sends a planner ahead before dividing:
the admiral a project-planner, the captain a specifier on the parent, whose spec names the lanes.
Three Types are primary-only by convention, never by a second label set: Inquisition, the landing
Merge, and Walkthrough.

### 2. Sessions and the board

- Several admirals run at once, one top-level session per Project, each spawning captains for AFK
  Issues. A HITL Issue a captain holds, a Prototype among them, runs in a top-level captain session
  the human opens himself; a Grilling or Walkthrough under a Project runs in the admiral's own session
  with him. The chief-of-staff is optional: the bird's-eye view over the admirals.
- The board is every hat's inbox. Every wake of an admiral begins by reading its Project: commission
  every pickable Issue, blocker-cleared ones included; resume every Merge Sub-issue whose turn came.
  A sub-agent's return is a wake-up carrying an Issue key and never repeats what the record holds.
- Assignment stays the claim; every Claude session assigns as the human. His queue is therefore the
  open `Question` Issues plus the gates, never the assignee filter.

### 3. Statuses

Eleven, one set for every level, and the captain alone sets a delivery Issue's status:

| Status | Set when |
|---|---|
| `FutureFeature` | a backlog-type status: an unscheduled possibility with no Type yet |
| `Backlog` | retained with a Type, not queued: no contract yet, or one awaiting the human's go |
| `Todo` | contracted and queued; an open native blocker still prevents pickup; a `Question` in `Todo` is the human's turn |
| `Specifying` | the specifier is spawned |
| `Implementing` | the implementer is spawned, a fix hop after FAIL included |
| `Hardening` | the hardener is spawned |
| `In Review` | any reviewer is spawned, spec review included |
| `In Progress` | a record not running the chain itself: a parent while its lanes run, a wayfinding Issue, a HITL session |
| `Done`, `Canceled`, `Duplicate` | as before |

The record that runs the chain flips on every spawn; nothing else flips it. `Planning` and
`Waiting for Human` leave the standard, and Linear never had them, nor a `Blocked` status: the
native blocker relation is the only blocked state. A `Question` runs `Todo` → `Done`. The admiral
sets Project statuses; the map holder sets a wayfinding Issue's.

*Rejected:* hop labels beside a flat `In Progress`, because a label flipped four times per Issue is
a second field to drift; and no hop granularity at all, because the human reads the board and the
role at work is exactly the status: specifier, implementer, hardener, reviewer.

### 4. Types and Mode

Two Linear label groups, `Type` and `Mode`, because one-per-group is the rule Linear can enforce.
Every Issue carries one Type; Mode sits on every Type a captain holds.

| Type | Held by | Level | Closes on |
|---|---|---|---|
| `Feature` | captain | any | the outcome merged: add, improve, refactor, document |
| `Bug` | captain | any | the behaviour restored |
| `Merge` | captain | any | the merge review PASS |
| `Enablement` | captain | any | the condition true, with `wizard` for steps only the human can do |
| `Inquisition` | captain | primary | Bugs filed and the record written |
| `Prototype` | captain | any | the human's verdict on the Issue |
| `Question` | map holder | any | the human's answer on the Issue |
| `Research` | map holder | any | cited findings on the Issue |
| `Grilling` | map holder | any | shared understanding recorded |
| `Walkthrough` | map holder | primary | the human has inspected what landed |

`Improvement` folds into `Feature`; `Needs human` goes; `FutureFeature` is a status, not a label. A
Sub-issue carries its parent's Type: a Bug's lanes are Bugs. Colours are fixed in the standard. One
Linear Issue template per Type carries the default Sub-issue map and the spec's shape, its body
specified in the standard so an agent can create it.

### 5. One chain, sized by the spec

[specifier] → [implementer] → [hardener] → [reviewer] runs on every record that carries the chain, at
every level. Every delivery Issue has a specify hop, a docs Issue included, so `Contract:` always
pins the specify commit. The spec declares a hop empty and the captain skips it: a Prototype has no
hardener and the human is its review; a Merge hardens only when resolution refactored; a Bug's
reproduction hardens nothing and its fix everything. The captain maps its Issue into Sub-issues at
its own discretion from the Type template's default: Bug (reproduce or identify, then fix),
Inquisition (parts and lenses swept, hypotheses, proofs, Bugs filed), Merge (one operation). A Bug's
reproduction is a scenario when the defect shows at the product's boundary, else the implementer's
red test through diagnosing-bugs, which the spec carries as a gate; a Bug the inquisition filed
arrives with its red test at a commit. The specifier gains one resource per kind whose spec differs:
Bug, Merge, Inquisition. Left open: whether a Prototype's winning code is submitted or its branch
kept until the delivery Issue implements it; until settled, the branch is kept and linked from the
Issue.

### 6. Merges are Issues

Every merge operation is an Issue of Type `Merge` with its own captain-directed chain and its own
merge review; no review at one level substitutes for another.

- Lanes into their parent: one Merge Sub-issue per lane, in order, like every other merge.
- A primary Issue into the feature branch: the primary's final Sub-issue, run by its own captain. The
  admiral serializes by wiring each Merge Sub-issue blocked by the previous one in plan order, so
  merges land PR by PR, never batched. The admiral does no git.
- The feature into main: the Project's landing, a top-level Merge Issue whose crew merges main into
  the feature, runs the gates and obtains the merge review that proves acceptance; the human clicks.
- After it, a Walkthrough Issue. Findings reopen the lap inside the same Project: the feature branch
  is re-cut from main under the same name, then fixes → merge → walkthrough, with an Inquisition only
  when the human confirms one. The Project closes when a walkthrough finds nothing.
- Merge review FAIL: an integration defect is a fix hop inside the Merge Issue; a defect in the
  landed work is reverted inside the Merge Issue, which closes `Canceled` with the reason, and the
  source Issue returns to `Implementing` with the findings. Once a later merge already depends on
  the failed one, a fix Issue follows it instead of a revert.
- An atomic Issue merges into main through its own final Merge Sub-issue, the same way.

*Rejected:* batching parallel PRs into one merge, because a batch hides which Issue broke a seam and
a FAIL then reverts several; and the admiral merging by hand, because the admiral directs and the
crew produces, and a conflict is code.

### 7. The captain's tenure and the release protocol

A captain returns one line to its spawner, a wake-up: `done <key>` or `released <key>: <reason>`.
The record holds everything else.

- **Two steps.** The captain returns `done: PR ready` when the PR carries its PASS block. When the
  Merge Sub-issue's blocker clears, the admiral resumes the same captain with one word, or
  commissions a fresh one from the record, and it returns `done: merged`.
- **Release.** The captain pushes the branch, removes its worktree, sets the parent to `Todo`,
  unassigns, and wires the
  blocker when there is one. The resume point is the last hop's SHA, which it posts at every hop, so
  a dead session leaves one too. The next captain resumes from the branch.
- **Three triggers.** A blocker the captain cannot clear; the human's takeover, requested through the
  admiral, or on Claude Code by opening the captain's transcript and steering it there; a session
  that dies, which the admiral treats as a release without the push.

### 8. Planning

- The **project-planner** is the admiral's worker, as the specifier is the captain's: it writes the
  plan file and the first pickable Issues and returns the commit; the admiral owns the review loop
  and puts approval to the human in its own session, which no sub-agent could. The upstream
  `to-tickets` rules, tracer bullets and blocking edges, fold into its Issue-writing step.
- **`to-project`**, a human command adapted from upstream `to-spec`, files a co-think as a Linear
  Project with no interview: title, summary, the intent as description with problem, solution,
  decisions taken and out of scope, links to the Decision Record, the glossary entries and the source
  FutureFeature when one exists, status `Backlog`, and no Issues. An admiral may take over a Project
  at any stage, and its first act is always reading the record; the co-thinker's session graduating
  to admiral is one entry among others, not the preferred one.
- The plan file stays in Git, committed on main before review, and the Linear Project description
  holds the live map; the approval in the admiral's session
  is its gate, as with a Decision Record, and the feature branch is cut afterwards so the governing
  SHA is an ancestor of every Issue branch. The plan review is capped at two rounds; the second FAIL
  goes to the human with the findings as the choice. The rubric's purpose is to catch what was missed
  and would cause problems, not to polish.
- An atomic Issue stays the co-thinker's own, five fields on the record.

*Rejected:* the plan as a Linear Document, because it has no citable commit and the governing SHA is
what every Issue and reviewer pins.

### 9. The inquisition is an Issue

Type `Inquisition`, a captain, `inquisition/<slug>` cut from the integrated feature SHA and never
merged, deleted at `Done`. The admiral proposes by filing it in `Backlog` with the parts, the lenses
and the cost; the human confirms by moving it to `Todo`. Inquisitors are read-only and refute their
own catches; their second product is the hypothesis list; each hypothesis goes to a proof-only
implementer whose only output is a test, red if the hypothesis holds; the captain deduplicates and
files one Bug per confirmed problem under the Project, with the feature as base branch and the red
test's commit as reproduction; the docs-writer writes the record into `dydo/project/inquisitions/`.
There is no PASS or FAIL: it files. The `Workflow` concept retires with `workflow-inquisition.js`
and the compiler's workflow emission, since nothing else used them.

### 10. Contract corrections carried from the map

Findings 2, 3, 4, 5, 8, 9, 11 and 16 of the map's first version, applied without objection. The
reviewer's brief names its four fields: rubric, `Contract` at its governing SHA, candidate SHA,
base SHA. The implementer and hardener read the review block when a FAIL sent them. The specify SHA
is posted like every hop's. A plan review's block is a Linear Project update; a merge review's block
lives on its Merge Issue. Only the admiral creates Project-level Question Issues; a captain creates
local Wayfinding Sub-issues under the scope rule of the standard. HITL judgment reaches the human as
a `Question` in `Todo` that the chief-of-staff surfaces on request; nothing is sent through the
chief-of-staff.

## Consequences

- Supersymmetry enters the glossary; the map's section 3 says it in one sentence.
- The standard is rewritten: eleven statuses, ten Types in one `Type` group and `Mode` in its own,
  with colours, the Issue templates. Setting the workspace up from it, statuses, labels and
  templates, is its first Enablement Issue: an agent does what the API allows, the human the rest.
  Priority stays an ordering hint, blockers carry sequence; Initiatives, Cycles and Milestones stay
  optional and untouched.
- Templates: the working-tree contract's merger column becomes the captain's Merge Sub-issue, its
  Claim row sets `Specifying`, and it gains the `inquisition/<slug>` and `prototype/<name>` rows;
  admiral, issue-captain,
  project-planner (now `emit: agent`, no delegation), specifier and its three resources,
  implementer, hardener, reviewer, docs-writer, inquisitor, chief-of-staff, co-thinker and wayfinder
  carry the sections above; `to-project` and `wizard` are imported; the inquisition workflow, the
  compiler's workflow emission and the `Workflow` glossary entry retire; `types.json` follows the
  Type set.
- DR 046 is accepted as written and amended here, not folded in.
- DYD-86 sets the nesting depth on both hosts. Whether AGENTS.md reaches a Codex sub-agent stays the
  empirical check DR 045 §10 already requires.

## Supersedes and amends

Amends DR 045: §1 the Audit row becomes the Inquisition Issue and the Coordinate row's serial merges
become Merge Issues; §2 project-planner is a worker, `Workflow` is none, human commands gain
`to-project`, methods gain `wizard`; §3 merge review runs inside every Merge Issue and tier three is
the Inquisition Issue; §4 the captain creates local Wayfinding Sub-issues and the admiral the
Project-level Questions, and nothing is sent through the chief-of-staff; §6 there is no `Blocked`
status; §7 inquisition confirmation is `Backlog` → `Todo`, the landing is a Merge Issue, and the
walkthrough is an Issue; §8 the captain merges its own Issue through a Merge Sub-issue; §9
`to-project` is explicit-only. Amends DR 046: §2 every delivery Issue has a specify hop and the
specifier carries per-kind resources; §3 hops are also statuses. Amends DR 044's status and label
list through the rewritten standard.

---

## Affects

- [Control Flow](../../understand/control-flow.md)
- [Work Model](../../understand/work-model.md)
- [Linear Issue Lifecycle](../../understand/task-lifecycle.md)
- [Linear Workspace Standard](../../reference/linear-workspace-standard.md)
- [dydo Glossary](../../reference/dydo-glossary.md)
- [Working-Tree Contract](../../guides/working-tree-contract.md)
- [Harmonize the skill system](../plans/dydo-3-skill-harmonization.md)
- [DR 045 — Flow Map, Hats and Workers, Review Tiers, and the Working-Tree Contract](./045-flow-map-hats-review-tiers-and-working-tree-contract.md)
- [DR 046 — Executable Specifications, the Specifier, and Commit-Addressed Hops](./046-executable-specifications-specifier-and-commit-addressed-hops.md)
