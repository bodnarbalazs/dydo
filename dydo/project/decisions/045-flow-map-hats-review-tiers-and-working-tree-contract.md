---
area: project
type: decision
status: accepted
date: 2026-08-30
accepted: 2026-08-30
participants: [balazs, Claude (Fable)]
---

# 045 — Flow Map, Hats and Workers, Review Tiers, and the Working-Tree Contract

Fixes the operating model the harmonized dydo 3 skill system compiles to: one flow map every agent can
place itself on, a taxonomy of hats, workers, methods and commands, three review tiers with a
regex-checkable review block, question Issues as the fog-clearing unit, planning at two resolutions, an
escalation ladder with a precedence order, the human's exact gates, and a working-tree contract so
parallel work never tangles. It was settled in the 2026-08-30 co-think that reviewed Codex's skill
restoration (Project "Restore skill craftsmanship") and found it clean but unharmonized.

---

## Context

- The restoration removed sediment and gave every role one shape, but the result does not route:
  descriptions state identity rather than triggers, no skill names the flow it belongs to, leading
  words that anchored behaviour ("YOU SHALL NOT PASS") were sanded into taglines, skills describe prose
  returns while workflows enforce JSON schemas, and four mechanical defects shipped — `dydo/index.md`
  is off-limits to reads although every entry prompt orders agents to read it; `Commands/SyncCommand.cs`
  drops `## Must-Reads` from skill-only roles so admiral skills compile without their context pointers;
  `workflow-run-sprint.js` cites `references/merge-sprint.md` where the compiler emits
  `resources/merge-sprint.md`; and the locked glossary still defines a Waypoint ontology the rebuilt
  Wayfinder no longer uses.
- Models now orchestrate sub-agents well ad hoc on both hosts, so hard-coded dynamic workflows are no
  longer the only way to get a disciplined loop; a precise skill plus one mechanical gate at the
  boundary the human sees can carry the same guarantee.
- The human is both the bottleneck to minimize (nothing reaches him that an independent agent has not
  reviewed) and the participant to keep in flow (co-thinking, planning, understanding, harmonizing).
  Drift between agents that each invent their own branching or escalation habits costs his attention
  disproportionately.

## Decision

### 1. The flow map

Every skill names the stage it serves. Sessions change hats along the map; the hats are not session
types.

| Stage | Hat | Uses | Output | Gate |
|---|---|---|---|---|
| Think | co-thinker | grilling, domain-modeling, research | ripe intent; a DR when the ADR test passes | — |
| Chart and plan a Project | project-planner, using wayfinder | grilling, research, prototype | first Project map; blocking question Issues | reviewer (project-plan) → human approval |
| Plan an Issue | issue-captain, using issue-planner | codebase-design, writing-good-briefs | just-in-time Issue route | optional reviewer (issue-plan) |
| Implement | issue-captain | code/test/docs-writer as optional workers, tdd, diagnosing-bugs | Issue branch, PR into the feature branch, evidence on the Issue | reviewer PASS (review block) |
| Coordinate *(optional)* | admiral | wayfinder, research | N Issues in flight, serial merges, plan amendments | merge review after every merge |
| Audit *(rare)* | inquisition workflow | inquisitors × lenses, reviewer as judge, docs-writer | audit + assimilation brief | human confirms before it runs |
| Land | human | walkthrough | feature → main | human's hands |
| Harmonize | human on main | walkthrough, teach | improvements; a new feature branch when needed | not a gate: main is THE state |

Cross-cutting at any stage: chief-of-staff (attention triage for the human, never delivery),
self-improvement, writing-for-agents, diagnosing-bugs, bro.

### 2. Taxonomy

- **Hats** (what a session is doing now): co-thinker · project-planner · issue-captain · admiral · chief-of-staff.
- **Workers** (spawned execution roles, `emit: agent`): code-writer · test-writer · docs-writer · reviewer ·
  issue-planner · inquisitor · research.
- **Methods** (model-invoked reference and procedure used inside other skills): grilling · wayfinder ·
  domain-modeling · codebase-design · diagnosing-bugs · prototype · writing-for-agents ·
  self-improvement.
- **Human commands** (explicit-only): grill-me · bro · handoff · walkthrough · teach ·
  improve-codebase-architecture.
