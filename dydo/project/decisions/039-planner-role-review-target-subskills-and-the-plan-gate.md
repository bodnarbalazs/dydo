---
area: general
type: decision
status: accepted
date: 2026-07-08
participants: [balazs, Mia, Adele (relay)]
---

# 039 — Planner Role, Review-Target Subskills, and the Plan Gate

Planning becomes a first-class, gated stage of implementation work. **Planner ships as the 8th
base role.** Plan review ships NOT as a new role but as a **review target of the one reviewer
role**, carried by **subskills** — a skill-folder structure holding per-target nuances (code,
merge/sprint, plan) outside the role system. Every plan is reviewed before code is written; the
reviewed plan *is* the sprint's rows. Motivated by the [DR 037](./037-cross-vendor-dispatch-same-vendor-default.md)
addendum: with implementation routed to the fast rail, **plan quality is the main quality lever**,
and plan-review is the shift-left of the review investment — balazs: "the code-writers should be
implementing a very good plan… a plan-reviewer to review the plan… is a good investment (less
waste after the implementation review)."

## Context

- The current planner is a **skill only** (single plan file in the agent workspace, no slicing
  discipline, no sprint-task creation, no review gate, no prior-art step). Verified 2026-07-08:
  7 base roles, no `planner.role.json`.
- The docs already half-promise the role: the customizing-roles guide lists planner among base
  roles, and the orchestrator's `requires-prior` constraint names it. Dispatch does not validate
  `--role` strings (issue 0240, found when `--role planner` was silently accepted and the agent
  landed on co-thinker; related: 0237).
- The same-day exemplar: Olivia's DR-034 migration plan caught four defects pre-implementation
  (sync-crashing stem collision, silent status-vocab rewrite, frontmatter-stripping archive,
  junction-defeated worktree isolation) — each would have cost an implementation-review round or a
  landed bug. That is the class of catch plan-review buys.
- A "plan-reviewer 9th base role" was considered and **rejected by balazs**: reviewer-shaped roles
  must not multiply — "we should respect the nuances that go into the different things which need
  to be reviewed and not inject them needlessly" (into the role system).

## Decision

### 1. Planner: 8th base role

Ships with dydo. The atomic flow: chief-of-staff dispatches a co-thinker on a topic → that agent
becomes the manager → hashes out the design with the human → **switches role to planner on the
same task** (the context is ripe) → produces the plan → the plan is reviewed (§2/§3) → on green
light the same manager orchestrates the sprint (run-sprint, or dispatch — Codex or otherwise, per
DR 037). Consequence: the orchestrator role's `requires-prior: [co-thinker, planner]` constraint —
previously unsatisfiable via planner — becomes the doctrine: the agent who planned is thereby
qualified to orchestrate.

**Doctrine line:** thinking is done, a plan is made, and questions are raised BEFORE code is
written (and again after code review). Early catches are the point: unexamined assumptions,
missed options, better alternatives — e.g. an existing library that makes the implementation
redundant, hence the prior-art obligation (§4).

### 2. One reviewer role; review-target subskills carry the nuances

The reviewer role stays the **single review identity**. What differs between reviewing code, a
merged sprint seam, and a plan is real — but it is **prompt substance, not role structure**. It
lives in a **skill folder**: the reviewer `SKILL.md` as entry point carrying the shared substance
(severity calibration, verify-before-asserting, adversarial mindset, findings format), plus
**per-target reference files** — `code`, `merge/sprint`, `plan` — each holding that target's
rubric and cues. The invoking context (run-sprint stage, plan gate, dispatch brief) names the
target; the role system stays at 8 roles.

Subskills are the **natural first consumer of the portable-skill-definitions mechanism**
(`backlog/portable-skill-definitions.md`): the target reference files should be defined once,
vendor-neutral, and compiled per platform by `dydo sync` — until that lands they ride the
existing sync emit path.

**Being straight about the compiled QA agents** (balazs, 2026-07-08 — split decision). At the
2.0 pivot, sprint-auditor (né merge-auditor) and inquisitor became compiled QA agents that
`dydo roles list` hides — the system should say what they are. Resolution:

