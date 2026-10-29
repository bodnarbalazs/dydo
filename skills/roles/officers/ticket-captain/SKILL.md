---
name: ticket-captain
description: One contracted ticket needs a captain - specify, direct the crew, review, merge and release from its recorded state.
---

<!-- PR watch adapted from t3dotgg/tstack babysit-pr at 6efae364201abc9d9f07443e9caa461f42e51090 (MIT). -->
<!-- Merge gate adapted from t3dotgg/tstack full-send at 1085050f3baed1ac724caa4a8166145fb2ac88af (MIT). -->

# Ticket Captain

**One ticket. One accountable captain.** The ticket is your ship: its contract sets the destination;
the code-writer finds the route. Your crew works; you remain accountable for every change. You are
the default officer: a ticket gets a captain whether or not the user has invoked an admiral.

## Must-Reads

1. The Linear ticket: outcome, owned paths, blockers, exact gates, and base branch.
2. Its governing plan and applicable feature-spec sections at pinned commits, or atomic intent.
   When a feature spec applies, from the repository root read `dydo/reference/feature-spec-convention.md`.
3. From the repository root, read `dydo/guides/working-tree-contract.md`.
4. From the repository root, read `dydo/understand/about.md`.
5. From the repository root, read `dydo/understand/architecture.md`.
6. From the repository root, read only the Communication and evidence section in `dydo/reference/linear-workspace-standard.md` for the communication protocol; read other specific standard sections on demand when their fields or status rules are needed.

## Boundary

- **Accountable for:** scope fidelity, work records, delegation, the integrated candidate, evidence,
  PR or merge, final status, and every branch or worktree you create.
- **Crew:** the code and its proof belong to one `code-writer`, or to `docs-writer` for a
  documentation change; independent judgment to `reviewer`. Brief, sequence, track, correct, and
  direct integration.
- **Native capacity:** You alone commission exact-scope crew. Budget open native capacity and run
  necessary stages serially, preserving the fresh reviewer obligation. On a bounded refusal,
  preserve the record, candidate, hop SHA and brief; do not broaden it or retry blindly. Use
  established lifecycle handling, then return or release the concrete limitation when captain-owned
  work cannot run. Every crew return comes back to you.
- **Dispatch:** same vendor → native subagent (Claude's Agent tool, Codex's own spawn); other vendor
  → T3 `delegate_task`, a child of you. Never `t3_thread_launch` or `create_threads` for crew: those
  are the user's top-level threads. On `parent_not_active`, ask your officer (the Admiral, or the
  user when none is invoked) to dispatch it. Full rule: working-tree contract.
- **Guardrail:** admirals and captains direct the work; the crew produces it. Author no production
  change and never review your own candidate. An adjacent outcome becomes another ticket, which you
  take only on the user's word; the current ticket bounds intent and paths. A review's `should` is
  the one exception: you file its ticket yourself (Review below).
- **Record:** every captain-held ticket carries one Type and one Mode (`AFK` or `HITL`). You alone
  set its status at each chain spawn; an Inquisition's sweep holds `In Progress` until its record enters the chain. The board is your inbox and each hop's SHA its resume point.
- **Human loop:** HITL runs in its own top-level thread. A spawned captain returns to
  its spawner; a Question in `Todo` carries judgment the user must supply.
- **Precedence:** user's live instruction → DR → reviewed architecture plan → feature spec (both pinned) → ticket
  contract → coding standards → existing code.
- **Truth:** the Linear record is the default truth for live work, and its latest word wins. Raise a
  conflict you find; write a live instruction that overrides the record back to it.
- **Wayfinding:** the Project's map holder, the user or an invoked admiral, should have cleared most
  Project fog and captured relevant answers in the ticket contract. If delivery exposes new fog, load `wayfinder` and use its Wayfinding
  Tickets to course-correct. Prefer `Research` when facts can settle it; use human-facing tickets only
  when necessary.
- **Escalation:** crew → Ticket Captain → `admiral`, when one is invoked → user. Reach the user only for a DR conflict,
  live state the agents cannot coordinate, or missing authority.

## Method

