---
area: reference
type: reference
---

# dydo Glossary

The locked vocabulary for dydo's Linear-native work model, durable knowledge, and agent execution.
Project-domain terms belong in the separate [glossary.md](../glossary.md).

## Linear work graph

- **Initiative** — an optional workspace-level goal spanning independently meaningful Projects.
- **Project** — a bounded product or technical outcome owned by one Linear team. The Linear Project
  and its Issues are its plan; a cross-cutting architecture contract links one reviewed repository
  Project plan.
- **Milestone** — an optional meaningful checkpoint inside a Linear Project.
- **Issue** — the only actionable tracked work item. Status, priority, assignment, blockers, updates,
  and current execution evidence live in Linear.
- **Task** — the role a captain-held Issue plays on a Wayfinder map; a name for that role, not a
  separate Linear Type.
- **Sub-issue** — optional decomposition when child Issues need independent tracking.
- **Cycle** — an optional team capacity timebox, orthogonal to Projects.
- **Label** — restrained cross-cutting routing metadata, never a shadow work-type hierarchy.

## Fog and Question Issues

- **Fog** — the unknown unknowns: relevant uncertainty not yet sharp enough to state as a question.
  The rule is *fog → discovery → Question Issue*. Search the Decision Records, the Project plan, the
  Issue's own links, the glossary, and the code first; file only what that leaves open.
- **Question Issue** — a Linear Issue of Type `Question` in `Todo` carrying one human judgment that remains
  after discovery. It records the homework, options, recommendation and named waiters, and resolves
  into an **answer** on the Issue. The captain creates local Sub-issues; the Project's map holder
  creates Project-level Questions.
- **Frontier** — the open, unblocked, unassigned map Issues: the edge of what a Project knows.
  Linear's own blocking relations and assignment render it; there is no separate navigation object.

## Durable knowledge

- **Decision** — a choice recorded in a Decision Record because it is hard to reverse, surprising
  later, and the result of a real trade-off. Issues carry questions, Decision Records carry
  decisions, and the two are linked rather than copied.
- **Project plan** — a reviewed repository contract the map holder writes only for cross-cutting
  architecture work, at low resolution: destination, acceptance, architecture, and an Issue
  map. Its `linear-project` URL is provenance, not synchronization.
- **FutureFeature** — an unscheduled strategic possibility recorded as a Linear Issue, distinct from
  a generic idea or delivery contract. It stays in `FutureFeature` until the human promotes or cancels it.
- **Assimilation brief** — the durable account of what changed, what was learned, and what remains.

## Roles and skills

- **Skill** — an authored native skill folder: methodology and resources any session loads when the
  work needs it. A skill that carries an identity is a role; every other skill sorts by subject into
  `engineering` (the craft) or `productivity` (thinking, writing and moving work along with the human).
- **Role** — a skill that carries an identity: an officer or crew.
- **Officer** — a role that holds something and never switches: the issue-captain, the default
  officer, holds an Issue; an admiral, only when the human invokes one, holds a Project; the
  chief-of-staff holds the board. A session or agent has at most one officer role; a captain never
  becomes an admiral or the reverse.
- **Map holder** — who holds a map and charts it: for a Project, the human, or an admiral the human
  invoked until the human takes the map back, as the Project description names; for an Issue, its
  captain.
- **Crew** — a role spawned for one bounded job that holds nothing and returns its result to whoever
  sent it: code-writer, docs-writer, reviewer, inquisitor, research, scout.
- **Agent** — a native-platform instance of a spawned role: a crew member, or an Issue Captain that
  the human's session or an invoked admiral keeps in flight.
- **Method** — a plain skill used inside other work, carrying no identity of its own: co-thinker,
  grilling, wayfinder, domain-modeling, codebase-design, diagnosing-bugs, prototype, show-me,
  writing-for-agents, writing-for-humans, self-improvement, wizard, maintain-verification-skill.
- **Human command** — a skill only the human invokes by name, never reached for by a model:
  to-project, to-issue, grill-me, bro, handoff, walkthrough, teach, improve-codebase-architecture,
  create-verification-skill.
- **Rubric** — the one named standard a reviewer judges a candidate against: code, docs,
  project-plan, spec, or merge.

## Execution and proof

- **Supersymmetry** — a captain's Issue is a Project one level down: the same Types, statuses and
  chain hold at both levels, and only the map holder changes.