- **Sprint-auditor FOLDS into reviewer.** It *is* reviewer-role work pointed at a target
  (reviewer × merge/sprint). The separate compiled sprint-auditor agent stops being emitted;
  run-sprint's audit stage invokes the compiled reviewer agent with the merge/sprint target
  reference file. The roles list is honest by construction.
- **Inquisitor does NOT fold — it is not review-shaped.** Balazs's reasoning: the inquisitor
  *orchestrates* reviewing agents, adversarially generates hypotheses, and orchestrates
  test-writers to verify those theories — that is orchestrator-shaped QA, not "review the X".
  It stays its own emitted agent, declared as its own thing wherever the QA surface is
  documented (never presented as a reviewer variant).

**Plan-review execution — the separate-subagent rule is explicit and primary:**
**dispatch a separate sub-agent as the plan-reviewer; the plan must get fresh eyes.** Never
review a plan in the context that produced it. The plan-reviewing sub-agent (or
dynamic-workflow stage) runs the reviewer skill with the plan target, receives ONLY the plan
artifacts — never the planning conversation — and the planner must not seed the review brief
with the plan's own assumptions. A dispatched reviewer session satisfies the rule a fortiori;
for that case the reviewer role's `role-transition` constraint set gains `fromRole: planner`
(alongside the existing `fromRole: code-writer`) — constraints are role-level, not per-target:
a coarse rail; the in-session path relies on §3's verdict-artifact discipline anyway. Per the
DR 037 addendum, planning and plan-review are intelligence-critical (strong tier); the
implementer is by default the fast rail and does not plan-review.

### 3. The format and the gate — plan-as-rows, sprint-status green light

*(Q1–Q4 accepted by balazs 2026-07-08, including the `audit` stage name)*

- **The plan is a folder of records, not a workspace doc.** The **Sprint record body** carries
  the big picture: structure, ordering, isolation strategy, out-of-scope, watch-outs. Each slice
  is **one SprintTask row** (brief in the body), materialized **at plan time** — this realizes
  [DR 034 §6](./034-pm-record-taxonomy.md) "the plan *is* the rows" and drives the
  SprintTask → Sprint → Campaign rollup (trackable progress) for free.
- **The green light is the Sprint status.** Nothing is ever "promoted" or made live; the status
  is the switch. run-sprint's (and any orchestrator's) contract: consume only a sprint whose
  status shows a passed plan-review. A half-baked plan on the board is not pollution — it is
  honestly visible as under plan-review.
- **Sprint status vocabulary — the two review stages never share a word** (balazs's rule: when he
  reads `in-review` he reads *implementation* review; plan-review and implementation-review are
  different stages and must be unconfusable):

  `planning → plan-review → active → audit → done`

  `plan-review` = the plan gate (this record); `audit` = the merged-seam review at sprint end
  (the folded sprint-auditor stage, §2). At sprint altitude the word `in-review` is retired
  entirely, so it remains unambiguous at task altitude, where
  [DR 034 §4](./034-pm-record-taxonomy.md)'s `Task` vocabulary
  (`backlog → in-progress → in-review → done`) keeps `in-review` meaning exactly one thing:
  implementation review of a task. No status word means two things anywhere. (balazs's original
  sketch used `in-review` post-`active`; he accepted `audit` — "I'm good with audit as well.")
- **SprintTask `ready` acquires its meaning**: *planned, awaiting green light / pickup.*
  Rows are born `ready`; a worker flips its row `in-progress` on claim, `done` when there
  (per DR-034 §6).
- **The gate does not ride the task lifecycle.** A plan-reviewing subagent shares the manager
  session's dydo identity, so [DR 036](./036-task-approval-reform.md)'s CLI-enforced
  invoker≠assigned check would refuse a self-flip — and carving around it would gut the check.
  Instead: the plan-reviewer sub-agent (separate, per §2) writes a **verdict artifact**
  (pass/fail + findings); the manager may flip the sprint `active` only when a pass verdict
  exists. Prompting-not-code in v1, the exact precedent of DR-036's acceptance checklist ("if
  discipline erodes, escalation is a future CLI check, not more prose"). A failed verdict sends
  the plan back to the planner with findings.

### 4. The plan rubric (the `plan` target reference file; equally the planner's checklist)

