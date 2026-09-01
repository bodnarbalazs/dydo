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
- **Sub-issue** — optional decomposition when child Issues need independent tracking.
- **Cycle** — an optional team capacity timebox, orthogonal to Projects.
- **Label** — restrained cross-cutting routing metadata, never a shadow work-type hierarchy.

## Fog and question Issues

- **Fog** — the unknown unknowns: relevant uncertainty not yet sharp enough to state as a question.
  The rule is *fog → discovery → question Issue*. Search the Decision Records, the Project plan, the
  Issue's own links, the glossary, and the code first; file only what that leaves open.
- **Question Issue** — a Linear Issue labelled `question` carrying one open question that blocks
  planning or implementation and is too large to settle inline. It resolves into an **answer**
  recorded on the Issue; a small preference stays spec detail on the implementation Issue instead.
- **Frontier** — the open, unblocked, unassigned question Issues: the edge of what a Project knows.
  Linear's own blocking relations and assignment render it; there is no separate navigation object.

## Durable knowledge

- **Decision** — a choice recorded in a Decision Record because it is hard to reverse, surprising
  later, and the result of a real trade-off. Issues carry questions, Decision Records carry
  decisions, and the two are linked rather than copied.
- **Project plan** — a reviewed repository contract for coordinated, cross-cutting, or
  architecture-sensitive work, at low resolution: destination, acceptance, architecture, and an Issue
  map. Its `linear-project` URL is provenance, not synchronization.
- **Issue-resolution plan** — the high-resolution plan written into an implementation Issue before
  its code: files to touch, the pattern to copy, steps, edge cases, exact gates. It is reviewed
  together with the code it governs.
- **FutureFeature** — an unscheduled repo-native idea. Only the human may promote it to exactly one
  Linear Initiative, Project, or Issue; `promoted` is terminal and does not mirror delivery state.
- **Assimilation brief** — the durable account of what changed, what was learned, and what remains.

## Roles and skills

- **Role** — an authored skill source compiled by dydo: a hat, a worker, a method, or a human command.
- **Skill** — the runtime package of a role's methodology and resources.
- **Agent** — a native-platform instance of a spawned role: a worker, or an Issue Captain a manager
  keeps in flight.
- **Hat** — what a session is doing now: co-thinker, planner, issue-captain, manager, or
  chief-of-staff. One at a time, changed as the work moves; a hat is not a session type.
- **Worker** — a role spawned as an agent for one bounded job, returning its result to whoever
  spawned it: code-writer, test-writer, docs-writer, reviewer, inquisitor, research.
- **Method** — a reference or procedure used inside another skill, carrying no identity of its own:
  grilling, wayfinder, domain-modeling, codebase-design, diagnosing-bugs, prototype,
  writing-for-agents, self-improvement.
- **Human command** — a skill only the human invokes by name, never reached for by a model:
  grill-me, bro, handoff, walkthrough, teach, improve-codebase-architecture.
- **Workflow** — a host-executed script for a sequence prose cannot be trusted to hold. The
  inquisition is the only one.
- **Rubric** — the one named standard a reviewer judges a candidate against: code, tests, docs, plan,
  or merge.

## Execution and proof

- **Reviewed intent** — the rule that implementation begins only from an independently reviewable
  contract: an atomic Issue or a linked reviewed Project plan plus its Issues.
- **Gate** — any explicit pass/fail checkpoint.
- **Issue review** — an independent reviewer's verdict on one candidate against one named rubric,
  before it merges. PASS means no findings; a note is a finding, and a finding is a FAIL.
- **Merge review** — the `merge` rubric run after every merge: a mechanical spot check of the
  integrated state that also proves the plan's acceptance criteria at a feature's final merge.
- **Inquisition** — a rare, human-confirmed audit: inquisitors across lenses, the reviewer as judge,
  and an assimilation brief. It catches what got through; it does not prove zero defects.
- **Review block** — the reviewer's whole return, and the only thing that fills an Issue Captain's
  review slot: rubric, reviewer label and model, candidate and base SHA, verdict, the gates rerun
  with their results, and findings as `file:line → consequence → correction`. It is posted as a
  comment on the Issue and carried in the PR body.
- **Evidence** — a commit, PR, test result, review verdict, or audit artifact linked to an Issue; it
  is not another work item.
- **Working-tree contract** — the shared rules for opening a feature branch, claiming an Issue by
  assignment, keeping one writer per worktree, and cleaning up after the merge. It lives in
  [working-tree-contract.md](../guides/working-tree-contract.md), so no agent invents its own habits.
- **Worktree** — Git isolation for implementation; the host owns where it lives.
- **HITL** / **AFK** — whether producing the work requires live human participation. They are not work
  types or acceptance states.

## Retired PM terms

These words survive only in older documents. Campaign, Sprint, Slice, Task, Ticket, backlog item, and the
separate observed-problem Issue are not dydo 3 PM objects: use the Linear Initiative, Project, and
Issue where work is live, though a slice may still name an implementation technique and a task a
question Issue's type. Tier-1 manager, orchestrator, and the run-sprint workflow (internally
run-issues) gave way to the hats — a manager coordinates one Project, an Issue Captain owns one
Issue. Wayfinding map, Waypoint, and the Frontier they defined are gone: a Project's map is its
Linear description, and frontier now means the question Issues above. Integrated audit is replaced
by the three reviews above. None of these words creates a file, command, lifecycle, or Linear type.

## Related

- [Working-Tree Contract](../guides/working-tree-contract.md) — Branches, claims, worktrees, cleanup
- [Project Glossary](../glossary.md) — This project's domain vocabulary
