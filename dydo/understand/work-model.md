---
area: understand
type: concept
---

# Work Model

How work moves here: Linear owns the live work graph, Git and dydo own durable knowledge and proof,
and every session places itself on one flow map before it acts. The stages, the roles that hold them,
and the reviews that prove them follow
[Decision 047](../project/decisions/047-supersymmetry-hop-statuses-merge-issues-and-the-release-protocol.md),
which amends Decisions 045 and 046.

## The flow map

A session keeps the role it was opened or spawned with and loads skills as the work needs them; an
officer never becomes another officer ([Decision 050](../project/decisions/050-officers-crew-and-skills-hats-retired.md)).

| Stage | Who | Output | Gate |
|---|---|---|---|
| Think | any session, loading co-thinker | ripe intent, and a Decision Record when the choice earns one | — |
| Chart and plan a Project | the map holder, the human or an invoked admiral, with wayfinder | the map in the Linear Project, first Issues, Questions; a repository plan only for a cross-cutting architecture contract | project-plan review of that plan, then human approval |
| Make an Issue exact | issue-captain, through its compact acceptance contract | a just-in-time contract with no hidden implementation decisions; the route stays the writer's | optional spec review |
| Implement | issue-captain | an Issue branch, a PR into the feature branch, or into main for an atomic Issue, evidence on the Issue | reviewer PASS |
| Coordinate | the map holder, the human or an invoked admiral | captains in flight, wired Merge Sub-issues, plan amendments | each captain directs its own merge review |
| Inquisition | issue-captain | read-only sweeps, proof tests for code hypotheses and quoted passages for prose ones, Bugs and an inquisition record | human confirms Backlog → Todo; it files rather than gates |
| Land | the human | the feature branch merged into main | the human's own hands |
| Walkthrough | an invoked admiral with the human, or the human alone | inspected landing, findings as Issues in the same Project | an empty walkthrough closes the Project |

Some work has no stage of its own: chief-of-staff triages the human's attention and never delivers,
and any session may reach for self-improvement, writing-for-agents and diagnosing-bugs; bro is the human's
corrective for agent-speak, at any stage.

## Officers, crew, and skills

- **Officers** hold something and never switch: the admiral holds a Project, the issue-captain an
  Issue, the chief-of-staff the board. A session or agent has at most one officer role; the human
  holds a row on the map but no role.
- **Crew** are spawned for one bounded job, hold nothing, and return their result to whoever sent them. Research delegates to non-authoring scouts; other crew do their own bounded work.
- **Methods** are plain skills — reference and procedure a session applies inside its own thread, never a separate
  session.
- **Human commands** are invoked by the human typing their name, and by nothing else. For the
  post-landing tour, the map holder — an invoked admiral, or the human itself — opens the Walkthrough
  Issue and asks the human to invoke `walkthrough` in that same session before facilitating it.

The [dydo glossary](../reference/dydo-glossary.md) names every member of each category. Every one of them is a native skill folder — see
[Scaffold and Customization](./scaffold-and-customization.md).

A captain's Issue is a Project one level down: the same Types, statuses and chain, with a different
map holder. The [control-flow map](./control-flow.md) carries every contact, including captain
two-step returns, release/takeover, review FAIL, merge FAIL and blocker-cleared resume. The board is
the inbox; an admiral wakes on a captain's return or the human's word.

## Canonical ownership

| Concern | Canonical home |
|---|---|
| Initiatives, Projects, Issues, optional Milestones and Cycles | Linear |
| Status, priority, assignee, dependencies, updates, review state | Linear |
| Decisions, reviewed Project plans, guides | dydo/Git |
| Audit, inquisition, migration, and assimilation evidence | dydo/Git |
| Release tags and changelog | Git |
| FutureFeatures and their promotion state | Linear |

dydo has no Linear client, token, schema, poller, webhook receiver, cache, or Markdown mirror. Agents
reach Linear through its official MCP, UI, API, and integrations, outside the dydo runtime.

## Reviewed intent

No implementation begins without a contract another agent can review independently.

