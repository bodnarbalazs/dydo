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
  become direct Sub-issues, each with an isolated branch and worktree. Parallel workers write only the
  disjoint paths their Issue Captain grants; the Bug Type-map exception below transfers paths serially.
- Commits touch owned paths only. The Issue Captain owns the whole diff, including worker edits.
- Every worker **hop** ends on a commit: the return names its SHA, the Issue Captain posts it on the
  record, and the Issue branch keeps its hops unsquashed and unrewritten.
- Branch, base SHA and worktree path are posted on the relevant Issue or Sub-issue before the first edit.
- Project Issues integrate serially through their feature branch; the human alone lands the feature on
  `main`/`master` (the repository's default branch, whatever it is named).

## Branches and targets

| Work | Branches from | Branch | PR targets | Merger |
|---|---|---|---|---|
| Project | `main` | `feature/<project-slug>` | `main` | human |
| Project Issue | its feature branch | `DYD-123-<slug>` | its feature branch | its captain-directed Merge Sub-issue |
| Atomic Issue | `main` | `DYD-123-<slug>` | `main` | its captain-directed Merge Sub-issue |
| Lane Sub-issue | its parent Issue branch | `DYD-124-<slug>` | its parent Issue branch | its captain-directed Merge Sub-issue |
| Inquisition Issue | integrated feature SHA | `inquisition/<slug>` | none; never merges | — |
| Prototype Issue | its feature branch, else `main` | `prototype/<name>` | none; never merges | — |

`DYD-123` is an example: use the Issue's key so Linear attaches the branch and PR. The host may
provide the Issue worktree; otherwise place it beside the repository at
`../<repo>.worktrees/DYD-123-<slug>`.

## Who does what

| Stage | Owner | Required state |
|---|---|---|
| Open the Project | `admiral`, commissioning the first Issue Captain | The first Captain opens the feature branch from the approved main SHA and reports it; the Project map is in Linear; every Issue carries outcome, owned paths, blockers, exact gates and base branch. Only then is an Issue pickable. |
| Claim the Issue | `issue-captain` | Issue is assigned; the captain sets `Specifying` when spawning its specifier; its branch and isolated worktree exist; branch, base SHA and worktree path are on the Issue. |
| Resolve the work | `issue-captain` | The parent spec names the lanes and empty hops; the spec and plan make the contract exact and work mechanical; parallel workers receive disjoint paths, the Issue's feature files among them, and exact gates; independently trackable parallel lanes become direct Sub-issues. |
| Open a parallel lane | `issue-captain` | The Sub-issue carries the parent's Type and Mode, its own chain, status and evidence, a disjoint owned-path subset, exact gates, child-key branch, parent-branch base SHA and isolated worktree. |
| Build and prove | workers | Changes stay inside owned paths; exact gates pass; each hop ends on a commit `<KEY> <hop>: <what>`, the hop being `specify`, `implement`, `harden` or `fix`; review evidence stays on the work item reviewed; every return comes back to the Issue Captain. |
| Review and offer | `issue-captain` | Passed lane branches are integrated into the parent Issue branch; combined gates pass; a fresh parent Issue-review PASS block is on the Issue and in the PR; the branch is pushed and the PR targets the branch in the table above. |
| Integrate a Project Issue | `issue-captain` | Its final Merge Sub-issue runs specifier → implementer → hardener if resolution refactored → fresh merge reviewer, preserving the merge commit and hop SHAs; the admiral wires the order and may advance an independent ready PR. Parent stays `Ready to Merge` until merge PASS, then both close `Done`. |
| Integrate an Atomic Issue | `issue-captain` | The final Merge Sub-issue merges to main, reruns combined gates and obtains fresh merge review, as at every other level. |
| Land the Project | human | The landing Merge Issue prepares main into feature and obtains acceptance PASS; the human clicks feature into main as a merge commit, never squash. |

## Before the first edit

Before the first edit in a parent Issue or lane, its assigned writer proves all five checks and comments
on that work item instead of working around a failure:

1. `HEAD` is on the relevant Issue or Sub-issue branch.
2. The repository root is the isolated worktree, not the main checkout.
3. The posted base SHA is an ancestor of `HEAD`.
4. The worktree is clean.
5. The work item owns every path named in its plan.

## Delegation

- Workers inherit the relevant Issue or Sub-issue contract, owned paths and gates, and commit their
  own hop. Ordinary workers do not create its branch, open its PR, merge it or review their own
   work. A captain-directed Merge implementer performs the specified merge and conflict resolutions;
   a fresh reviewer judges that integrated candidate.
- Fan-out is safe only across disjoint paths. Each independently trackable parallel lane is a direct
  Sub-issue of the captain's Issue. Lanes have one level; the Bug stages below, Merge and map-holder-held Sub-issues are the other permitted children. If a lane needs splitting, replace
  it with sibling lanes under the parent Issue.
- An agent invocation is recorded as comments and evidence on the relevant Issue or Sub-issue, never
  as another child. Successive agents may work through the same record.
- A Bug may retain its Type template's ordered reproduce-or-identify and fix Sub-issues. Fix is
  natively blocked by reproduction; shared paths transfer only after the reproduction closes with
  its evidence recorded. Each stage has a contract, branch and worktree; every actual integration
  has a Merge Sub-issue. A simple Bug collapses the placeholders into parent hops and closes the
  unused records `Canceled` with the reason. This exception does not permit overlapping parallel lanes.
- The Issue Captain directs collision resolution and integration of review-passed branches through
  their Merge implementers, then verifies the integrated result. Each writer stages owned paths by
  name; a whole-tree add can capture another writer's work.
- The Issue Captain consumes every worker return and remains accountable for the Issue, evidence and
  complete diff. After integration it verifies the crew's combined proof and obtains the final parent review.

## Return and release

The captain offers a PR with its PASS block, sets `Ready to Merge`, and returns
`done <key>: PR ready`. It resumes when its Merge Sub-issue's native blocker clears, or a fresh
captain takes the record, and returns `done <key>: merged` after merge PASS and cleanup. The record
holds the detail; each worker hop posts its SHA. A Merge Sub-issue never enters `Ready to Merge`.

For an uncleared blocker or human takeover: push, post the resume SHA, remove the worktree, set the
parent `Todo`, unassign and wire any blocker; return `released <key>: <reason>`. A dead session is
treated as release from its last recorded hop without a final push. Fresh commission from the
record works on both hosts. The admiral wakes on a captain's return or the human's word and rereads
the board, including released and blocker-cleared Issues.

Merge FAIL stays owned: fix integration defects inside Merge; revert a source defect there and
close Merge `Canceled`, returning the source to `Implementing`. If a later merge depends on it,
use a following fix Issue. Each corrected candidate gets fresh merge review.

## Cleanup

| Artifact | Accountable | Completion |
|---|---|---|
| Parent Issue and lane worktrees | `issue-captain` | Every worktree it or its workers created is removed. A spawned Issue Captain first pushes the parent branch and opens its PR so the work survives its return. |
| Integrated lane Sub-issue branch | `issue-captain` | The branch is deleted after it passes review and is integrated into the parent Issue branch. |
| Merged Project-Issue branch | `issue-captain` | The branch is deleted after the merge. |
| Merged Atomic-Issue branch | `issue-captain` | The branch is deleted after the merge. |
| Merged feature branch | `admiral`, commissioning the landing Captain | The landing Captain deletes the branch after the human lands it and reports completion. |
| Prototype branch | `issue-captain`, tracked by the admiral | Keep the winning code linked as delivery-spec input; delete when that delivery Issue is Done or with feature cleanup. |
| Inquisition branch | `issue-captain` | Delete at Done after Bugs and the record exist; never merge it. |

The `chief-of-staff` compares `git worktree list` and merged branches with Linear during board hygiene.
Anything it finds is a contract failure to clear or route with its owner named, not normal cleanup
delegated to staff.

## Related

- [dydo Glossary](../reference/dydo-glossary.md) — locked delivery vocabulary