- **Workflow:** inquisition, and nothing else.
- **Rubrics** (reviewer resources): code · tests · docs · project-plan · issue-plan · merge.
The **Project Planner** is a hat and also compiles as a spawnable agent (`emit: agent`,
`delegates: true`), bound to the strong tier. It owns the low-resolution Project map and its mandatory
`reviewer(project-plan)` loop through PASS and human approval. The **Issue Planner** is a spawned
worker (`emit: agent`), also bound to strong, that makes one Issue mechanical and returns its plan and
review recommendation to the Issue Captain. Neither role accepts the other's target.

Renames and moves: `orchestrator` retires; **admiral** owns one Project's delivery from plan approval
to a human-landable feature branch. One **Issue Captain** owns each Issue end to end: the Issue
contract is its destination, its Issue-resolution plan the route, and spawned Issue Planners, writers, and
independent reviewers its crew. Admirals coordinate captains; captains direct crews; neither role
authors production changes or reviews its own candidate.
It also compiles as a spawnable agent (`emit: agent`, `delegates: true`) so an admiral can keep N
Issues in flight as sub-agents; a spawned Issue Captain returns `blocked` with its question instead of
waiting on the human. The same skill serves both spawners; the Issue is the shared state and
assignment is the claim. **wayfinder** stops
being an identity and becomes a method consumed by the Project Planner (charting) and the admiral (working the
map). `run-issues` retires; its loop becomes the Issue Captain's completion criterion. Imported from
mattpocock/skills at `6654f6b6`, adapted: diagnosing-bugs, research, codebase-design, domain-modeling,
prototype, handoff, teach, improve-codebase-architecture, and `SKILL-MECHANICS` as a writing-for-agents
resource. New and dydo-native: Issue Captain, walkthrough, the working-tree contract guide.

### 3. Three review tiers and the review block

1. **Issue review** — reviewer with the target rubric: code, tests, or docs before a production merge;
   project-plan before Project approval; issue-plan before production only when the Issue Captain
   requires it.
2. **Merge review** — reviewer with the `merge` rubric after *every* merge: a mechanical spot check
   that scales with what landed; at the final feature merge it also proves the plan's acceptance
   criteria. The former `merge-sprint.md` is this rubric, renamed and narrowed; lens-hunting moves to
   the inquisitor.
3. **Inquisition** — rare, fan-out across lenses, adversarial verification, reviewer as judge using
   the `merge` rubric at full scale, docs-writer assimilation. Proposed by the admiral with scope and
   cost; runs only after the human confirms, enforced by a `confirmed` argument the workflow refuses
   to run without. Its purpose is to catch what got through, not to prove zero defects.

