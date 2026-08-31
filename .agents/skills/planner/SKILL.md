---
name: planner
description: Ripe intent, no route yet. Use when a settled goal must become an atomic Issue or a Project plan with its Issue map; when a Project is too foggy to route and has to be charted first; when work is about to start from an Issue with no outcome, owned paths, blockers, gates or base branch.
---

# Planner

Turn ripe intent into a route another agent can walk without asking.

## Must-Reads

1. The Linear Project or Issue carrying the intent, with its links, answers, and blocking relations.
2. The Decision Records that govern the area being routed.
3. [about.md](../../../dydo/understand/about.md)
4. [architecture.md](../../../dydo/understand/architecture.md)
5. [dydo-glossary.md](../../../dydo/reference/dydo-glossary.md)

## Boundary

Plan is the stage: intent arrives ripe from Think, the route goes to a fresh reviewer under the `plan`
rubric, and the human approves it before the manager opens the feature. Route the work; write none of
it.

**Start only when ripe.** Ripe is a settled goal, settled trade-offs, settled product decisions. Send
anything still open back to Think with the question named, or file it as a question Issue.

## Method

1. **Chart before you route.** Chart a foggy Project with wayfinder: map what is known, keep fog too
   dim to phrase in `## Not yet specified`, and file a question Issue for every unknown sharp enough to
   ask that a route would otherwise invent. Done when the frontier is visible and nothing on the map is
   invented.
2. **Read the ground.** Code, tests, prior plans, the governing Decisions. Done when you can name the
   pattern to follow with its path, the touchpoints, the hazards, and the evidence of success.
3. **Design at architecture level.** Use codebase-design for module interfaces, seams, and depth. Done
   when no implementation Issue is left carrying an architectural decision of its own.
4. **Write the plan at low resolution.** Follow [project](.agents/skills/planner/resources/project.md): destination, scope,
   acceptance, the design, the Issue map, ordering and isolation, watch-outs. One atomic Issue is its
   own contract and needs no plan. Done when the plan can govern execution unchanged, or says
   `## Not yet specified` for fog too dim to phrase and carries a question Issue for every gap sharp
   enough to ask.
5. **Cut the map into tracer bullets.** Every Issue is one independently reviewable outcome that runs
   end to end through the stack; a wide refactor expands before it contracts. Done when each Issue
   carries its five required fields — outcome, owned paths, blockers, exact gates, base branch — meets
   the self-containment bar of this repository's writing-good-briefs guide, and owns paths no sibling
   touches or is marked serial.
6. **Leave high resolution to just-in-time.** The [issue](.agents/skills/planner/resources/issue.md) plan is written into the
   Issue as implementation's first step and reviewed with the code. Done when the Project plan flags
   the architecture-sensitive Issues — the ones whose plan is reviewed before any code — and each such
   Issue says so.

## Handoff

Give the reviewer the committed plan or Issue at its exact commit, its governing Decisions, and its
Linear Project — never this conversation — and name the `plan` rubric. Resolve every finding, then ask
the human to approve. On approval hand the manager the passing commit, the Issue map with its blockers
and base branches, and the open question Issues; the manager opens the feature and amends the plan as
fog clears.
