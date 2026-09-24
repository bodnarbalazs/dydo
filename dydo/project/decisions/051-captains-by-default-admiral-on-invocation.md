---
area: project
type: decision
status: accepted
date: 2026-09-24
accepted: 2026-09-24
participants: [balazs, Claude admiral]
---

# 051 — Captains by Default, the Admiral on Invocation

The issue-captain is the default officer, and the admiral exists only when the human invokes it.
With no admiral, the human holds the Project map in their own session with `wayfinder`, and the map
holder charts and grills. The project-planner is removed. The Linear Project and its Issues are the
plan, and the one-Wayfinding-Issue-per-session rule binds only the sessions the human drives. A new
user-invoked `to-issue` skill writes pickable Issues. Settled by the human in the
[DYD-252](https://linear.app/bodnar-balazs/issue/DYD-252/grilling-reconcile-the-atomic-pocock-stateless-model-with-the)
grilling, 2026-09-23/24.

---

## Context

Two philosophies ran side by side in the 3.0 skill tree:

- **Atomic and stateless**, from Matt Pocock's original design. `wayfinder` charts and resolves one
  Wayfinding Issue per session, sessions are fresh, and everything lives on the record.
- **Orchestrator**, brought in to manage agents with agents. Officers hold a Project or Issue to the
  end in standing sessions ([DR 050](./050-officers-crew-and-skills-hats-retired.md)), and the
  admiral charts through a spawned project-planner
  ([DR 047](./047-supersymmetry-hop-statuses-merge-issues-and-the-release-protocol.md) §8) that
  never asks the human.

The [skill-harmony inquisition](../inquisitions/2026-09-23-skill-harmony.md) (DYD-235) filed
[DYD-243](https://linear.app/bodnar-balazs/issue/DYD-243/wayfinders-role-bindings-drift-from-the-30-roles-and-standard-planner),
which showed where the two collide inside `wayfinder`:

- **Who charts.** `wayfinder` has the Project Planner chart the first map, and its Chart steps grill
  the human. The project-planner skill says a spawned planner never asks the human.
- **One per session.** `wayfinder` never resolves more than one Wayfinding Issue per session. DR 047
  §2 runs a Grilling under a Project in the admiral's own standing session.
- **Informing the admiral.** `wayfinder` has a captain report a local resolution to the admiral. The
  issue-captain returns one line to its spawner, and everything else lives on the record.
- **Enablement and Task.** `wayfinder` defines both more narrowly than the
  [Linear Workspace Standard](../../reference/linear-workspace-standard.md) does.

The human's starting position was that the admiral charts. The grilling made the human the default
map holder instead, with the admiral as an optional right hand.

## Decision

1. **Truth.** The Linear record is the default truth, and the latest word wins. An agent that finds
   a conflict raises it. The human's live instruction overrides the record, and the agent writes the
   result back to it.
2. **Captains are the default.** An issue-captain owns its Issue and runs its crew, a writer and a
   reviewer, automatically. It takes an adjacent Issue only on the human's word.
3. **The admiral is optional.** It exists only when the human invokes it, as a right hand for
   throughput, for example AFK work overnight. It never takes the frontline from the human, who can
   take the map back at any time.
4. **The map holder.** When no admiral is invoked, the human holds the Project map, working in their
   own session with `wayfinder`. Upstream `wayfinder` calls this the dev driving the map. An invoked
   admiral holds it until the human takes it back. The Project description names the current holder.
5. **Charting.** The map holder charts and grills. The project-planner is removed.
6. **Plans.** The Linear Project and its Issues are the plan. A repository plan file is written only
   for a cross-cutting architecture contract, by the map holder, and it keeps its
   `reviewer(project-plan)` review.
7. **Local decisions** live on the record only. An invoked admiral monitors the record, or the human
   tells it; a captain sends it no separate report.
8. **One Wayfinding Issue per session** binds the sessions the human drives. An admiral is exempt.
   Upstream's reason is sizing: one ticket is one session of about 100K tokens.
9. **Terms.** "Project" means a Linear Project. Enablement and Task mean what the standard defines:
   Enablement is access, environment, credentials or material other work needs, and Task names a
   captain-held Issue's role on a map.
10. **`to-issue`.** A new user-invoked skill, a light, near-verbatim adoption of upstream
    `to-tickets`. It names Linear, lists the valid Issue Types with their definitions, and writes
    pickable Issues: Outcome, Type, Mode, blockers, settled paths, `Todo`. The captain sharpens the
    contract when it claims an Issue, and asks a Question if more is needed, or for a Grilling when
    the work is complex. `to-project` points to `to-issue` or to an invoked admiral instead of
    assuming an admiral.

## Consequences

- **Gained:** each DYD-243 collision has one answer. Decisions 4 and 5 settle who charts, 8 the
  session limit, 7 the report to the admiral, and 9 the two definitions. A Project moves without an
  admiral: the human charts it, and captains deliver its Issues.
- **Accepted:** the project-planner skill and every reference to it go. A Project no longer gets a
  repository plan file by default; the `project-plan` rubric stays for the map holder's plan file.
- **Adoption:** [DYD-254](https://linear.app/bodnar-balazs/issue/DYD-254/dr-051-and-its-adoption-captains-by-default-the-admiral-on-invocation)
  carries this record through the skills and documents: `wayfinder`, the admiral, the issue-captain,
  `to-project`, the new `to-issue`, the roles READMEs, the standard, control flow, the glossary, the
  working-tree contract and the entry point.

---

## Supersedes and amends

Amends [DR 045](./045-flow-map-hats-review-tiers-and-working-tree-contract.md):

- §1, the "Chart and plan a Project" row: the map holder charts with `wayfinder`; there is no
  project-planner.
- §2: the Project Planner paragraph is removed, and `wayfinder` is used by the map holder, not by
  the Project Planner and the admiral.
- §4: the map holder, not a Project Planner, creates and wires the blocking Questions while starting
  the map.
- §5, Project planning: the Linear Project and its Issues are the plan; a plan file is written only
  under decision 6.

Amends [DR 047](./047-supersymmetry-hop-statuses-merge-issues-and-the-release-protocol.md):

- §1: the Project's map holder is the human, or an admiral the human invoked, and nobody sends a
  project-planner ahead.
- §2, first bullet: an admiral runs only when the human invokes one, and a Grilling under a Project
  runs in the map holder's session.
- §8: the project-planner bullet is removed, and upstream `to-tickets` moves into `to-issue`; the
  `to-project` bullet ends as decision 10 says; the plan-file bullet holds only for decision 6's plan
  file, written by the map holder.
- §10: the map holder, not only the admiral, creates Project-level Question Issues.

Amends [DR 050](./050-officers-crew-and-skills-hats-retired.md):

- Decision 1: the admiral holds a Project only when the human invokes it, and project-planner leaves
  the crew.
- Decision 4: `skills/roles/crew/` loses project-planner, and `skills/productivity/` gains `to-issue`.

No earlier record states the one-Wayfinding-Issue-per-session rule; it lives in `wayfinder`'s
Invocation section, and decision 8 sets its scope.

## Affects

- [Control Flow](../../understand/control-flow.md)
- [Linear Workspace Standard](../../reference/linear-workspace-standard.md)
- [dydo Glossary](../../reference/dydo-glossary.md)
- [Working-Tree Contract](../../guides/working-tree-contract.md)
