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
every other crew member raises a hand to its captain. Captains file local Question Sub-issues; the
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
  approval; the admiral then updates the Linear map as fog clears and commissions project-planner
  to commit dated plan amendments, returning only changes to destination,
  scope, acceptance criteria, or governing architecture for fresh review and human approval.
- **Issue** — high resolution, just in time: the Issue Captain's compact acceptance contract on the
  parent Issue or a direct lane Sub-issue, with its scenarios where the Issue carries Gherkin, owned
  paths and gates, exact enough to build. The spawned `code-writer` builds from it. The contract is
  reviewed with the code; the Captain may buy a `spec` review of it before any code for one risk it
  records in the contract.

## Claimed, isolated, executed

Assignment is the claim: nothing else marks an Issue as taken. From there the Issue has one branch and
one worktree, one writer inside it, and commits that touch only the paths the Issue owns. The
[Working-Tree Contract](../guides/working-tree-contract.md) is that procedure end to end — how the
branch is named, what goes on the Issue before the first edit, and what is cleaned up after the
merge.

Linear owns the Issue's status, priority, assignee, blockers and updates throughout. The branch,
worktree, session, commits, PR and test runs are evidence for that Issue, never additional work
records.

Every delivery kind starts with the captain making its contract exact. The captain sets
Implementing at the author's spawn, a code-writer or a docs-writer, and In Review at a
reviewer's, the optional spec review before any code included. A parent whose lanes run is In Progress. The writer
posts nothing before code unless the work splits into disjoint lanes or the contract is inexact;
its IMPLEMENTED return carries the hop SHA, the red proof, the gates run and any blocker. A
scenario changes only through a fresh author's fix hop. Every hop's SHA is posted, preserved and passed to the next crew member.

## Reviewed before it merges

A fresh reviewer judges the candidate against the rubric it targets before any merge, and where the
merge has a Merge Issue a second reviewer applies the `merge` rubric after the merge lands. An
atomic Issue's merge into main has none; its Issue review PASS, green CI and the human's click stand
in its place. Each reviewer returns the review block, which is posted on the work judged: its Issue or Merge Issue, and in the PR body when present. A fifth consecutive FAIL on the same review loop is
itself an escalation — stop looping and raise a hand. Review, inquisition and the verdict's rule are in
the [Work Model](./work-model.md); the review block's one-line form is locked in the
[Linear Workspace Standard](../reference/linear-workspace-standard.md#communication-and-evidence).

A reviewed PR sets the source Issue Ready to Merge and the captain returns `done <key>: PR ready`.
A Project Issue's final Merge Sub-issue runs when its native blocker clears; the parent stays Ready to Merge.
The captain directs the merge by one code-writer and a fresh merge review, then marks both
Done and returns `done <key>: merged`. A Merge Sub-issue never waits in Ready to Merge. The landing
Merge does: the human clicks its reviewed PR as a merge commit.

Every FAIL returns to Implementing, whatever it found: a fresh author of the change's kind —
`code-writer`, or `docs-writer` for a documentation change — takes the next fix hop
with the FAIL block as its contract, amending a scenario only where the block names it. Corrections carry new
commits and fresh reviews. Merge FAIL
fixes integration defects inside Merge. A source defect is reverted there, Merge closes Canceled
and source returns to Implementing; if a later merge depends on it, a following fix Issue replaces
the revert. Plan review has its own two-round cap before the human chooses.

## Raising a hand

The ladder runs crew → Issue Captain → admiral → human, and agents settle operational conflicts
themselves by precedence, highest first: the human's live instruction, a Decision Record, the reviewed
Project plan at its governing commit, the Issue contract, coding standards, existing code. The human
is reached only for a conflict with a Decision Record — is it truth, or is it obsolete? — for live
external state agents cannot coordinate, or for authority the contract cannot supply.

A crew member raises its hand by returning to its Issue Captain; a comment on the Issue and, when
the work is blocked, a Question Issue wired as a blocker are the captain's rungs. The code-writer's
one pre-code comment naming lanes or an inexact contract is its scoped exception, not a hand-raise.
A blocked captain releases: post the resume SHA, push the branch, remove the worktree,
return the parent to Todo and unassign. A human takeover does the same; a dead session leaves its
last recorded hop without a final push. The admiral's next wake picks up blocker-cleared work.

## Where the human is asked

Project-plan approval; Questions and HITL work; an escalation that survived the ladder; confirming
an Inquisition; the feature → main merge-commit click; and the Walkthrough after it. Findings reopen
a lap in the same Project. An atomic Issue branches from main and has no Merge Sub-issue: once its
PR carries a reviewer PASS and CI is green, the human clicks it, and its captain closes it Done.
The [workspace standard](../reference/linear-workspace-standard.md) owns the Type/Mode and priority rules.

Linear status is the only delivery status. dydo does not copy it into frontmatter, infer it from Git,
or poll Linear.

## Related

- [Work Model](./work-model.md) — the flow map, ownership, review and inquisition
- [Working-Tree Contract](../guides/working-tree-contract.md) — branches, worktrees, claims, cleanup
- [Writing Good Briefs](../guides/writing-good-briefs.md) — how an Issue's contract is written
- [dydo Glossary](../reference/dydo-glossary.md) — the locked vocabulary
