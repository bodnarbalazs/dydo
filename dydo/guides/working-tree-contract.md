---
area: guides
type: guide
---

# Working-Tree Contract

The bird's-eye view of how a Project moves through branches and worktrees. A Project's map holder,
the user or an invoked admiral, uses it to know what to expect from Ticket Captains and their lane
crew; Ticket Captains use it to know what they own and what they hand back. The `chief-of-staff` audits failures of this contract, not routine cleanup.

## Invariants

- Linear assignment is the **claim**. One Ticket Captain owns the parent ticket and its integrated outcome
  end to end.
- Every parent ticket has an isolated integration worktree. Independently trackable parallel lanes may
  become direct Sub-tickets, each with an isolated branch and worktree. Parallel crew write only the
  disjoint paths their Ticket Captain grants; the Bug Type-map exception below transfers paths serially.
- Commits touch owned paths only. The Ticket Captain owns the whole diff, including crew edits.
- Every crew **hop** ends on a commit: the return names its SHA, the Ticket Captain posts it on the
  record, and the ticket branch keeps its hops unsquashed and unrewritten.
- Branch, base SHA and worktree path are posted on the relevant ticket or Sub-ticket before the first edit.
- Project tickets integrate serially through their feature branch; the user alone lands the feature on
  `main`/`master` (the repository's default branch, whatever it is named).

## Branches and targets

| Work | Branches from | Branch | PR targets | Merger |
|---|---|---|---|---|
| Project | `main` | `feature/<project-slug>` | `main` | user |
| Project ticket | its feature branch | `DYD-123-<slug>` | its feature branch | its captain-directed Merge Sub-ticket |
| Atomic ticket | `main` | `DYD-123-<slug>` | `main` | user, clicking its PR once it carries a reviewer PASS and CI is green; no Merge Sub-ticket |
| Lane Sub-ticket | its parent ticket branch | `DYD-124-<slug>` | its parent ticket branch | its captain-directed Merge Sub-ticket |
| Inquisition Ticket | integrated feature SHA | `inquisition/<slug>` | none; never merges | - |
| Proof branch | the Inquisition's audit SHA | a child branch named in the proof-only brief | none; never merges | - |
| Prototype Ticket | its feature branch, else `main` | `prototype/<name>` | none; never merges | - |

`DYD-123` is an example: use the ticket's key so Linear attaches the branch and PR. The host may
provide the ticket worktree; otherwise place it beside the repository at
`../<repo>.worktrees/DYD-123-<slug>`.

## Who does what

| Stage | Owner | Required state |
|---|---|---|
| Open the Project | the map holder, commissioning the first Ticket Captain | The first Captain opens the feature branch from the approved main SHA and reports it; the Project map is in Linear; every ticket carries outcome, owned paths, blockers, exact gates and base branch. Only then is a ticket pickable. |
| Claim the ticket | `ticket-captain` | Ticket is assigned; its branch and isolated worktree exist; branch, base SHA and worktree path are on the ticket. The captain sets `Implementing` when it spawns the author, a code-writer or a docs-writer. |
| Resolve the work | `ticket-captain` | The captain's compact acceptance contract names the lanes, or none; when the work splits beyond it, the writer's one pre-code comment names them. Parallel crew receive disjoint paths, the ticket's feature files among them, and exact gates; independently trackable parallel lanes become direct Sub-tickets. |
| Open a parallel lane | `ticket-captain` | The Sub-ticket carries the parent's Type and Mode, its own chain, status and evidence, a disjoint owned-path subset, exact gates, child-key branch, parent-branch base SHA and isolated worktree. |
| Build and prove | crew | Changes stay inside owned paths; exact gates pass; each hop ends on a commit `<KEY> <hop>: <what>`, the hop being `implement`, `fix` after a FAIL, `merge`, or `proof`, which commits as `<KEY> proof: <hypothesis>`; review evidence stays on the work item reviewed; every return comes back to the Ticket Captain. |
| Review and offer | `ticket-captain` | Passed lane branches are integrated into the parent ticket branch; combined gates pass; a fresh parent ticket-review PASS block is on the ticket and in the PR; the branch is pushed and the PR targets the branch in the table above. |
| Integrate a Project ticket | `ticket-captain` | Its final Merge Sub-ticket runs a code-writer - conflicts and combined gates mapped, the merge performed, a resolution that refactored leaving its code no worse than either side - then a fresh merge reviewer, preserving the merge commit and hop SHAs; the map holder wires the order and may advance an independent ready PR. Parent stays `Ready to Merge` until merge PASS, then both close `Done`. |
| Integrate an atomic ticket | `ticket-captain` | No Merge Sub-ticket: the PR into main carries its reviewer PASS, the captain confirms CI green and sets `Ready to Merge`, and the user clicks the merge. The captain then closes the ticket `Done` and cleans up. |
| Land the Project | user | The landing Merge Ticket prepares main into feature and obtains acceptance PASS; the user clicks feature into main as a merge commit, never squash. |

## Before the first edit

Before the first edit of an `implement` or `fix` hop in a parent ticket or lane, its assigned writer
proves all five checks and returns a failure to its Ticket Captain instead of working around it:

1. `HEAD` is on the relevant ticket or Sub-ticket branch.
2. The repository root is the isolated worktree, not the main checkout.
3. The posted base SHA is an ancestor of `HEAD`.
4. The worktree is clean.
5. Every path the work item will touch is in the ticket's owned paths.

Merge work proves instead the pins of the code-writer's `merge` resource: source and target at their
SHAs, the source's PASS, the governing contract and the combined gates. Proof-only work proves three
things: `HEAD` is on a child branch cut from the audit SHA and named in its brief, the worktree is
clean, and only the test path is touched.

## Delivery scale and evidence

The default crew is one author - `code-writer`, or `docs-writer` for a documentation change - and
fresh independent whole-change review, whose first round on a candidate seats two reviewers in
parallel, one Claude and one Codex. Add a spec review of the contract before any code only
when one short, concrete risk reason, recorded in the contract, calls for it; persistence, migrations,
permissions and uncertain native interfaces are examples of such a risk. Keep one compact acceptance contract and point to evidence rather than copying
it. This scale rule does not weaken required G/M, integration or release gates, and skipped native
proof is never runtime proof.

Before an expensive test, cheaply prove the repository or snapshot, intended selection and nonzero
discovery. A `code-writer` hop runs the tests relevant to the change while building, then the full
suite and static gate of each changed stack once before it returns, and its review judges that run.
Other crew hops and their reviews prove with the tests relevant to the change and the cheap static
checks. The full suites and the whole gate set run at the ticket's final gates, its Merge
Sub-ticket's combined gates and the landing. Within one such gate run, each suite executes once.
Record every gate result with candidate, command, environment or session, exit and result
location; do not interrupt a quiet healthy test merely because it is silent. Reuse exact-candidate
evidence only where the gate and environment permit it, rerunning after relevant changes or new
concerns and at distinct mandatory integration or release boundaries. Validate uncertain native
interface shapes early.

## Delegation

- The Admiral budgets open native capacity before commissioning or resuming a captain. The captain alone
  commissions exact-scope crew, budgets that capacity and preserves the fresh reviewer
  obligation. A saved brief is not Admiral-to-crew authority.
- AFK work, captain or crew, runs as a child task of its dispatcher; HITL work, which the user joins,
  runs as a new top-level thread, never a child. Within one vendor, use the host's native subagents
  and messaging where they carry the work and its return. When any agent in the exchange is another
  vendor's, always use T3's `delegate_task` for a child and T3 messaging for a peer; without T3, use
  what the host supplies, or none. A return comes back once, on the channel it was dispatched on.
  Open a top-level thread only for HITL work or when the user asks for one; on `parent_not_active`,
  hand the dispatch up to your officer.
- On a bounded refusal, preserve the record, candidate, hop SHA and exact brief; do not broaden it or
  retry blindly. Use established lifecycle handling, then return or release the concrete limitation
  when captain-owned work cannot run. Record only observed native evidence.
- Crew inherit the relevant ticket or Sub-ticket contract, owned paths and gates, and commit their
  own hop. Ordinary crew do not create its branch, open its PR, merge it or review their own
   work. A captain-directed Merge code-writer performs the specified merge and conflict resolutions;
   a fresh reviewer judges that integrated candidate.
- Fan-out is safe only across disjoint paths. Each independently trackable parallel lane is a direct
  Sub-ticket of the captain's ticket. Lanes have one level; the Bug stages below, Merge and map-holder-held Sub-tickets are the other permitted children. If a lane needs splitting, replace
  it with sibling lanes under the parent ticket.
- An agent invocation is recorded as comments and evidence on the relevant ticket or Sub-ticket, never
  as another child. Successive agents may work through the same record.
- A Bug may retain its Type template's ordered reproduce-or-identify and fix Sub-tickets. Fix is
  natively blocked by reproduction; shared paths transfer only after the reproduction closes with
  its evidence recorded. Each stage has a contract, branch and worktree; every actual integration
  has a Merge Sub-ticket. A simple Bug collapses the placeholders into parent hops and closes the
  unused records `Canceled` with the reason. This exception does not permit overlapping parallel lanes.
- The Ticket Captain directs collision resolution and integration of review-passed branches through
  their Merge code-writers, then verifies the integrated result. Each writer stages owned paths by
  name; a whole-tree add can capture another writer's work.
- The Ticket Captain consumes every crew return and remains accountable for the ticket, evidence and
  complete diff. After integration it verifies the crew's combined proof and obtains the final parent review.

## Return and release

The captain offers a PR with its PASS block, sets `Ready to Merge`, and returns
`done <key>: PR ready`. It resumes when its Merge Sub-ticket's native blocker clears, or a fresh
captain takes the record, and returns `done <key>: merged` after merge PASS and cleanup. The record
holds the detail; the captain posts each crew hop's SHA. A Merge Sub-ticket never enters `Ready to Merge`.
An atomic ticket has no Merge Sub-ticket: its captain sets `Ready to Merge` once CI is green, and after
the user's click closes it `Done`, cleans up and returns `done <key>: merged`.

For an uncleared blocker or user takeover: push, post the resume SHA, clean the worktree (`dydo clean --dry-run <worktree>`, then `dydo clean <worktree>`, run from outside that tree), set the
parent `Todo`, unassign and wire any blocker; return `released <key>: <reason>`. A dead session is
treated as release from its last recorded hop without a final push. Fresh commission from the
record works on both hosts. An invoked admiral wakes on a captain's return or the user's word and rereads
the board, including released and blocker-cleared tickets.

Merge FAIL stays owned: fix integration defects inside Merge; revert a source defect there and
close Merge `Canceled`, returning the source to `Implementing`. If a later merge depends on it,
use a following fix ticket. Each corrected candidate gets fresh merge review.

## Retaining an Inquisition's record and proofs

The Inquisition captain pins the completed evidence packet on its ticket: audited feature SHA,
scope, parts and lenses, findings, hypotheses with verdicts, and deduplicated Bugs with their
reproduction SHAs, or a prose Bug's quoted passages. The captain pushes and posts its resume state, cleans its worktree (`dydo clean --dry-run <worktree>`, then `dydo clean <worktree>`, run from outside that tree), sets the
Inquisition `Todo`, unassigns and returns `released <key>: record delivery`. The record Feature and
its blocker do not exist yet; this release return reaches the Project's map holder.

On that return, before generic pickable commissioning, the map holder uses the packet to contract a
separate primary `Feature` / `AFK` record-delivery ticket from the retained feature branch. Its ownership names the exact
`dydo/project/inquisitions/` record file and exact authored navigation paths only when the record-delivery ticket contract requires them. Its own
captain directs the ordinary specification, docs-writer, docs review and final Merge Sub-ticket
into the feature. Audit and proof branches never merge.

First wire the Inquisition **blocked by the record Feature**, never the reverse, then commission
record delivery from the completed packet. The map holder does not recommission the Inquisition from
its temporary `Todo` gap. On the normal board loop after record delivery is `Done`, the map holder
resumes that captain from the ticket. It returns to
`In Progress`, verifies the filed Bugs and the exact record path and blob or content digest on the
retained feature, and records the delivery merge's ancestry/reachability there. Only then does it
close `Done`, return `done <key>` and delete the audit branch. A record present only on an unmerged
audit branch cannot satisfy this check.

Before release or audit cleanup, every open Bug with a reproduction commit keeps its SHA reachable
from a pushed named ref independent of the audit branch, linked on that Bug. A prose Bug carries
its quoted passages instead and has no ref to retain. The Inquisition captain owns
that retention until the Bug captain records adoption of the reproduction and transfer of cleanup
responsibility. Audit cleanup never deletes those refs; they remain input to each Bug's normal
chain. This is ordinary Feature delivery and release, not a new Type or a serial-lane exception.

## Cleanup

| Artifact | Accountable | Completion |
|---|---|---|
| Parent ticket and lane worktrees | `ticket-captain` | Every worktree it or its crew created is cleaned with `dydo clean --dry-run <worktree>`, then `dydo clean <worktree>`, run from outside that tree. A spawned Ticket Captain first pushes the parent branch and opens its PR so the work survives its return. |
| Integrated lane Sub-ticket branch | `ticket-captain` | The branch is deleted after it passes review and is integrated into the parent ticket branch. |
| Merged Project ticket branch | `ticket-captain` | The branch is deleted after the merge. |
| Merged atomic ticket branch | `ticket-captain` | The branch is deleted after the user clicks its PR into main. |
| Merged feature branch | the map holder, commissioning the landing Captain | The landing Captain deletes the branch after the user lands it and reports completion. |
| Prototype branch | `ticket-captain`, tracked by the map holder | Keep the winning code linked as delivery-spec input; delete when that delivery ticket is Done or with feature cleanup. |
| Inquisition branch | `ticket-captain` | Delete at Done after the record/proof retention checks above; never merge it or delete the independently retained Bug refs. |

`dydo clean` deletes a local branch only once origin's default branch holds it. Deleting a branch here means the local branch. GitHub deletes a merged PR's remote branch on merge,
and the guard blocks agents from deleting remote branches, so the accountable officer names any
other remote branch due for deletion on its record for the user.

The `chief-of-staff` compares `git worktree list` and merged branches with Linear during board hygiene.
Anything it finds is a contract failure to clear or route with its owner named, not normal cleanup
delegated to staff.

## Related

- [dydo Glossary](../reference/dydo-glossary.md) - locked delivery vocabulary
