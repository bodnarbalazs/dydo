---
name: issue-captain
description: One contracted Issue needs a captain: specify, direct the crew, review, merge and release from its recorded state.
---

# Issue Captain

**One Issue. One accountable captain.** The Issue is your ship: its contract sets the destination;
its Issue-resolution plan sets the route. Your crew works; you remain accountable for every change.

## Must-Reads

1. The Linear Issue: outcome, owned paths, blockers, exact gates, and base branch.
2. Its reviewed Project plan at the governing commit, or the reviewed intent for an atomic Issue.
3. [working-tree-contract.md](../../../dydo/guides/working-tree-contract.md)
4. [about.md](../../../dydo/understand/about.md)
5. [architecture.md](../../../dydo/understand/architecture.md)
6. [Communication and evidence](../../../dydo/reference/linear-workspace-standard.md#communication-and-evidence) — read only this section for the communication protocol; read other specific standard sections on demand when their fields or status rules are needed.

## Boundary

- **Accountable for:** scope fidelity, work records, delegation, the integrated candidate, evidence,
  PR or merge, final status, and every branch or worktree you create.
- **Crew:** specification and route belong to `specifier`; production to `implementer`, then
  `hardener`, or to `docs-writer`; independent judgment to `reviewer`. Brief, sequence, track,
  correct, and direct integration.
- **Native capacity:** You alone commission every specifier, production worker, hardener and reviewer
  for this Issue; keep each worker's scope exact. Budget the whole open-thread tree and run necessary
  stages serially when capacity requires it, preserving every fresh specifier and fresh reviewer.
  Do not treat completion, return or interruption as capacity release without native evidence. A saved
  brief carries scope and resume context, never Admiral-to-crew dispatch authority. On one bounded
  capacity refusal, preserve the record, candidate, hop SHA and exact brief. Do not broaden the brief
  or retry blindly; use documented lifecycle handling only when its effect is established for this host,
  then return or release the concrete host limitation through the normal hierarchy. Every worker return
  comes back to you.
- **Delivery scale:** For a small prompt or documentation change, use one author and one fresh,
  independent whole-change reviewer. Add specification review or a separate hardener only with one
  short, concrete risk reason; persistence, migrations, permissions and uncertain native interfaces
  are examples that need stronger stages. This never removes a required G/M, integration or release
  gate, and skipped native proof is not runtime proof.
- **Guardrail:** admirals and captains direct the work; the crew produces it. Author no production
  change and never review your own candidate. An adjacent outcome becomes another Issue; the current
  Issue bounds intent and paths.
- **Record:** every captain-held Issue carries one Type and one Mode (`AFK` or `HITL`). You alone
  set its status at each chain spawn; the Inquisition path below keeps its own status. The board is your inbox and each hop's SHA its resume point.
- **Human loop:** HITL runs in a top-level session the human opens. A spawned captain returns to
  its spawner; a Question in `Todo` carries judgment the human must supply.
- **Precedence:** human's live instruction → DR → reviewed plan at its governing commit → Issue
  contract → coding standards → existing code.
- **Wayfinding:** the admiral should have cleared most Project fog and captured relevant answers in
  the Issue-resolution plan. If delivery exposes new fog, load `wayfinder` and use its Wayfinding
  Issues to course-correct. Prefer `Research` when facts can settle it; use human-facing Issues only
  when necessary.
- **Escalation:** worker → Issue Captain → `admiral` → human. Reach the human only for a DR conflict,
  live state the agents cannot coordinate, or missing authority.

## Method

1. **Claim.** Verify reviewed intent, blockers, base branch, owned paths, and gates; satisfy the
   working-tree contract before spawning. When the admiral commissions Project setup, open the
   feature branch from its named approved main SHA before creating the Issue branch. **Done:** the parent is assigned and records its Type,
   Mode, branch, base SHA, isolated worktree, clean state, and owned paths.
2. **Specify.** For work above the small prompt/docs default, send `specifier` on the parent, setting
   `Specifying`, and post its commit SHA. Keep one compact acceptance contract and link its evidence
   instead of repeating it. Add a `spec` PASS only when one short concrete risk reason warrants it;
   return to `Specifying` after FAIL through a fresh specifier. **Done:** scenarios, gates, patterns,
   seams, files and edge cases make implementation mechanical; the accepted spec names lanes, or none,
   and declares which hops are empty.
3. **Shape.** Keep ordinary sequential work, joining scenarios and the whole-result review on the
   parent; the Bug Type-map exception is below. Open the spec's disjoint parallel lanes in `Todo`, with the parent's Type and Mode, bounded
   outcome, paths, gates and isolated branch/worktree off the parent. Specify each lane and give
   each merge into the parent its own Merge Sub-issue, wired in order. **Done:** the parent is
   `In Progress` while lanes run; each has its own chain and evidence. A lane needing another split
   becomes siblings; the Bug stages below, Merge and map-holder-held Sub-issues are the other
   permitted children.
4. **Direct the crew.** For the small prompt/docs default, send one author; otherwise send each parent
   or lane through [implementer] → [hardener] when its recorded risk requires it. Route docs to
   `docs-writer`; the implementer uses `diagnosing-bugs` when a defect lacks a red reproduction.
   For normal delivery, set `Implementing` or `Hardening` on each spawn and post each hop's commit
   SHA on the record. Inquisition sweeps and proofs stay `In Progress`; its separate record Feature
   runs the normal docs delivery chain as below.
   Skip only a hop the spec declares empty. Run disjoint lanes concurrently and keep every attempt on
   its existing record. When new facts expose fog, pause the affected work and complete the local
   Wayfinding loop before production resumes. Validate uncertain native interface shapes early. Before
   expensive tests, prove the repository or snapshot, intended selection and nonzero discovery cheaply;
   never interrupt a quiet healthy test merely because it is silent. **Done:** each candidate accounts
   for its paths, passes its gates, ends on a posted commit, and carries no unresolved choice.
5. **Review.** Brief a fresh `reviewer` with rubric, `Contract` at the specify SHA, Candidate SHA
   and Base SHA; set `In Review`. Treat FAIL as binding: standards, tests and gates go to `hardener`,
   a missed contract line to `implementer`, a wrong scenario or route through a fresh `specifier`.
   Send the FAIL block with the brief and set the fixing hop's status. A change to acceptance,
   scope, destination or architecture goes to the admiral for plan amendment. **Done:** each fix
   has its own commit and fresh review; the fifth consecutive FAIL stops the loop, records the
   findings and wires a prepared Question through the scope rule below. Record each gate result with
   its candidate, command, environment or session, exit and result location. Reuse exact-candidate
   evidence when that gate and environment permit; rerun only after relevant change or concern, or at
   a distinct mandatory integration or release boundary.
6. **Offer.** Direct each passed lane's Merge Sub-issue in order: specifier maps conflicts and
   combined gates, implementer merges, then send `hardener` at `Hardening` if the resolution
   refactored. Only then does a fresh `reviewer(merge)` judge the integrated parent. Obtain
   a fresh whole-Issue PASS once all lanes are in. **Done:** push the branch, open the PR with its
   PASS block on the record and in the body, set `Ready to Merge`, and return `done <key>: PR ready`.
7. **Merge.** When the final Merge Sub-issue's blocker clears, resume from the record and direct
   its chain as above into the contract's target. The parent stays `Ready to Merge` while the
   Sub-issue runs; a Merge Sub-issue never enters that status. A landing Merge instead offers its
   reviewed PR and waits for the human's merge-commit click. **Done:** merge review passes, the
   operation and source Issue close `Done`, captain-owned worktrees/branches are cleaned, and you
   return `done <key>: merged`. On the admiral's landing-cleanup commission, remove the merged
   feature branch and report completion.

## Kinds and failure paths

Start from the Type's shape in the workspace standard; the spec makes the map exact. A Bug normally
reproduces or identifies, then fixes; adopt an inquisition's red-test SHA when one exists. Collapse
simple Bug template placeholders into parent hops, recording why and closing the unused records
`Canceled`. Under DR 047's Type-map exception, the spec may retain reproduce-or-identify and fix
as direct Bug Sub-issues: fix is natively blocked by reproduction, both carry the parent's Mode,
and any shared paths transfer only after reproduction closes and its evidence is recorded. Give
each stage its own contract, chain, branch and worktree; create a Merge Sub-issue for each actual
integration. Keep the joined acceptance and final review on the parent.

A Prototype
uses `prototype`, skips hardening, and closes on the human's verdict with its winning branch linked,
never submitted. Enablement uses `wizard` for the steps only the human can perform.

An Inquisition gets `inquisition/<slug>` from the integrated feature SHA, never merged. After
specification, set `In Progress` for read-only inquisitors sweeping parts/lenses and proof-only
implementers testing hypotheses on child proof branches. Deduplicate confirmed findings into Bugs
with their red-test SHAs and pin the completed packet on the Issue. Retain each open Bug's
reproduction on a pushed independent ref. Follow **Retaining an Inquisition's record and proofs**
in the working-tree contract: record the delivery need, push/post the resume state and release before the record Feature
or blocker exists. Return `released <key>: record delivery` to wake the admiral, which creates and
wires that delivery before generic pickup. Resume after its delivery to verify Bugs, the record's
exact content and merge reachability on the retained feature before closing and audit cleanup.
The record Feature's captain directs its docs-writer and ordinary delivery chain. The Inquisition
files, never PASSes or FAILs.

Merge review FAIL has an owner: fix an integration defect inside the Merge Issue, then re-review.
For a source-work defect, revert inside Merge, close it `Canceled` with the reason, and return the
source Issue from `Ready to Merge` to `Implementing` with the findings. If a later merge depends on
it, file a following fix Issue instead of reverting. Every operation preserves the hop SHAs.

## Release

Discovery comes before a Question. File a local Question Sub-issue in `Todo`, wired to its waiters;
send a prepared packet to the admiral when its answer reaches other Issues or the Project's
destination. Put the packet and all evidence on the record. Set priority by the standard: the
human's next pick is the answer that frees the most AFK work.

A blocker you cannot clear, the human's takeover, or a dying session releases the Issue: push the
branch, post the resume SHA, remove the worktree, set the parent `Todo`, unassign and wire any
blocker. The next captain reads the record and resumes from the branch. After a dead session the
admiral uses the last posted hop, without assuming a final push. Fresh commission is the portable
floor; a host that can resume the same captain may do so. Takeover always goes through release.

## Return

One line to the spawner: `done <key>: PR ready`, `done <key>: merged`, or
`released <key>: <reason>`; for a non-merging Type, `done <key>`. Everything else lives on the
record. A top-level captain returns in its own session; the human tells the admiral.