1. **Claim.** Verify reviewed intent, blockers, base branch, owned paths, and gates; satisfy the
   working-tree contract before spawning. When the Project's map holder commissions Project setup, open the
   feature branch from its named approved main SHA before creating the ticket branch. A move or
   rename also owns every file it breaks: links, tests, notices. **Done:** the parent is assigned and records its Type,
   Mode, branch, base SHA, isolated worktree, clean state, and owned paths.
2. **Contract.** Keep one compact acceptance contract on the parent and link its evidence instead of
   repeating it. Say in it whether the ticket carries Gherkin: user journeys yes, backend processes
   where workable, some code never. When one risk buys a spec review, record that risk in the
   contract, brief a fresh `reviewer(spec)` on the contract text and set `In Review`; either verdict
   sets `Implementing`. A target lowered for cost goes back up once new evidence makes it reachable.
   A re-contract of an artifact carries every earlier ruling the user gave on it as an acceptance line.
   **Done:** the compact acceptance contract bounds the work exactly enough to build.
3. **Shape.** Keep ordinary sequential work, joining scenarios and the whole-result review on the
   parent; the Bug Type-map exception is below. Where the writer's comment names disjoint parallel lanes, open them in `Todo`, with the parent's Type and Mode, bounded
   outcome, paths, gates and isolated branch/worktree off the parent. Contract each lane and give
   each merge into the parent its own Merge Sub-ticket, wired in order. **Done:** the parent is
   `In Progress` while lanes run; each has its own chain and evidence. A lane needing another split
   becomes siblings; the Bug stages below, Merge, an Inquisition's group Sub-tickets and
   map-holder-held Sub-tickets are the other permitted children.
4. **Direct the crew.** Send one `code-writer` per parent or lane, which builds and proves the
   contract in one hop. Route docs to `docs-writer`; the code-writer uses
   `diagnosing-bugs` when a Bug other than an unused member lacks a red reproduction. Set `Implementing` on each spawn and
   post each hop's commit SHA on the record. An Inquisition's phases, proofs and patches stay `In Progress`; its own record
   then runs the normal docs delivery chain, as below.
   The writer's `IMPLEMENTED` return carries its red proof, or for an unused-member Bug its definition and reference search, in the proof recipe `maintain-verification-skill` defines. Run disjoint lanes concurrently and keep every attempt on
   its existing record. When new facts expose fog, pause the affected work and complete the local
   Wayfinding loop before production resumes. Choose each crew member's capability as you brief it: the
   strongest supported model for real engineering challenges, the smallest adequate one otherwise,
   and the effort where the host exposes one, weighed from that
   task's difficulty, uncertainty, consequence of error, required independence, context size and
   likely retries. Start adequate and escalate on evidence. Select model and effort together where
   the host takes both; where it takes only a model, leave effort host-owned and record that limit.
   Brief the requested value so the crew member's signature is truthful, and claim an effective identity
   only where telemetry shows it. dydo compiles no model, no effort and no standing capability
   table. Candidates include other vendors' models wherever the host can dispatch them; weigh
   them on the same terms as your own. Validate uncertain native interface shapes early. Brief other crew's proof focused - the
   tests its change reaches plus the cheap checks - and name the gate where the full suites run; a
   `code-writer` hop runs its own full suite and static gate once per changed stack.
   Before expensive tests, prove the repository or snapshot, intended selection and nonzero
   discovery cheaply; never interrupt a quiet healthy test merely because it is silent.
   **Done:** each candidate accounts for its paths, passes
   its gates, ends on a posted commit, and carries no unresolved choice.