- **Reviewed intent** — the rule that implementation begins only from an independently reviewable
  contract: an atomic Issue, or a Project's Issues with any reviewed Project plan they link.
- **Scenario** — one acceptance criterion at the product's boundary, written in Gherkin in the
  Issue's feature files by the code-writer, where the Issue's contract calls for Gherkin. It is
  contract: the captain decides the set and the writer wires it without weakening it; a change to it
  is a contract amendment.
- **Gate** — any explicit pass/fail checkpoint.
- **Hop** — one crew member's committed pass over an Issue branch: `implement`, `fix` or `merge`, and
  `proof` for a proof-only test on an Inquisition's proof branch. Its SHA is evidence on the Issue,
  and the reviewer reads the hops in order.
- **Issue review** — an independent reviewer's verdict on one candidate against one named rubric,
  before it merges. PASS means no findings; a note is a finding, and a finding is a FAIL.
- **Merge review** — the `merge` rubric run after every merge that has a Merge Issue: a mechanical
  spot check of the integrated state that also proves the plan's acceptance criteria at a feature's
  final merge. An atomic Issue's merge into main has none; its Issue review PASS, green CI and the
  human's click stand in its place.
- **Inquisition** — a human-confirmed Type held by a captain: read-only sweeps, hypotheses proved
  (one in code by a proof-only test, one in prose by its quoted passages), deduplicated Bugs and an
  inquisition record. It files outcomes, never PASS or FAIL.
- **Merge** — a Type for one merge operation, with its own implement/review chain and
  integrated gates. The human clicks the Project landing and an atomic Issue's PR, which has no Merge
  Issue; every operation preserves hop SHAs.
- **Release** — a captain leaves a resume SHA on the record, pushes its branch, removes the worktree,
  returns the parent to `Todo`, unassigns and wires any blocker. A dead session has no final push.
- **Review block** — the reviewer's whole return, and the only thing that fills an Issue Captain's
  review slot, in the one-line `PASS` or `FAIL` form of the
  [Linear Workspace Standard](./linear-workspace-standard.md#communication-and-evidence). A PASS binds one candidate under one contract; a change to
  either calls for a fresh review. It lives on the work judged: a Project update for a plan, its Merge Issue for a merge, otherwise
  its Issue; a PR carries the block when one exists.
- **Evidence** — a commit, PR, test result, review verdict, or audit artifact linked to an Issue; it
  is not another work item.
- **Working-tree contract** — the shared rules for opening a feature branch, claiming an Issue by
  assignment, keeping one writer per worktree, and cleaning up after the merge. It lives in
  [working-tree-contract.md](../guides/working-tree-contract.md), so no agent invents its own habits.
- **Worktree** — Git isolation for implementation; the host owns where it lives.
- **HITL** / **AFK** — whether producing the work requires live human participation. They are not work
  types or acceptance states.

## Retired PM terms

These words survive only in older documents. Campaign, Sprint, Slice, Ticket, backlog item, and the
separate observed-problem Issue are not dydo 3 PM objects: use the Linear Initiative, Project, and
Issue where work is live, though a slice may still name an implementation technique. Tier-1
manager, orchestrator, and the run-sprint workflow (internally
run-issues) gave way to the officers — an Issue Captain owns one Issue, and an admiral the human
invokes coordinates one Project. The Project Planner gave way to the map holder, who charts with
`wayfinder` (DR 051). Hat and worker are retired too (DR 050): a session keeps the role it was given and loads skills
as the work needs them, and a spawned role is crew. Issue planner and test-writer gave way to `code-writer`, which since 2026-09-21 also absorbs the
specifier, implementer and hardener chain that briefly replaced it (DR 047, amended by DYD-222). Its
phases and the Issue-resolution plan it posted retired on 2026-09-23 (DR 046, amended by DYD-230):
the captain's contract is the Issue's only contract text. The `Specifying` and `Hardening` statuses
and the tightening or hardening pass retired the same day (DR 047, amended 2026-09-23): a spec review
shows `In Review`. Wayfinding map, Waypoint, and the Frontier they defined are gone: a Project's map is its
Linear description, and frontier now means the map Issues above. Workflow retired with the old inquisition harness. Inquisition is now an Issue Type with the
filing outcome above. None of these words creates a file, command, lifecycle, or Linear type.

## Related

- [Working-Tree Contract](../guides/working-tree-contract.md) — Branches, claims, worktrees, cleanup
- [Project Glossary](../glossary.md) — This project's domain vocabulary