The reviewer's return is a fixed **review block**: rubric, reviewer label and model, candidate and base
SHA, verdict PASS/FAIL, gates rerun with results, findings as file:line → consequence → correction.
The Issue Captain's own return has a slot only this block can fill. The block is posted as a comment on
the Linear Issue and pasted into the PR body; a guard nudge on `gh pr create` checks for it at warn
severity, escalating to block only if discipline erodes (DR 042's rule). Independence of *context* is
the requirement; same-vendor review with the reviewer bound to the strong tier is acceptable, and the
block records the model so cross-vendor review remains observable later.

### 4. Questions are Issues; decisions are DRs

A **question Issue** (Linear label `question`, body `## Question`) is an open question that blocks
named planning or implementation work and is too big or too uncertain to settle inline. It records
why it blocks and the homework already done. Its resolution is an *answer* posted on the Issue; small
preferences stay as spec detail on the implementation Issue, and a thought that blocks nothing stays
fog rather than becoming an Issue. The word *decision* is reserved for Decision Records: an answer
graduates to a DR only when it is hard to reverse, surprising later, and the result of a real
trade-off. Issues carry questions and work; DRs carry decisions; the two are linked, never copied.

**Fog** is the leading word for unknown unknowns. The rule is *fog → discovery → question Issue*: an
agent in fog first runs bounded discovery through the DR index, Project plan, Issue links, glossary,
code, and tests. Only a precise blocker still unanswered after that homework becomes a question Issue.
A Project Planner creates and wires these while starting the map. During Issue work, an Issue Planner or
worker returns the prepared hand-raise to the Issue Captain, who records and wires it and returns
`blocked` plus its key to the admiral. The admiral may create new questions directly as forward motion
clears fog, routes AFK homework to agents, and sends only remaining HITL judgment through
chief-of-staff to the human. The filing test is Grilling's sentence: facts are the agent's job;
choices are the human's. Native blocking does the pickup.

### 5. Planning at two resolutions

- **Project planning** — low resolution: destination, scope, acceptance, architecture-level design, the first
  few pickable tracer-bullet Issues with full contracts (expand–contract for wide refactors), rough
  later bearings, ordering and isolation, watch-outs, and — when foggy — `## Not yet specified` plus
  blocking question Issues instead of a pretended-complete route. Perfect plans are fiction: the
  approved plan fixes the destination, not every turn. The admiral uses wayfinder as fog clears to
  create, split, drop, or resequence Issues and records dated amendments. Normal map refinement inside
  the approved destination needs no new review; changes to destination, scope, acceptance criteria,
  or governing architecture do.
- **Issue planning** — high resolution, just in time: files to touch, the pattern to copy with its path,
  steps, edge cases, exact gates — until implementation contains no hidden decisions. Authored in the
  parent Issue or direct lane Sub-issue by a spawned `issue-planner` at the Issue Captain's direction,
  then implemented by delegated writers and reviewed with their code. The Issue Planner recommends whether
  route risk warrants a separate `issue-plan` review; the Issue Captain decides and uses it sparingly
  before production for hard-to-reverse or high-blast-radius work.

Every implementation Issue carries as required fields: outcome, owned paths, blockers, exact gates,
base branch.

### 6. Escalation ladder and precedence order

Worker → Issue Captain → admiral → human. Agents resolve operational conflicts themselves by this
precedence: the human's live instruction > DR > reviewed Project plan at its governing commit > Issue
contract > coding standards > existing code. The human is reached only for a conflict with a DR (which
is truth, or is the DR obsolete?), live external state agents cannot coordinate, or authority the
contract cannot supply. Raising a hand means a comment on the Issue and, when blocked, a question
Issue wired as blocker with the Issue moved to Blocked — never silent waiting. A fifth consecutive
review FAIL on the same candidate is itself an escalation: stop looping and raise. The retired
`run-issues` workflow enforced this cap in code; the Issue Captain and admiral carry it as prose.
There is no "PASS with notes" — a note is a finding, and a finding is a FAIL; the cap, not a
softened verdict, is the relief valve.

### 7. The human's gates

Project-plan approval; HITL question Issues; escalations that survive the ladder; inquisition confirmation;
the feature → main merge. Harmonization happens on main afterwards and is not a gate. Atomic Issues
(no Project) branch from main and are merged by their Issue Captain after Issue review and merge review.

### 8. Working-tree contract

One guide, `dydo/guides/working-tree-contract.md`, pointed at by issue-captain, admiral, chief-of-staff
and the Issue Planner, so no agent invents its own habits:

- At plan approval the admiral (or the human when there is no admiral) opens the feature: creates
  `feature/<project-slug>` from main, writes the wayfinder map into the Project description, confirms
  every Issue carries its base branch. Only then are Issues pickable.
- Assignment is the claim. An Issue branch is `DYD-123-<slug>` off the feature branch (off main for
  atomic Issues); the key in the name lets Linear's GitHub integration attach branch and PR.
- Worktree location is host-managed; base SHA, branch and worktree path are posted on the Issue
  before the first edit. One writer per worktree; commits touch owned paths only.
- The PR targets the feature branch and carries the review block. After merge the Issue Captain deletes
  its worktree and branch. The chief-of-staff sweeps orphans on its board-hygiene pass.

### 9. Invocation policy

Explicit-only (`invocation: explicit` → `disable-model-invocation` / `allow_implicit_invocation:
false`): chief-of-staff, admiral, grill-me, bro, handoff, walkthrough, teach,
improve-codebase-architecture. Everything else is model-invoked with a description that carries
trigger branches, per writing-for-agents. Descriptions state *when to reach for the skill*, not what it
is; explicit-only descriptions are a punchy human-facing line.

### 10. Guard and compiler

- A **protected tier** joins off-limits: any tool may read, no tool — Bash included — may write or
  delete. Members are dydo's own system files only: `dydo/index.md`, `dydo/files-off-limits.md`,
  `dydo.json`. `CLAUDE.md`, `AGENTS.md` and harness config files stay outside the guard — the
  harness owns its own defensive measures, and off-limits keeps its original meaning of files agents
  must not even read (secrets). `dydo/_system/**` stays fully off-limits. This unblinds agents to
  their orientation without stretching the guard beyond dydo's own surface; who may *edit* the entry
  points is an ownership rule, not enforcement.
- `dydo sync` keeps `## Must-Reads` in the compiled body of every role and rewrites its links to
  resolve from the emitted skill folder; `{{include:extra-must-reads}}` therefore works again for all
  roles. The spawnable `project-planner` and `issue-planner` are bound to the strong tier. The `merge-sprint` resource is renamed
  `merge`.
- Compiled agent definitions are thin identity wrappers over their skill, and the compiler must make
  the skill actually reach the spawned agent. Verified against the Claude Code documentation
  (2026-08-30): a custom subagent receives the full `CLAUDE.md` hierarchy; a `tools:` allowlist that
  omits `Skill` gives no guarantee that skill descriptions or the Skill tool are available; the
  `skills:` frontmatter field preloads a skill's full content at startup and cannot preload an
  explicit-only skill. Therefore every `emit: agent` role compiles with `skills: [<name>]` and the `Skill` tool,
  `delegates: true` alone grants the `Agent` tool, worker skills stay model-invocable, and links to a
  skill's `resources/` are rewritten to the host's emitted path so a preloaded reviewer can `Read`
  its rubric. Codex has no official subagent documentation;
  whether a `.codex/agents/*.toml` agent receives `AGENTS.md` and can load `.agents/skills` is
  verified empirically in the build Project before any Codex worker is relied on.

### 11. Voice and vocabulary

Leading words return, one strong anchor per skill; a generic tagline is a no-op and is deleted.
`writing-for-agents` governs every prompt file. The locked glossary retires Wayfinding map, Waypoint
and the Waypoint-defined Frontier; it adds fog, frontier (the open, unblocked, unassigned question
Issues), question Issue, review block, working-tree contract, hat, worker, method. Tests prove
structure, invocation metadata and role boundaries, never prose.

## Consequences

- Twenty-six skills, one workflow and six rubrics. The count is acceptable
  only because the entry prompt carries the flow map and `dydo/index.md` the taxonomy; routing is a
  first-class deliverable, not documentation.
- The human clicks exactly one merge per feature and reads a walkthrough first; everything else
  reaches him agent-reviewed or not at all.
- Cross-vendor adversarial review is deferred; a cron-driven cross-vendor reviewer is a FutureFeature
  candidate once stateless spawning is routine.
- The system described here does not yet exist. The Project that builds it runs hands-on under the
  current tooling; its own plan must not assume issue-captain, admiral or the protected tier are live.

## Supersedes and amends

Amends DR 026 (Tier-1 managers doctrine): hats replace fixed tiers, and the Issue Captain is a
first-class top-level hat that delegates. Amends DR 041's interim "how work runs" section: workers
are host-native sub-agents of an Issue Captain, not `codex exec` sessions the human babysits. Amends DR
042: slice files become `issue`-resolution plans in the Linear Issue; the no-code-without-reviewed-intent
rule stands. Amends DR 044: Waypoint is retired rather than optional; the three review tiers replace
the single "integrated audit" phrase; question Issues are the fog-clearing unit.

Amended by [DR 046](./046-executable-specifications-specifier-and-commit-addressed-hops.md) (2026-09-02): the worker chain becomes specifier · implementer · hardener, the `issue-plan` rubric becomes `spec`, and the review block gains its Contract line.

---

## Affects

- [Work Model](../../understand/work-model.md)
- [Linear Issue Lifecycle](../../understand/task-lifecycle.md)
- [Orchestration Pitfalls](../../guides/orchestration-pitfalls.md)
- [Writing Good Briefs](../../guides/writing-good-briefs.md)
- [Customizing Roles](../../guides/customizing-roles.md)
- [dydo Glossary](../../reference/dydo-glossary.md)
- [Restore skill craftsmanship](../plans/dydo-3-skill-craftsmanship-restoration.md)
- [DR 044 — Linear-Canonical PM and the dydo Knowledge Boundary](./044-linear-canonical-pm-and-dydo-knowledge-boundary.md)
