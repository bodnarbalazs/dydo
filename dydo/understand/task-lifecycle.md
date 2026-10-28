---
area: understand
type: concept
---

# Linear Ticket Lifecycle

A ticket is the only actionable work item, and Linear owns every field on it that moves. This is what
a ticket carries, how it is planned, claimed, reviewed and merged, and the user's gates. The file keeps its historical path so durable links still resolve.

## Two kinds of ticket

An **implementation ticket** carries five required fields - outcome, owned paths, blockers, exact
gates, base branch - plus the relevant context, and it links the governing Decision and the exact plan
commit when a Project plan applies, plus applicable feature-spec commit and sections. Use a
Sub-ticket only when the child needs its own status, owner,
dependency, or review evidence; a checklist is enough for mechanical steps that cannot progress
independently.

A **Question Ticket** (Linear Type `Question`, body under `## Question`) is an open question that
blocks planning or implementation and is too big or too uncertain to settle inline. The rule is *fog →
discovery → Question Ticket*: an agent in fog first runs a bounded discovery - the Decision index, the
Project plan, the ticket's own links, the glossary, the code - and only when that comes up empty does
the question become a ticket that records what was searched and blocks every named plan or
implementation ticket awaiting its answer. A crew member raises a hand to its captain. Captains file
local Question Sub-tickets; the Project's map holder, the user or an invoked admiral, alone files
Project-level Questions. An invoked admiral routes AFK homework and sends only HITL judgment to the user.
Facts are the agent's job; choices are the user's.

Its resolution is an *answer* posted on the ticket. The answer graduates to a Decision Record only when
it is hard to reverse, surprising later, and the result of a real trade-off. Tickets carry questions,
Decision Records carry decisions, and the two are linked rather than copied.

## Planned at two resolutions

- **Project** - low resolution: the Linear Project and its tickets are the plan, charted by the map
  holder with `wayfinder`: destination, the first pickable tracer-bullet tickets, and fog written
  down in `## Not yet specified`, with blocking Question Tickets instead of a pretended complete
  route. Only a cross-cutting architecture contract earns a repository plan file, written by the
  map holder; a fresh reviewer passes it against `project-plan` before user approval, and dated
  amendments return only changes to destination, scope, acceptance criteria, or governing
  architecture for fresh review and user approval. Shared multi-ticket behavior may use a
  [feature spec](../reference/feature-spec-convention.md); reconcile affected contracts when intent shifts.
- **Ticket** - high resolution, just in time: the Ticket Captain's compact acceptance contract on the
  parent ticket or a direct lane Sub-ticket, with its scenarios where the ticket carries Gherkin, owned
  paths and gates, exact enough to build. The spawned `code-writer` builds from it. The contract is
  reviewed with the code; the Captain may buy a `spec` review of it before any code for one risk it
  records in the contract.

## Claimed, isolated, executed

Assignment is the claim: nothing else marks a ticket as taken. From there the ticket has one branch and
one worktree, one writer inside it, and commits that touch only the paths the ticket owns. The
[Working-Tree Contract](../guides/working-tree-contract.md) is that procedure end to end - how the
branch is named, what goes on the ticket before the first edit, and what is cleaned up after the
merge.

Linear owns the ticket's status, priority, assignee, blockers and updates throughout. The branch,
worktree, session, commits, PR and test runs are evidence for that ticket, never additional work
records.

Every delivery kind starts with the captain making its contract exact. The captain sets
Implementing at the author's spawn, a code-writer or a docs-writer, and In Review at a
reviewer's, the optional spec review before any code included. A parent whose lanes run is In Progress. The writer
posts nothing before code unless the work splits into disjoint lanes or the contract is inexact;
its IMPLEMENTED return carries the hop SHA, the red proof, the gates run and any blocker. A
scenario changes only through a fresh author's fix hop. Every hop's SHA is posted, preserved and passed to the next crew member.

## Reviewed before it merges

