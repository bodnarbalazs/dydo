---
area: understand
type: concept
---

# Work Model

Linear owns dydo's live work graph; Git owns durable knowledge and proof. The boundary is deliberately
one-way linking, not synchronization: Linear points to repository plans and evidence, while dydo never
mirrors workflow status, assignments, or issue bodies.

## Canonical ownership

| Concern | Canonical home |
|---|---|
| Initiatives, Projects, Issues, optional Milestones and Cycles | Linear |
| Status, priority, assignee, dependencies, updates, review state | Linear |
| Decisions, doctrine, reviewed Project plans, guides | dydo/Git |
| Audit, inquisition, migration, and assimilation evidence | dydo/Git |
| Release tags and changelog | Git |
| FutureFeatures before and after human promotion | dydo/Git |

An Initiative is an optional workspace-level goal. A Project is one bounded outcome owned by a Linear
team. An Issue is the only actionable work item; use Sub-issues only when children need independent
tracking. Milestones are optional checkpoints inside a Project, and Cycles are optional capacity
timeboxes orthogonal to Projects. Labels provide restrained cross-cutting routing, not a second type
system.

## Reviewed intent

No implementation begins without a contract another agent can review independently.

- One atomic, autonomous-ready Issue can be its own reviewed contract.
- Coordinated, cross-cutting, or architecture-sensitive work gets one reviewed repository Project plan.
- The plan carries one `linear-project` URL, and its Linear Project links back to the published plan.
- Each implementation Issue records the exact governing commit before execution and receives
  independent review before human harmonization.
- A Project closes only after an integrated audit against its linked plan and an assimilation brief
  proportionate to the semantic change.

Branches, worktrees, sessions, workers, commits, PRs, and reviewer passes are execution evidence linked
to an Issue. They are not extra levels in the work graph.

## References and evidence

Use a branch-following GitHub URL for current human navigation and an exact commit permalink for the
governing contract or historical proof. A PR or commit includes its Linear Issue key so Linear's GitHub
integration can attach native execution evidence. Durable knowledge discovered during work is extracted
to a Decision, guide, Project plan, audit, or assimilation brief instead of remaining only in comments
or a session transcript.

dydo has no Linear client, token, schema, poller, webhook receiver, cache, or Markdown mirror. Agents use
Linear's official MCP, UI, API, and integrations outside the dydo runtime.

## FutureFeatures

A FutureFeature is an unscheduled, non-actionable repo-native idea. It has `area: project`,
`type: concept`, and `status: idea` until the human promotes it. Promotion creates exactly one Linear
Initiative, Project, or Issue, adds its stable URL as `linear-reference`, and changes the status to the
terminal `promoted`. Subsequent delivery state exists only in Linear. Every idea includes a non-empty
`## Rationale` and a `## Related` section with at least one resolving, non-Linear durable-knowledge link.
It carries no assignment, priority, blocker, dependency, Project, Initiative, Cycle, Milestone, due-date,
estimate, label, parent, Sub-issue, team, workflow, or delivery-state fields.

## Navigating uncertainty

A committed Project may use a low-resolution Wayfinding map when the route cannot yet be planned
responsibly. Waypoints capture navigation, the Frontier identifies what is actionable, and Fog records
relevant uncertainty. None is a live work type: when delivery becomes actionable, it enters Linear as
an Issue or Project.

## Retired repository PM model

Campaign, Sprint, Slice, Task, backlog item, and the separate observed-problem Issue are retired as
canonical work objects. “Slice” may remain an informal verb for making implementation reviewable; it
creates no repo record, state machine, command, or Linear type. The frozen v2 corpus remains temporarily
tracked only under the manifest-backed 3.0 migration boundary and cannot grow. The former
[dydo 2.0 Campaign Roadmap](https://github.com/bodnarbalazs/dydo/blob/ffffc02dcdf92b9677d0eb4f522d1af57a869990/dydo/project/backlog/dydo-2-campaign-roadmap.md)
is frozen historical evidence, not an active work model.

## Related

- [DR 044 — Linear-Canonical PM and the dydo Knowledge Boundary](../project/decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md)
- [Linear Issue Lifecycle](./task-lifecycle.md)
- [dydo Glossary](../reference/dydo-glossary.md)
- [Writing Good Briefs](../guides/writing-good-briefs.md)