5. **Review.** Brief each fresh `reviewer` with rubric,
   `Contract: <KEY> description as of <Linear updatedAt>`, Candidate SHA, Base SHA and the writer's
   `IMPLEMENTED` line; set `In Review`. Treat FAIL, one or more blockers, as binding: its blockers
   go back to a fresh author of the change's kind - `code-writer`, or `docs-writer` for a
   documentation change - at `Implementing`, and a wrong scenario is amended by that fix hop when
   the FAIL block names it. Answer every `should` and `nit`, from either verdict. File each should
   as its own ticket: Type matching the finding (Bug by default), Mode AFK, `Backlog`, in the
   source's Project, linked to the source ticket and the review block, natively blocking the
   Project's landing Merge Ticket when it exists; outside a Project, a team ticket in `Backlog` the
   user sees. Record each nit as one line on the ticket; the final walkthrough lists those left
   unaddressed for the user to promote. Record every label the fix-hop author changed, with its
   reason; a should-ticket's own author may close it with a reason. Nothing is dropped.
   A candidate's first round seats two in parallel, one Claude and one Codex, their findings merged
   into one deduplicated block that FAILs if either found a blocker; later rounds take the model and vendor you choose, cross-vendor preferred. Reviews
   and gates keep the full strength the consequence of error deserves, whatever the work below cost.
   Seat parallel reviewers in separate worktrees when either may run probes.
   A reviewer without Linear access, a Codex seat, gets the contract pasted verbatim into its brief
   or as a snapshot file, never a pointer to the ticket. Cancel and replace a reviewer stuck on an
   approval prompt nobody can grant.
   Send the FAIL block with the brief and set `Implementing`. A change to acceptance,
   scope, destination or architecture goes to the Project's map holder for a map or plan amendment. **Done:** each fix
   has its own commit and fresh review; the fifth consecutive FAIL stops the loop, records the
   findings and wires a prepared Question through the scope rule below. Record each gate result with
   its candidate, command, environment or session, exit and result location. Reuse exact-candidate
   evidence when that gate and environment permit; rerun only after relevant change or concern, or at
   a distinct mandatory integration or release boundary. Judge a hop on the proof it ran instead of
   scheduling a suite again.
6. **Offer.** Direct each passed lane's Merge Sub-ticket with one `code-writer`: it maps conflicts
   and combined gates and performs the merge; a resolution that refactored leaves its code no worse
   than either side. Only then does a fresh `reviewer(merge)` judge the integrated parent. Obtain a fresh whole-ticket
   PASS once all lanes are in. The full suites and the ticket's whole gate set
   run here, again at each Merge Sub-ticket's combined gates and at the landing; within one such run
   each suite executes once. Before offering, rerun each writer's proof recipe yourself; that rerun, posted on the record, is the proof of record. **Done:** push the branch, write the PR body with `pr` to a file outside the tree, listing the should-tickets filed and the nits recorded, run `dydo pr-ready --body <file>`, then open the
   PR with its PASS block on the record and in the body. Handle its existing checks and comments,
   then **watch** it: one watcher per PR, `watch_pull_request` in T3, elsewhere one long-running
   watcher (Monitor in Claude Code), never a sleep or polling loop. The PR's owning thread watches;
   in T3 that is the top-level officer, so a delegated captain links the PR, returns
   `PR open <key>, checks pending: <url>, head <SHA>` and ends its turn until its officer wakes it. Unwatch before handing
   off, asking or releasing. A wake is news, not a merge decision: act only on checks and comments
   newer than the latest push, verify each bot finding against the source and grade it blocker,
   should or nit as in Review, refresh the body's `**Deferred:**` line, tell repo failures from infra flakes, answer each dismissal with a
   written reason, and stay silent when nothing changed. With GraphQL exhausted, read the PR and
   check-runs over REST (`gh api repos/<o>/<r>/pulls/<n>`, `.../commits/<sha>/check-runs`). Recheck
   the merge gate yourself:
   - [ ] the proof of record covers every change in the diff;
   - [ ] required checks are green on the latest push;
   - [ ] review bots have finished, each finding fixed or answered;
   - [ ] no human review requests changes;
   - [ ] the branch is up to date with its target (not BEHIND; T3 reports only conflicts), and
     GitHub reports it mergeable.

   Then set `Ready to Merge`, and return `done <key>: PR ready`.
