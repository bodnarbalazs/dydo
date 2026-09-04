---
area: reference
type: reference
---

# dydo Glossary

The locked vocabulary for dydo's Linear-native work model, durable knowledge, and agent execution.
Project-domain terms belong in the separate [glossary.md](../glossary.md).

## Linear work graph

- **Initiative** — an optional workspace-level goal spanning independently meaningful Projects.
- **Project** — a bounded product or technical outcome owned by one Linear team. Coordinated or
  architecture-sensitive work links to one reviewed repository Project plan.
- **Milestone** — an optional meaningful checkpoint inside a Linear Project.
- **Issue** — the only actionable tracked work item. Status, priority, assignment, blockers, updates,
  and current execution evidence live in Linear.
- **Task** — the implementation or delivery step on a Wayfinder map, represented by a `Feature`,
  `Improvement`, or `Bug` Issue rather than a separate Linear Type.
- **Sub-issue** — optional decomposition when child Issues need independent tracking.
- **Cycle** — an optional team capacity timebox, orthogonal to Projects.
- **Label** — restrained cross-cutting routing metadata, never a shadow work-type hierarchy.

## Fog and question Issues

- **Fog** — the unknown unknowns: relevant uncertainty not yet sharp enough to state as a question.
  The rule is *fog → discovery → question Issue*. Search the Decision Records, the Project plan, the
  Issue's own links, the glossary, and the code first; file only what that leaves open.
- **Question Issue** — a Linear Issue labelled `question` carrying one open question that blocks
  named planning or implementation work and is too large to settle inline. It records the homework
  already done and resolves into an **answer** on the Issue; a small preference stays spec detail on
  the implementation Issue instead.
- **Frontier** — the open, unblocked, unassigned question Issues: the edge of what a Project knows.
  Linear's own blocking relations and assignment render it; there is no separate navigation object.

## Durable knowledge

- **Decision** — a choice recorded in a Decision Record because it is hard to reverse, surprising
  later, and the result of a real trade-off. Issues carry questions, Decision Records carry
  decisions, and the two are linked rather than copied.
- **Project plan** — a reviewed repository contract for coordinated, cross-cutting, or
  architecture-sensitive work, at low resolution: destination, acceptance, architecture, and an Issue
  map. Its `linear-project` URL is provenance, not synchronization.
- **Issue-resolution plan** — the high-resolution spec and plan the specifier writes into an
  implementation Issue before its code: the scenarios and gates that make the contract exact, then
  the files to touch, the pattern to copy, steps and edge cases that make the route mechanical. It is
  reviewed with the code it governs; the Issue Captain may require a separate pre-code `spec` review
  when route risk warrants the extra gate.
- **FutureFeature** — an unscheduled strategic possibility recorded as a Linear Issue, distinct from
  a generic idea or delivery contract. It stays in `Backlog` until the human promotes or cancels it.
- **Assimilation brief** — the durable account of what changed, what was learned, and what remains.

## Roles and skills

- **Role** — an authored skill source compiled by dydo: a hat, a worker, a method, or a human command.
- **Skill** — the runtime package of a role's methodology and resources.
- **Agent** — a native-platform instance of a spawned role: a worker, a Project Planner, or an Issue
  Captain that an admiral keeps in flight.
- **Hat** — what a session is doing now: co-thinker, project-planner, issue-captain, admiral, or
  chief-of-staff. One at a time, changed as the work moves; a hat is not a session type.
- **Worker** — a role spawned as an agent for one bounded job, returning its result to whoever
  spawned it: specifier, implementer, hardener, docs-writer, reviewer, inquisitor, research.
- **Method** — a reference or procedure used inside another skill, carrying no identity of its own:
  grilling, wayfinder, domain-modeling, codebase-design, diagnosing-bugs, prototype, show-me,
  writing-for-agents, self-improvement.
- **Human command** — a skill only the human invokes by name, never reached for by a model:
  grill-me, bro, handoff, walkthrough, teach, improve-codebase-architecture.
- **Workflow** — a host-executed script for a sequence prose cannot be trusted to hold. The
  inquisition is the only one.
- **Rubric** — the one named standard a reviewer judges a candidate against: code, tests, docs,
  project-plan, spec, or merge.

## Execution and proof

- **Reviewed intent** — the rule that implementation begins only from an independently reviewable
  contract: an atomic Issue or a linked reviewed Project plan plus its Issues.
- **Scenario** — one acceptance criterion at the product's boundary, written in Gherkin in the
  Issue's feature files by the specifier. It is contract: implementation wires it and never edits it;
  a change to it is a spec amendment.
- **Gate** — any explicit pass/fail checkpoint.
- **Hop** — one worker's committed pass over an Issue branch: `specify`, `implement`, `harden`, or
  `fix`. Its SHA is evidence on the Issue, and the reviewer reads the hops in order.
- **Issue review** — an independent reviewer's verdict on one candidate against one named rubric,
  before it merges. PASS means no findings; a note is a finding, and a finding is a FAIL.
- **Merge review** — the `merge` rubric run after every merge: a mechanical spot check of the
  integrated state that also proves the plan's acceptance criteria at a feature's final merge.
- **Inquisition** — a rare, human-confirmed audit: inquisitors across lenses, the reviewer as judge,
  and an assimilation brief. It catches what got through; it does not prove zero defects.
- **Review block** — the reviewer's whole return, and the only thing that fills an Issue Captain's
  review slot: rubric, reviewer label and model, the contract at its governing SHA, candidate and
  base SHA, verdict, the gates rerun with their results, and findings as
  `file:line → consequence → correction`. A PASS binds one candidate under one contract; a change to
  either calls for a fresh review. It is posted as a comment on the Issue and carried in the PR body.
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
run-issues) gave way to the hats — an admiral coordinates one Project, an Issue Captain owns one
Issue. Issue planner, code-writer and test-writer gave way to the specifier, implementer and hardener
chain. Wayfinding map, Waypoint, and the Frontier they defined are gone: a Project's map is its
Linear description, and frontier now means the question Issues above. Integrated audit is replaced
by the three reviews above. None of these words creates a file, command, lifecycle, or Linear type.

## Related

- [Working-Tree Contract](../guides/working-tree-contract.md) — Branches, claims, worktrees, cleanup
- [Project Glossary](../glossary.md) — This project's domain vocabulary
