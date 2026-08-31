---
area: understand
type: concept
---

# Linear Issue Lifecycle

An Issue is the only actionable work item, and Linear owns every field on it that moves. This is what
an Issue carries, how it is planned, claimed, reviewed and merged, and the five moments the human is
asked. The file keeps its historical path so durable links still resolve.

## Two kinds of Issue

An **implementation Issue** carries five required fields — outcome, owned paths, blockers, exact
gates, base branch — plus the relevant context, and it links the governing Decision and the exact plan
commit when a Project plan applies. Use a Sub-issue only when the child needs its own status, owner,
dependency, or review evidence; a checklist is enough for mechanical steps that cannot progress
independently.

A **question Issue** (Linear label `question`, body under `## Question`) is an open question that
blocks planning or implementation and is too big or too uncertain to settle inline. The rule is *fog →
discovery → question Issue*: an agent in fog first runs a bounded discovery — the Decision index, the
Project plan, the Issue's own links, the glossary, the code — and only when that comes up empty does
the question become an Issue that lists what was searched, wired as a blocker and routed onward: the
manager when the Project itself is foggy, the planner when the plan needs refinement, the human only
when the question is HITL. Facts are the agent's job; choices are the human's.

Its resolution is an *answer* posted on the Issue. The answer graduates to a Decision Record only when
it is hard to reverse, surprising later, and the result of a real trade-off. Issues carry questions,
Decision Records carry decisions, and the two are linked rather than copied.

## Planned at two resolutions

- **Project** — low resolution: destination, scope, acceptance criteria,
  architecture-level design, and an Issue map of tracer bullets that each cut end to end, with
  ordering, isolation and watch-outs. When the route is foggy it says so in a `## Not yet specified`
  section and files question Issues instead of pretending a complete route. A fresh reviewer passes it
  against the `plan` rubric before any Issue is pickable; the manager then amends it in dated sections
  as fog clears, and sends it back for review only when scope, acceptance criteria or the Issue map
  move.
- **Issue** — high resolution, just in time: files to touch, the pattern to copy with its path, steps,
  edge cases, exact gates — until building is mechanical. The implementer writes it into the Issue as
  its first act, and it is reviewed together with the code it produced. A separate plan review before
  any code exists happens only for an Issue the Project plan flags as architecture-sensitive.

## Claimed, isolated, executed

Assignment is the claim: nothing else marks an Issue as taken. From there the Issue has one branch and
one worktree, one writer inside it, and commits that touch only the paths the Issue owns. The
[Working-Tree Contract](../guides/working-tree-contract.md) is that procedure end to end — how the
branch is named, what goes on the Issue before the first edit, and what is cleaned up after the
merge.

Linear owns the Issue's status, priority, assignee, blockers and updates throughout. The branch,
worktree, session, commits, PR and test runs are evidence for that Issue, never additional work
records.

## Reviewed before it merges

A fresh reviewer judges the candidate against the rubric it targets before any merge, and a second
reviewer applies the `merge` rubric after the merge lands; both return the review block, which is
posted on the Issue and carried in the PR body. A fifth consecutive FAIL on the same candidate is
itself an escalation — stop looping and raise a hand. The three tiers and the verdict's rule are in
the [Work Model](./work-model.md); the review block's fields are locked in the
[dydo Glossary](../reference/dydo-glossary.md).

## Raising a hand

The ladder runs worker → implementer → manager → human, and agents settle operational conflicts
themselves by precedence, highest first: the human's live instruction, a Decision Record, the reviewed
Project plan at its governing commit, the Issue contract, coding standards, existing code. The human
is reached only for a conflict with a Decision Record — is it truth, or is it obsolete? — for live
external state agents cannot coordinate, or for authority the contract cannot supply.

Raising a hand means a comment on the Issue and, when the work is blocked, a question Issue wired as a
blocker with the Issue moved to Blocked. Never silent waiting.

## Where the human is asked

Plan approval; HITL question Issues; an escalation that survived the ladder; confirming an
inquisition; and the feature → main merge. Harmonization happens on main afterwards and is not a
gate. An atomic Issue with no Project branches from main and is merged by its own implementer after
Issue review and merge review.

Linear status is the only delivery status. dydo does not copy it into frontmatter, infer it from Git,
or poll Linear.

## Related

- [Work Model](./work-model.md) — the flow map, the ownership boundary, the three review tiers
- [Working-Tree Contract](../guides/working-tree-contract.md) — branches, worktrees, claims, cleanup
- [Writing Good Briefs](../guides/writing-good-briefs.md) — how an Issue's contract is written
- [dydo Glossary](../reference/dydo-glossary.md) — the locked vocabulary