- One atomic Issue can be its own reviewed contract. Coordinated, cross-cutting, or
  architecture-sensitive work gets one reviewed Project plan in the repository.
- The plan carries one `linear-project` URL, and its Linear Project links back to the published plan.
- An implementation Issue under a Project plan records the plan's exact governing commit before
  execution; a review pins the Issue's own contract as its description as of a Linear `updatedAt`.
- The two planning resolutions, the fields an Issue must carry, and the Question Issue that clears
  fog are in the [Linear Issue Lifecycle](./task-lifecycle.md).

Branches, worktrees, sessions, crew, commits, PRs, and review passes are execution evidence linked
to an Issue. They are not extra levels in the work graph.

## Review and inquisition

1. **Issue review** — a fresh reviewer with the rubric the candidate targets: code or docs
   before merge; `project-plan` before Project approval; `spec` on the contract before any code
   only for one risk the Issue Captain records in the contract.
2. **Merge review** — a reviewer with the `merge` rubric after every merge that has a Merge Issue
   (a lane into its parent, an Issue into the feature, the landing): a mechanical spot check
   scaled to what landed, which at the final feature merge also proves the plan's acceptance criteria.
   An atomic Issue's merge into main has none; its Issue review PASS, green CI and the human's
   click stand in its place.
3. **Inquisition** — human-confirmed, captain-directed sweeps that file Bugs, each code hypothesis
   proved by a proof-only test and each prose one by its quoted passages, with
   a separate record Feature's docs-writer preserving the evidence through normal delivery. The
   Inquisition releases until that delivery lands, then verifies retention before closing; see the
   [working-tree contract](../guides/working-tree-contract.md#retaining-an-inquisitions-record-and-proofs).
   It catches what got through; it never proves zero defects.

Every reviewer verdict is the same **review block**: a Project update for a plan, on the Merge
Issue for a merge, otherwise on its Issue, and in the PR body when one exists; its one-line form is locked in the
[Linear Workspace Standard](../reference/linear-workspace-standard.md#communication-and-evidence). Independence here is independence of *context*: the
reviewer arrives fresh and reads the candidate itself rather than the story told about it. There is
no PASS with notes; a note is a finding, and a finding is a FAIL.

Reviewer, inquisitor and scout are non-authoring roles: their commissions and methods produce
verdicts, findings or source passages, never candidate edits. This is a methodological boundary, not
a claim that every host enforces a read-only filesystem. The portable floor is a fresh commission,
an isolated pinned candidate and an independent review; a native sandbox request strengthens that
floor only when a live check establishes its enforcement.

## References and evidence

Use a branch-following GitHub URL for current human navigation, and an exact commit permalink for a
governing contract or historical proof. The Issue branch carries its Linear Issue key, so Linear's
GitHub integration attaches branch and PR to the Issue natively; the
[Working-Tree Contract](../guides/working-tree-contract.md) is the procedure. Durable knowledge
discovered during work is extracted to a Decision, guide, Project plan, audit, or assimilation brief
rather than left in a comment thread or a session transcript.

## FutureFeatures

A FutureFeature is an unscheduled strategic possibility recorded as a Linear Issue, not a generic
idea or delivery contract. It stays in `FutureFeature` until the human promotes or cancels it. The
[Linear Workspace Standard](../reference/linear-workspace-standard.md) defines its status and
promotion paths.

## Related

- [Control Flow](./control-flow.md) — every handoff drawn: roster, happy path, states, edge contracts, exceptions
- [Linear Issue Lifecycle](./task-lifecycle.md) — what an Issue carries, and how it is claimed and merged
- [Working-Tree Contract](../guides/working-tree-contract.md) — branches, worktrees, claims, cleanup
- [Decision 045 — Flow Map, Hats and Workers, Review Tiers, and the Working-Tree Contract](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md)
- [Decision 044 — Linear-Canonical PM and the dydo Knowledge Boundary](../project/decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md)
- [dydo Glossary](../reference/dydo-glossary.md) — the locked vocabulary
- [Writing Good Briefs](../guides/writing-good-briefs.md) — how a brief is written
