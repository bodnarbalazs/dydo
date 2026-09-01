---
area: guides
type: guide
---

# Orchestration Pitfalls

Failure modes seen when several agents deliver one Project in parallel. They are stated against the
model that governs delivery here: one hat owns one Issue end to end, a manager coordinates and
implements nothing, a fresh reviewer gates every candidate, and Git isolation keeps concurrent work
apart — one writer per worktree, commits touching owned paths only.

## 1. The coordinator picks up a file

**Symptom:** the session holding the plan makes "one small fix" itself, and the Project's history
contains a change nobody reviewed independently.

**Mechanism:** coordination and authorship collapse into the same context. The coordinating session is
the one that judges merges, so anything it writes is judged by its author, and its checkout becomes a
second writer in a tree someone else owns.

**Rule:** a manager sequences, merges, and judges work it did not write. Every edit inside an Issue's
owned paths belongs to a spawned worker or to the Issue Captain that owns the Issue; the manager's own pen
touches only the plan's dated amendments and the Linear record.

## 2. A branch cut from the wrong base

**Symptom:** a worker reports that required code or doctrine is missing, or a PR carries a diff nobody
asked for.

**Mechanism:** a worktree starts from a revision, not from another checkout's uncommitted state. A
branch cut from a stale base, or from the wrong branch entirely, cannot see a prerequisite that has not
merged.

**Rule:** cut from the Issue's base branch and post branch, base SHA and worktree path on the Issue
before the first edit. Keep the Issue blocked in Linear until its blockers land, and rebase onto the
feature branch when one of them lands late.

## 3. A whole-tree `git add`

**Symptom:** a commit carries another writer's unfinished edits, or a later cleanup reverts them.

**Mechanism:** a shared checkout holds several authors' unstaged work, and broad staging turns that
temporary union into permanent history.

**Rule:** stage the paths you own, by name. One writer per worktree; edits you meet that you do not own
are left where they are and reported.

## 4. Disjoint paths, shared gates

**Symptom:** an Issue's build or documentation gate fails on a sibling's incomplete change.

**Mechanism:** builds, documentation validation, generated output and coverage are whole-tree gates. Two
Issues can own strictly different files and still meet inside one compiled artifact, one hub, or one
test file.

**Rule:** name the shared surface in the plan, express it as a blocking relation, and sequence the
merges. Parallelize only where file ownership and gates are both independent.

## 5. A reviewer who is not fresh

**Symptom:** a PASS from a session that helped shape the change, or a second PASS from the reviewer who
issued the first.

**Mechanism:** independence is independence of *context*. A reviewer holding the writer's context
inherits its blind spot, and one revisiting its own verdict is defending it.

**Rule:** one fresh reviewer per candidate, given the rubric the change targets, reading the candidate
rather than the story told about it, rerunning the gates itself. Same-vendor review is acceptable; the
review block names the model, so who judged what stays observable later.

## 6. "PASS with notes"

**Symptom:** a merge on a verdict that carried leftovers, or a candidate on its eighth review pass.

**Mechanism:** a note is a finding and a finding is a FAIL. Softening the verdict moves the finding into
the integrated state, where it costs more to find. Looping instead is the opposite failure: the same
candidate keeps failing because something outside it is wrong.

**Rule:** fix and re-review; there is no verdict between PASS and FAIL. The relief valve is the cap, not
a softer verdict — a fifth consecutive FAIL on one candidate is itself an escalation.

## 7. Silent waiting, and its opposite

**Symptom:** an Issue sits in progress with no comment for hours, or a question that the plan already
answers arrives at the human.

**Mechanism:** an agent that meets fog can neither wait for an answer nor invent one. Both failures come
from skipping the middle: discovery first, then a question that is on the record.

**Rule:** run bounded discovery — the Decision Records, the plan, the Issue's links, the glossary, the
code — and only when it comes up empty file a question Issue listing what you searched, wire it as a
blocker, move the Issue to Blocked, and say so in a comment. Settle operational conflicts by
precedence: the human's live instruction, then the Decision Record, the reviewed plan at its governing
commit, the Issue contract, coding standards, existing code. The ladder is worker → Issue Captain → manager → human, and the human is
reached for a conflict with a Decision Record, live external state no agent can coordinate, or authority
the contract cannot supply.

## 8. A green result nobody can tie to a diff

**Symptom:** the gates passed, but the evidence cannot say which commit they passed against.

**Mechanism:** tests prove the checkout they ran in. They do not prove which commit was judged, which
worktree supplied it, or whether an edit landed afterwards.

**Rule:** let the review block pin it — candidate and base SHA, gates rerun with their output — posted
on the Issue and in the PR body. Every merge is followed by its own review of the integrated state, and
the last one proves the plan's acceptance criteria.

## 9. A spawned agent without its methodology

**Symptom:** a sub-agent works in a way its skill forbids, or its edits never reach the disk.

**Mechanism:** hosts differ. A compiled Claude agent preloads its skill; a compiled Codex agent is told
to load the skill by name, and what else it inherits is not documented. Sandbox mode decides whether it
can write at all, and hook trust is pinned by hash, so changing the hook configuration leaves sessions
unguarded until the human re-trusts it.

**Rule:** before relying on a new spawn path, ask the agent to name what it loaded and record the
answer. A spawn that cannot see its skill is a finding to file and route around, not a reason to stop
the work — and re-trust the hooks after any change to them.

## 10. Live knowledge left in Linear

**Symptom:** later work re-derives a design that was settled in an Issue comment.

**Mechanism:** Linear is excellent for volatile coordination and is not the repository's durable memory.

**Rule:** Issues carry questions and work; Decision Records carry decisions; guides, plans, audits and
assimilation briefs carry the rest. Extract the invariant, link the artifact from the Issue, and never
mirror an Issue body into the repository.

## Related

- [Working-Tree Contract](./working-tree-contract.md) — the procedure most of these rules point at
- [Writing Good Briefs](./writing-good-briefs.md) — the contract an Issue or a worker starts from
- [Testing Strategy](./testing-strategy.md) — what the gates are worth
- [DR 045 — Flow Map, Hats and Workers, Review Tiers, and the Working-Tree Contract](../project/decisions/045-flow-map-hats-review-tiers-and-working-tree-contract.md)
- [DR 044 — Linear-Canonical PM and the dydo Knowledge Boundary](../project/decisions/044-linear-canonical-pm-and-dydo-knowledge-boundary.md)
