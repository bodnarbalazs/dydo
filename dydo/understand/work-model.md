---
area: understand
type: concept
---

# Work Model

How work moves here: Linear owns the live work graph, Git and dydo own durable knowledge and proof,
and every session places itself on one flow map before it acts. The stages, the hats that wear them,
and the tiers that gate them are fixed by
[Decision 045](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md).

## The flow map

A session is not a type. It wears the hat the work is in now, and changes hats as the work moves.

| Stage | Hat | Output | Gate |
|---|---|---|---|
| Think | co-thinker | ripe intent, and a Decision Record when the choice earns one | — |
| Chart a foggy Project | planner, using wayfinder | the Linear Project as a map, and question Issues | — |
| Plan | planner | an atomic Issue, or a Project plan and its Issue map | plan review, then the human's approval |
| Implement | issue-captain | an Issue branch, a PR into the feature branch, evidence on the Issue | reviewer PASS |
| Coordinate (optional) | manager | several Issues in flight, serial merges, plan amendments | merge review after every merge |
| Audit (rare) | inquisition workflow | an audit and an assimilation brief | the human confirms before it runs |
| Land | the human | the feature branch merged into main | the human's own hands |
| Harmonize | the human, on main | improvements, and a new feature branch when one is needed | none — main is the state |

Some work has no stage of its own: chief-of-staff triages the human's attention and never delivers,
and any hat may reach for self-improvement, writing-for-agents and diagnosing-bugs; bro is the human's
corrective for agent-speak, at any stage.

## Hats, workers, and methods

- **Hats** are what a session is doing now, one at a time — the agent hats on the map plus
  chief-of-staff; the human and the inquisition workflow hold rows but wear no hat.
- **Workers** are spawned for one bounded job and report back to whoever spawned them. A worker never
  delegates.
- **Methods** are reference and procedure a session applies inside its own thread, never a separate
  session.
- **Human commands** are invoked by the human typing their name, and by nothing else.

The [dydo glossary](../reference/dydo-glossary.md) names every member of each category. Every one of them compiles from a template — see
[Templates and Customization](./templates-and-customization.md).

## Canonical ownership

| Concern | Canonical home |
|---|---|
| Initiatives, Projects, Issues, optional Milestones and Cycles | Linear |
| Status, priority, assignee, dependencies, updates, review state | Linear |
| Decisions, reviewed Project plans, guides | dydo/Git |
| Audit, inquisition, migration, and assimilation evidence | dydo/Git |
| Release tags and changelog | Git |
| FutureFeatures before and after human promotion | dydo/Git |

dydo has no Linear client, token, schema, poller, webhook receiver, cache, or Markdown mirror. Agents
reach Linear through its official MCP, UI, API, and integrations, outside the dydo runtime.

## Reviewed intent

No implementation begins without a contract another agent can review independently.

- One atomic Issue can be its own reviewed contract. Coordinated, cross-cutting, or
  architecture-sensitive work gets one reviewed Project plan in the repository.
- The plan carries one `linear-project` URL, and its Linear Project links back to the published plan.
- Every implementation Issue records the exact governing commit before execution.
- The two planning resolutions, the fields an Issue must carry, and the question Issue that clears
  fog are in the [Linear Issue Lifecycle](./task-lifecycle.md).

Branches, worktrees, sessions, workers, commits, PRs, and review passes are execution evidence linked
to an Issue. They are not extra levels in the work graph.

## Three review tiers

1. **Issue review** — a fresh reviewer with the rubric the change targets (code, tests, docs, plan),
   before any merge.
2. **Merge review** — a reviewer with the `merge` rubric after *every* merge: a mechanical spot check
   scaled to what landed, which at the final feature merge also proves the plan's acceptance criteria.
3. **Inquisition** — rare and human-confirmed, fanned out across lenses with the reviewer as judge and
   a docs-writer assimilating the result. It catches what got through; it never proves zero defects.

Every reviewer verdict, in any tier, is the same **review block**, posted as a comment on the Linear
Issue and carried in the PR body; its fields are locked in the
[dydo Glossary](../reference/dydo-glossary.md). Independence here is independence of *context*: the
reviewer arrives fresh and reads the candidate itself rather than the story told about it. There is
no PASS with notes; a note is a finding, and a finding is a FAIL.

## References and evidence

Use a branch-following GitHub URL for current human navigation, and an exact commit permalink for a
governing contract or historical proof. The Issue branch carries its Linear Issue key, so Linear's
GitHub integration attaches branch and PR to the Issue natively; the
[Working-Tree Contract](../guides/working-tree-contract.md) is the procedure. Durable knowledge
discovered during work is extracted to a Decision, guide, Project plan, audit, or assimilation brief
rather than left in a comment thread or a session transcript.

## FutureFeatures

A FutureFeature is an unscheduled, non-actionable repo-native idea, and it stays one until the human
decides otherwise. Promotion is his alone: it creates exactly one Linear Initiative, Project, or
Issue, records that stable URL on the idea, and moves its status to the terminal `promoted`. Delivery
state after that exists only in Linear, and the idea remains provenance rather than a mirror of it.
The frontmatter and body an idea must carry in either state are in
[Future Features](../project/future-features/_future-features.md).

## Related

- [Linear Issue Lifecycle](./task-lifecycle.md) — what an Issue carries, and how it is claimed and merged
- [Working-Tree Contract](../guides/working-tree-contract.md) — branches, worktrees, claims, cleanup
- [Decision 045 — Flow Map, Hats and Workers, Review Tiers, and the Working-Tree Contract](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md)
- [Decision 044 — Linear-Canonical PM and the dydo Knowledge Boundary](../project/decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md)
- [dydo Glossary](../reference/dydo-glossary.md) — the locked vocabulary
- [Writing Good Briefs](../guides/writing-good-briefs.md) — how a brief is written
