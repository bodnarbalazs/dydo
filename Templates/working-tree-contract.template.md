---
area: guides
type: guide
---

# Working-Tree Contract

The bird's-eye view of how a Project moves through branches and worktrees. Admirals use it to know
what to expect from Issue Captains and their lane workers; Issue Captains use it to know what they own
and what they hand back. The `chief-of-staff` audits failures of this contract, not routine cleanup.

## Invariants

- Linear assignment is the **claim**. One Issue Captain owns the parent Issue and its integrated outcome
  end to end.
- Every parent Issue has an isolated integration worktree. Independently trackable parallel lanes may
  become direct Sub-issues, each with an isolated branch and worktree. Workers write only the disjoint
  paths their Issue Captain grants.
- Commits touch owned paths only. The Issue Captain owns the whole diff, including worker edits.
- Branch, base SHA and worktree path are posted on the relevant Issue or Sub-issue before the first edit.
- Project Issues integrate serially through their feature branch; the human alone lands the feature on
  `main`/`master` (the repository's default branch, whatever it is named).

## Branches and targets

| Work | Branches from | Branch | PR targets | Merger |
|---|---|---|---|---|
| Project | `main` | `feature/<project-slug>` | `main` | human |
| Project Issue | its feature branch | `DYD-123-<slug>` | its feature branch | `admiral` |
| Atomic Issue | `main` | `DYD-123-<slug>` | `main` | `issue-captain` |
| Lane Sub-issue | its parent Issue branch | `DYD-124-<slug>` | its parent Issue branch | `issue-captain` |
| Prototype Issue | its feature branch, else `main` | `prototype/<name>` | none; never merges | — |

`DYD-123` is an example: use the Issue's key so Linear attaches the branch and PR. The host may
provide the Issue worktree; otherwise place it beside the repository at
`../<repo>.worktrees/DYD-123-<slug>`.

## Who does what

| Stage | Owner | Required state |
|---|---|---|
| Open the Project | `admiral`, or the human when there is none | Feature branch exists; the Project map is in Linear; every Issue carries outcome, owned paths, blockers, exact gates and base branch. Only then is an Issue pickable. |
| Claim the Issue | `issue-captain` | Issue is assigned and In Progress; its branch and isolated worktree exist; branch, base SHA and worktree path are on the Issue. |
| Resolve the work | `issue-captain` | The Issue-resolution plan makes the work mechanical; workers receive disjoint paths and exact gates; independently trackable parallel lanes become direct Sub-issues. |
| Open a parallel lane | `issue-captain` | The Sub-issue carries its own status and evidence, a disjoint owned-path subset, exact gates, child-key branch, parent-branch base SHA and isolated worktree. |
| Build and prove | workers | Changes stay inside owned paths; exact gates pass; review evidence stays on the work item reviewed; every return comes back to the Issue Captain. |
| Review and offer | `issue-captain` | Passed lane branches are integrated into the parent Issue branch; combined gates pass; a fresh parent Issue-review PASS block is on the Issue and in the PR; the branch is pushed and the PR targets the branch in the table above. |
| Integrate a Project Issue | `admiral` | Passed PRs merge one at a time, in plan order and with `--no-ff`; each merge is followed by a fresh merge review over the integrated feature branch. |
| Integrate an Atomic Issue | `issue-captain` | Issue review passes; the PR merges to `main`; a fresh merge review follows over the integrated state. |
| Land the Project | human | The reviewed feature branch merges to `main`. |

## Before the first edit

Before the first edit in a parent Issue or lane, its assigned writer proves all five checks and comments
on that work item instead of working around a failure:

1. `HEAD` is on the relevant Issue or Sub-issue branch.
2. The repository root is the isolated worktree, not the main checkout.
3. The posted base SHA is an ancestor of `HEAD`.
4. The worktree is clean.
5. The work item owns every path named in its plan.

## Delegation

- Workers inherit the relevant Issue or Sub-issue contract, owned paths and gates. They do not create
  its branch, open its PR, merge it or review their own work.
- Fan-out is safe only across disjoint paths. Each independently trackable parallel lane is a direct
  Sub-issue of the captain's Issue; Sub-issues never have children. If a lane needs splitting, replace
  it with sibling lanes under the parent Issue.
- An agent invocation is recorded as comments and evidence on the relevant Issue or Sub-issue, never
  as another child. Successive agents may work through the same record.
- The Issue Captain resolves every collision, integrates review-passed lane branches into the parent
  Issue branch and stages owned paths by name; a whole-tree add can capture another writer's work.
- The Issue Captain consumes every worker return and remains accountable for the Issue, evidence and
  complete diff. After integration it proves the combined state and obtains the final parent review.

## Cleanup

| Artifact | Accountable | Completion |
|---|---|---|
| Parent Issue and lane worktrees | `issue-captain` | Every worktree it or its workers created is removed. A spawned Issue Captain first pushes the parent branch and opens its PR so the work survives its return. |
| Integrated lane Sub-issue branch | `issue-captain` | The branch is deleted after it passes review and is integrated into the parent Issue branch. |
| Merged Project-Issue branch | `admiral` | The branch is deleted after the merge. |
| Merged Atomic-Issue branch | `issue-captain` | The branch is deleted after the merge. |
| Merged feature branch | `admiral` | The branch is deleted after the human lands it. |
| Prototype branch | `admiral` | Deleted with the feature branch; the verdict is already on the Prototype Issue. |

The `chief-of-staff` compares `git worktree list` and merged branches with Linear during board hygiene.
Anything it finds is a contract failure to clear or route with its owner named, not normal cleanup
delegated to staff.

## Related

- [dydo Glossary](../reference/dydo-glossary.md) — locked delivery vocabulary