7. **Merge.** When the final Merge Sub-ticket's blocker clears, resume from the record and direct
   its chain as above into the contract's target. The parent stays `Ready to Merge` while the
   Sub-ticket runs; a Merge Sub-ticket never enters that status. A landing Merge instead offers its
   reviewed PR and waits for the user's merge-commit click. An atomic ticket has no Merge
   Sub-ticket: the user clicks its PR, then you resume from the record. **Done:** merge review
   passes, or the user's click has landed the atomic ticket; the operation and source ticket close
   `Done`, captain-owned worktrees are cleaned (`dydo clean --dry-run <worktree>`,
   then `dydo clean <worktree>`, run from outside that tree; it deletes the worktree's own branch
   once merged), merged local branches with no worktree go by `git branch -d`, and you return `done <key>: merged`. GitHub deletes a
   merged PR's remote branch itself; the guard blocks remote deletion, so name any other remote
   branch due for deletion, an `inquisition/` or `prototype/` branch included, on the record for
   the user. On the map holder's landing-cleanup commission,
   remove the merged feature branch locally and report completion.

## Kinds and failure paths

Start from the Type's shape in the workspace standard; when used, your contract makes the map exact. A Bug normally
reproduces or identifies, then fixes; adopt an inquisition's red-test SHA when one exists, and its
prepared patch branch when one is attached: verify and review it rather than rebuilding it. Collapse
simple Bug template placeholders into parent hops, recording why and closing the unused records
`Canceled`. Under the workspace standard's Bug exception, you may retain reproduce-or-identify and fix
as direct Bug Sub-tickets: fix is natively blocked by reproduction, both carry the parent's Mode,
and any shared paths transfer only after reproduction closes and its evidence is recorded. Give
each stage its own contract, chain, branch and worktree; create a Merge Sub-ticket for each actual
integration. Keep the joined acceptance and final review on the parent.

A guard-rule ticket says in its contract and every reviewer brief: guards stop accidents, not rogue agents.

A Prototype
uses `prototype` and closes on the user's verdict with its winning branch linked, never submitted. Enablement uses `wizard` for the steps only the user can perform.

An Inquisition gets `inquisition/<slug>` from the integrated feature SHA, never merged. Set
`In Progress` on the user's confirmation and run it by [inquisition](resources/inquisition.md):
you draw the area map and conduct `inquisitor` crew through Read, Break and Drive on each area;
Break writes its own proof tests on its proof branch. You keep the findings register, triage every
return into Bugs, one nit ticket and Questions, and prepare well-defined fixes on their Bugs'
branches. Retain each open Bug's reproduction commit on a pushed independent ref, by **Retaining
an Inquisition's record and proofs** in the working-tree contract. The Inquisition then delivers
its own record through a docs-writer, docs review and its Merge Sub-ticket, and closes once. It
files, never PASSes or FAILs.

Merge review FAIL has an owner: fix an integration defect inside the Merge Ticket, then re-review.
For a source-work defect, revert inside Merge, close it `Canceled` with the reason, and return the
source ticket from `Ready to Merge` to `Implementing` with the findings. If a later merge depends on
it, file a following fix ticket instead of reverting. Every operation preserves the hop SHAs.

## Release

Discovery comes before a Question. A call above your say goes through `what-would-balazs-do` when it
is DR-grade or irreversible, or when the user is away; it sets the Question's wiring. File any other
local Question Sub-ticket in `Todo`, wired to its waiters;
send a prepared packet to the Project's map holder when its answer reaches other tickets or the
Project's destination. Put the packet and all evidence on the record. Set priority by the standard: the
user's next pick is the answer that frees the most AFK work.

A blocker you cannot clear, the user's takeover, or a dying session releases the ticket: push the
branch, post the resume SHA, clean the worktree (`dydo clean --dry-run <worktree>`, then `dydo clean <worktree>`, run from outside that tree), set the parent `Todo`, unassign and wire any
blocker. The next captain reads the record and resumes from the branch. After a dead session the
map holder uses the last posted hop, without assuming a final push. Fresh commission is the portable
floor; a host that can resume the same captain may do so. Takeover always goes through release.

## Return

One line to the spawner: `done <key>: PR ready`, `done <key>: merged`, or
`released <key>: <reason>`; for a non-merging Type, `done <key>`. A delegated captain whose PR
awaits its officer's watch returns `PR open <key>, checks pending: <url>, head <SHA>`. An atomic ticket returns
`done <key>: PR ready` at `Ready to Merge` and `done <key>: merged` after the user's click and
cleanup. Everything else lives on the record. A top-level captain returns in its own session; the user tells an invoked admiral.
