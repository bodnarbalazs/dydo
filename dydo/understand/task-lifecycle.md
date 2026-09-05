---
area: understand
type: concept
---

# Linear Issue Lifecycle

An Issue is the only actionable work item, and Linear owns every field on it that moves. This is what
an Issue carries, how it is planned, claimed, reviewed and merged, and the human gates. The file keeps its historical path so durable links still resolve.

## Two kinds of Issue

An **implementation Issue** carries five required fields — outcome, owned paths, blockers, exact
gates, base branch — plus the relevant context, and it links the governing Decision and the exact plan
commit when a Project plan applies. Use a Sub-issue only when the child needs its own status, owner,
dependency, or review evidence; a checklist is enough for mechanical steps that cannot progress
independently.

A **Question Issue** (Linear Type `Question`, body under `## Question`) is an open question that
blocks planning or implementation and is too big or too uncertain to settle inline. The rule is *fog →
discovery → Question Issue*: an agent in fog first runs a bounded discovery — the Decision index, the
Project plan, the Issue's own links, the glossary, the code — and only when that comes up empty does
the question become an Issue that records what was searched and blocks every named plan or
implementation Issue awaiting its answer. Project planners prepare Question packets for the admiral;
Specifiers and workers raise a hand to their captain. Captains file local Question Sub-issues; the
admiral alone files Project-level Questions. The admiral routes AFK homework and sends only HITL judgment to the human.
Facts are the agent's job; choices are the human's.

Its resolution is an *answer* posted on the Issue. The answer graduates to a Decision Record only when
it is hard to reverse, surprising later, and the result of a real trade-off. Issues carry questions,
Decision Records carry decisions, and the two are linked rather than copied.

## Planned at two resolutions

- **Project** — low resolution: destination, scope, acceptance criteria,
  architecture-level design, the first pickable tracer-bullet Issues, and rough later bearings. When
  the route is foggy it says so in `## Not yet specified` and files blocking Question Issues instead
  of pretending a complete route. A fresh reviewer passes it against `project-plan` before human
  approval; the admiral then amends the map as fog clears and returns only changes to destination,
  scope, acceptance criteria, or governing architecture for fresh review and human approval.
- **Issue** — high resolution, just in time: the scenarios and gates that make the contract exact,
  then the files to touch, the pattern to copy with its path, steps and edge cases — until building
  contains no hidden decisions. A spawned `specifier` writes it into the parent Issue or direct lane
  Sub-issue at the Issue Captain's direction. It is reviewed with the code it produced; the Captain
  may require `spec` before production when route risk warrants it.

## Claimed, isolated, executed

Assignment is the claim: nothing else marks an Issue as taken. From there the Issue has one branch and
one worktree, one writer inside it, and commits that touch only the paths the Issue owns. The
[Working-Tree Contract](../guides/working-tree-contract.md) is that procedure end to end — how the
branch is named, what goes on the Issue before the first edit, and what is cleaned up after the
merge.

Linear owns the Issue's status, priority, assignee, blockers and updates throughout. The branch,
worktree, session, commits, PR and test runs are evidence for that Issue, never additional work
records.

Every delivery kind starts with a specify commit. The captain sets Specifying, Implementing,
Hardening or In Review at the corresponding spawn; In Review always means a reviewer is running.
A parent whose lanes run is In Progress. The spec names lanes and empty hops; a scenario changes
only through a fresh specifier. Every hop's SHA is posted, preserved and passed to the next worker.

## Reviewed before it merges

A fresh reviewer judges the candidate against the rubric it targets before any merge, and a second
reviewer applies the `merge` rubric after the merge lands; both return the review block, which is
posted on the work judged: its Issue or Merge Issue, and in the PR body when present. A fifth consecutive FAIL on the same review loop is
itself an escalation — stop looping and raise a hand. Review, inquisition and the verdict's rule are in
the [Work Model](./work-model.md); the review block's fields are locked in the
[dydo Glossary](../reference/dydo-glossary.md).

A reviewed PR sets the source Issue Ready to Merge and the captain returns `done <key>: PR ready`.
Its final Merge Sub-issue runs when its native blocker clears; the parent stays Ready to Merge.
The captain directs specification, merge implementation and fresh merge review, then marks both
Done and returns `done <key>: merged`. A Merge Sub-issue never waits in Ready to Merge. The landing
Merge does: the human clicks its reviewed PR as a merge commit.

FAIL returns to the fixing hop: contract to implementer, standards/tests/gates to hardener, wrong
scenario or route to fresh specifier; corrections carry new commits and fresh reviews. Merge FAIL
fixes integration defects inside Merge. A source defect is reverted there, Merge closes Canceled
and source returns to Implementing; if a later merge depends on it, a following fix Issue replaces
the revert. Plan review has its own two-round cap before the human chooses.

## Raising a hand

The ladder runs worker → Issue Captain → admiral → human, and agents settle operational conflicts
themselves by precedence, highest first: the human's live instruction, a Decision Record, the reviewed
Project plan at its governing commit, the Issue contract, coding standards, existing code. The human
is reached only for a conflict with a Decision Record — is it truth, or is it obsolete? — for live
external state agents cannot coordinate, or for authority the contract cannot supply.

Raising a hand means a comment on the Issue and, when the work is blocked, a Question Issue wired as a
blocker. A blocked captain releases: post the resume SHA, push the branch, remove the worktree,
return the parent to Todo and unassign. A human takeover does the same; a dead session leaves its
last recorded hop without a final push. The admiral's next wake picks up blocker-cleared work.

## Where the human is asked

Project-plan approval; Questions and HITL work; an escalation that survived the ladder; confirming
an Inquisition; the feature → main merge-commit click; and the Walkthrough after it. Findings reopen
a lap in the same Project. An atomic Issue branches from main and uses its own final Merge Sub-issue.
The [workspace standard](../reference/linear-workspace-standard.md) owns the Type/Mode and priority rules.

Linear status is the only delivery status. dydo does not copy it into frontmatter, infer it from Git,
or poll Linear.

## Related

- [Work Model](./work-model.md) — the flow map, ownership, review and inquisition
- [Working-Tree Contract](../guides/working-tree-contract.md) — branches, worktrees, claims, cleanup
- [Writing Good Briefs](../guides/writing-good-briefs.md) — how an Issue's contract is written
- [dydo Glossary](../reference/dydo-glossary.md) — the locked vocabulary