A good plan:

- is **detailed enough that implementation becomes mechanical**; contains **concrete examples**;
- **names the existing patterns to follow** (and where they live);
- **cites a prior-art / library search** — performed with evidence, findings recorded even when
  rejected; the reviewer verifies the evidence rather than repeating the search;
- divides work into **disjoint slices** (parallelizable by file) that are **atomic** — each
  reviewable in one round; oversized chunks let bugs through unnoticed;
- **materializes proper sprint-task rows** (§3) so progress is trackable;
- states **dependency order and per-slice gates** (exact test/check commands);
- validates **isolation** (worktree-safe vs in-branch; junction/shared-surface hazards);
- calls out **data-shape / migration hazards** and the **rollback story**;
- carries **out-of-scope definitions, touched-files list, files-to-create, and watch-outs**
  (the surviving skeleton of the old skill format);
- writes each slice brief **self-contained to the writing-good-briefs bar** — file lists, success
  criteria, no interpretive latitude — so a fresh fast-tier implementer can execute it alone;
  **no model names in plan text** (bindings come from dydo config).

### 5. Scope

**Every plan gets reviewed — no calibration tiers.** Trivial edits get no plan at all (the
Managers Doctrine's trivial-edit exception is unchanged); anything that warrants a plan warrants
the gate.

## Implementation outline (follow-on; Managers Doctrine — all through run-sprint)

- **R1 (role + validation):** `planner.role.json` (base: true; writable: own workspace +
  `project/sprints/**` + `project/sprint-tasks/**` + `project/tasks/**`); reviewer role gains
  `role-transition fromRole: planner`. Fix dispatch `--role` validation (issues 0240 + 0237
  together). NOTE: `dydo/_system/roles/` is guard-off-limits to agents — landing needs the human
  or a guard-sanctioned path.
- **R2 (skills):** restructure the reviewer skill into the folder shape (shared SKILL.md +
  `code` / `merge-sprint` / `plan` target reference files, the separate-subagent rule stated
  primary in the plan target); rewrite the planner skill to §3/§4 (folder-of-rows format,
  prior-art step, sprint-task creation, review handoff); stop emitting the sprint-auditor agent
  (folded, §2) — inquisitor emission unchanged.
- **R3 (workflow contract):** run-sprint consumes green-lit sprint rows instead of raw arg
  slices — subsumes the "runtime → board bridge" backlog item (notion-board-followups §A);
  sprint status vocabulary per §3; run-sprint's review and audit stages point at their target
  reference files (audit stage invokes reviewer × merge/sprint).
- **R4 (docs):** role tables (workflow template), roles reference pages, customizing-roles guide
  (its planner mention becomes true), mode templates; QA surface docs updated for the §2 split —
  sprint-auditor documented as reviewer × merge/sprint, inquisitor declared as its own
  orchestrator-shaped agent; new-role surfaces analogous to the 6-surface command rule.

## Revisit when

- Verdict-gate discipline erodes → promote §3 Q3 from prompting to a CLI check.
- Plan-review rounds start thrashing (plans ping-ponging) → the rubric is miscalibrated; tighten
  examples, or split planning altitude.
- Target reference files start needing different tool profiles or bindings per target → the
  subskill shape is straining; re-open the role question with evidence.
- `portable-skill-definitions` lands → migrate the target reference files onto it (dogfood).

## Related

- [DR 034](./034-pm-record-taxonomy.md) — SprintTask rows, "the plan is the rows", status vocab.
- [DR 036](./036-task-approval-reform.md) — verifier-flips-done, checklist-as-prompting precedent.
- [DR 037](./037-cross-vendor-dispatch-same-vendor-default.md) (+ addendum) — work-split routing
  that makes plan quality the lever.
- [DR 028](./028-model-tier-abstraction.md) §5 — reviewer-tier asymmetry; with sprint-auditor
  folded, it applies to the reviewer agent across all targets, audit stage included.
- `backlog/portable-skill-definitions.md` — vendor-neutral skill definitions; subskills are its
  first consumer.
- Issues 0240 / 0237 — dispatch role validation.
- Exemplar: `dydo/agents/Olivia/plan-dr034-migration-slices.md`.
