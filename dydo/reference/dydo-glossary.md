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

## Durable knowledge

- **Decision** — an accepted architectural or product choice stored in Git.
- **Project plan** — a reviewed repository contract for coordinated, cross-cutting, or
  architecture-sensitive work. Its `linear-project` URL is provenance, not synchronization.
- **FutureFeature** — an unscheduled repo-native idea. Only the human may promote it to exactly one
  Linear Initiative, Project, or Issue; `promoted` is terminal and does not mirror delivery state.
- **Wayfinding map** — an optional durable navigation overlay for a committed Project whose route is
  still too uncertain to plan completely.
- **Waypoint** — a navigation node, not a work object. It may point to durable evidence or Linear work.
- **Frontier** — the currently actionable Waypoints.
- **Fog** — relevant uncertainty not yet sharp enough to become a Waypoint.
- **Assimilation brief** — the durable account of what changed, what was learned, and what remains.

## Execution and proof

- **Reviewed intent** — the rule that implementation begins only from an independently reviewable
  contract: an atomic Issue or a linked reviewed Project plan plus its Issues.
- **Gate** — any explicit pass/fail checkpoint.
- **Review** — an independent examination of one implementation Issue before human harmonization.
- **Integrated audit** — the Project-level examination of the combined result against its linked plan.
- **Inquisition** — a multi-lens QA sweep used at meaningful product milestones.
- **Evidence** — a commit, PR, test result, review verdict, or audit artifact linked to an Issue; it is
  not another work item.
- **Role** — an authored methodology identity compiled by dydo.
- **Skill** — the runtime package of a role's methodology and resources.
- **Agent** — a native-platform worker instance.
- **Worktree** — Git isolation for implementation; the platform owns it.
- **HITL** / **AFK** — whether producing the work requires live human participation. They are not work
  types or acceptance states.

## Retired PM terms

Campaign, Sprint, Slice, Task, backlog item, and the separate observed-problem Issue are not canonical
dydo 3 PM objects. Use Linear Initiative/Project/Issue where work is live. “Slice” may describe an
implementation technique informally, but creates no file, command, lifecycle, or Linear type.