A fresh reviewer judges the candidate against the rubric it targets before any merge, and where the
merge has a Merge Ticket a second reviewer applies the `merge` rubric after the merge lands. An
atomic ticket's merge into main has none; its ticket review PASS, green CI and the user's click stand
in its place. Each reviewer returns the review block, which is posted on the work judged: its ticket or Merge Ticket, and in the PR body when present. A fifth consecutive FAIL on the same review loop is
itself an escalation - stop looping and raise a hand. Review, inquisition and the verdict's rule are in
the [Work Model](./work-model.md); the review block's one-line form is locked in the
[Linear Workspace Standard](../reference/linear-workspace-standard.md#communication-and-evidence).

A reviewed PR sets the source ticket Ready to Merge and the captain returns `done <key>: PR ready`.
A Project ticket's final Merge Sub-ticket runs when its native blocker clears; the parent stays Ready to Merge.
The captain directs the merge by one code-writer and a fresh merge review, then marks both
Done and returns `done <key>: merged`. A Merge Sub-ticket never waits in Ready to Merge. The landing
Merge does: the user clicks its reviewed PR as a merge commit.

A FAIL, one or more blockers, returns to Implementing: a fresh author of the change's kind -
`code-writer`, or `docs-writer` for a documentation change - takes the next fix hop
with the FAIL block's blockers as its contract, amending a scenario only where the block names it. Corrections carry new
commits and fresh reviews. From either verdict, the captain files each should as its own ticket and
records each nit as one line on the ticket ([severity](../reference/dydo-glossary.md)). Merge FAIL
fixes integration defects inside Merge. A source defect is reverted there, Merge closes Canceled
and source returns to Implementing; if a later merge depends on it, a following fix ticket replaces
the revert. Plan review has its own two-round cap before the user chooses.

## Raising a hand

The ladder runs crew → Ticket Captain → an invoked admiral → user when one holds the map; without an
invoked admiral, the Ticket Captain escalates straight to the user, already the map holder. Agents
settle operational conflicts themselves by precedence, highest first: the user's live instruction, a
Decision Record, the reviewed architecture plan, the feature spec (both pinned), the ticket contract,
coding standards, existing code. The user
is reached only for a conflict with a Decision Record - is it truth, or is it obsolete? - for live
external state agents cannot coordinate, or for authority the contract cannot supply.

A crew member raises its hand by returning to its Ticket Captain; a comment on the ticket and, when
the work is blocked, a Question Ticket wired as a blocker are the captain's rungs. The code-writer's
one pre-code comment naming lanes or an inexact contract is its scoped exception, not a hand-raise.
A blocked captain releases: post the resume SHA, push the branch, clean the worktree (`dydo clean --dry-run <worktree>`, then `dydo clean <worktree>`, run from outside that tree),
return the parent to Todo and unassign. A user takeover does the same; a dead session leaves its
last recorded hop without a final push. The map holder's next wake - an invoked admiral's, or the
user's own return to the board when none is invoked - picks up blocker-cleared work.

## Where the user is asked

Project-plan approval; Questions and HITL work; an escalation that survived the ladder; confirming
an Inquisition; the feature → main merge-commit click; and the Walkthrough after it. Findings reopen
a lap in the same Project. An atomic ticket branches from main and has no Merge Sub-ticket: once its
PR carries a reviewer PASS and CI is green, the user clicks it, and its captain closes it Done.
The [workspace standard](../reference/linear-workspace-standard.md) owns the Type/Mode and priority rules.

Linear status is the only delivery status. dydo does not copy it into frontmatter, infer it from Git,
or poll Linear.

## Related

- [Work Model](./work-model.md) - the flow map, ownership, review and inquisition
- [Working-Tree Contract](../guides/working-tree-contract.md) - branches, worktrees, claims, cleanup
- [Writing Good Briefs](../guides/writing-good-briefs.md) - how a ticket's contract is written
- [dydo Glossary](../reference/dydo-glossary.md) - the locked vocabulary
