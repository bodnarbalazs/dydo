---
area: general
type: decision
status: accepted
date: 2026-10-28
---

# 059 - Inquisition Rebuilt: Area Map, Three Phases, Rounds Until Clean

An inquisition maps the landed work into small areas, runs Read, Break and Drive on every area,
triages each finding by severity, and repeats from zero until a round finds no blocker and no
should. Ruled by Balazs on 2026-10-28 in the admiral's grilling and the HITL design rounds on
[DYD-400](https://linear.app/bodnar-balazs/issue/DYD-400).

## Context

The inquisition is dydo's last line before landing, and its job is to find what nothing so far has
found. Until now it was one pass of lens-based reading: read-only inquisitors swept parts or lenses
(correctness, coverage, security, dead code, doc drift, seams), a separate proof-only code-writer
wrote one test per code hypothesis, and the captain released the ticket so the map holder could
contract a separate record Feature before resuming the audit to verify it. Coverage came out
superficial on big landings, and nothing drove the app the way the user would. Balazs asked for a
rework from the ground up. This replaces the earlier "journey lens" version of the ticket.

Sizes measured during the design (2026-10-28): an inquisitor's Must-Reads total about 6k tokens,
and the areas of the dydo map polish landing (PR #129, merge `5ff4c20b`, about 11 areas) are 1 to
8k tokens of code each.

## Decision

### Doctrine (Balazs, nearly verbatim)

1. **A purpose-built area map.** Before anything else the inquisition maps the landed work into
   small areas of code, architecture and files. The map is separate from the feature list, and one
   file can be an area. Areas stay small on purpose: a big area gets superficial coverage.
2. **Three tracks per area, as a methodical checklist:**
   - **Break it:** form hypotheses about how the area fails, then write tests that confirm or
     refute them.
   - **Read it:** look for what is broken in ways neither tests nor clicking around would catch.
   - **Drive it:** exercise every feature and edge case through the running app, the project's
     verification skill and the API endpoints. Where the verification skill cannot do something a
     user can do, that is a *verification gap* finding, owned by `maintain-verification-skill`.
3. **Swarm for large landings, and it is meant to be costly.** At commission, the admiral asks the
   user which provider and agents to use and how big the swarm is. Codex concurrency follows the
   dispatch rule's cap ([DYD-397](https://linear.app/bodnar-balazs/issue/DYD-397), DR 057).
4. **Triage by severity** (blocker, should, nit, from
   [DR 058](./058-review-severities-blocker-should-nit.md)). Blockers and shoulds become tickets as
   DR 058 defines. **All nits go on one ticket** on the inquisition record; nits are approved as
   nits.
5. **Repeat from zero until clean.** After the fixes, the admiral offers a follow-up inquisition,
   and each one starts from zero (a fresh map, not a recheck of the old findings). The Project
   lands after an inquisition that finds no blocker and no should.

### Rulings

The tracks are called **phases** below.

1. **The map.** The captain draws the area map from the landing diff and the file layout, as a
   table on the Inquisition ticket, fresh every round and drawn without the old one. Areas are
   small; one file can be an area. A seam between areas is its own area. A phase that does not
   apply says why in one line.
2. **Prose areas.** A plain skill or doc area gets Read only; Break and Drive are a no-op there.
   A contradiction it finds is proved by quoting both passages. Break and Drive apply wherever the
   landing changed behaviour: a change to the dydo guard gets a Break phase.
3. **Phase order: Read → Break → Drive.** The phases run in that order within an area, and areas
   run in parallel up to the swarm width, as a pipeline rather than three waves. One agent per
   area per phase. Each phase runs discovery → work → document, as exact steps with
   done-conditions and a fixed return shape.
   - **Read** goes first and covers what tests cannot show: not doing what the ticket asked, a DR
     contradiction, bad coding practice, wrong or bad comments, coding-standard violations,
     security. It still lists the testable and drivable findings it sees and tags them for the
     later phases instead of dropping them.
   - **Break** does its own discovery by reading the code for how it fails, folds in the Read
     findings tagged testable, and confirms or refutes them with its own proof tests on a proof
     branch, touching test paths only.
   - **Drive** takes the app for a spin through each area's user journeys and edge cases, through
     the project's verification skill and API, so that it works as well as it reads, and records
     rerunnable steps. A verification gap is a should: the Project cannot land, but the gap may be
     picked up later.
4. **Verification skill first.** A verification skill is a precursor and a requirement for a
   healthy inquisition. When a project with a runnable surface has none, the admiral raises it at
   commission and the skill is created before the inquisition. A project with nothing to run has
   no verification skill by its nature, and Drive does not apply there.
5. **Crew are workers.** Nesting stays at two levels. One agent per area per phase: its queue is
   that area's work items, its hypotheses, journeys, routed findings and follow-ups. Tiny areas are
   merged into one area when the map is drawn, never packed into one dispatch. Inquisition crew run
   as their own sessions, dispatched through T3 `delegate_task` per DR 057, because they outlive the
   captain's turn. There are no per-phase quotas of hypotheses or journeys. The only budget rule:
   an inquisition crew agent past its context limit finishes its current item, documents, and
   hands the rest of its area back to its captain, who dispatches a fresh agent. The limit
   defaults to 110k tokens and is tunable at commission. It applies to inquisition crew only:
   officers and other agents compact well, while inquisition crew run long and autonomous, without
   the user's messages that trigger a session's compaction. The threshold is a tunable for today's
   models, not a law. No hook enforces it, because a hook cannot tell which agents the rule is
   for; the inquisitor's own resources give the command that reads the agent's context size on
   Claude Code and on Codex.
6. **The captain conducts and never hunts.**
   - **Findings register:** a file on the audit branch, pushed after every return.
   - **Routing:** a finding goes to the phase that can prove it, with a short follow-up agent if
     that phase already ran for the area.
   - **Duplicates:** the same bug seen in several places (in the code and proven with a test, then
     again while driving the app) is one finding with several sightings. Every brief carries the
     area's register entries.
   - **Triage on each return:** blockers and shoulds become Bug tickets; nits go on one nit ticket
     per round, linked to its inquisition, and need no proof; judgment calls become a Question for
     Balazs. Each inquisition gets its own tickets and findings.
   - **Size:** one captain covers about 10 areas. Above that, a parent captain cuts the map into
     group Sub-tickets, the admiral commissions one captain per group, and the parent captain runs
     the cross-group triage.
7. **Patch lane.** A proven, well-defined blocker or should gets its fix prepared mid-round on its
   own branch off the feature SHA, its red test turning green. Well-defined means one obvious fix,
   inside one area, small, touching no contract, DR or public interface. The Bug is filed with the
   patch attached and reviewed once, in its own chain; its captain uses the prepared fix, verifies
   and reviews it rather than starting from zero. Nits are never auto-patched. The inquisition
   merges nothing but its own record. A patch-lane Bug stays assigned to the inquisition's captain
   until its patch is attached, or its reason for none recorded, and is then unassigned; any other
   Bug is pickable once filed. Its fix may merge into the feature mid-round, since the audit is pinned to its SHA (the captain's provisional call,
   pending Balazs).
8. **The record.** The inquisition delivers its own record as its last step: a docs-writer writes
   it, a docs reviewer checks it, and it merges into the feature branch. Audit and proof branches
   never merge, and each open Bug's reproduction ref is retained as before.
9. **Commission and rounds.** The admiral's commission question picks a provider and model per
   phase and the swarm width. Codex starts about 8 agents at once, goes toward 16 while runs stay
   clean, and backs off on 429, per DR 057; the "at most 2 Codex agents" figure in the design was
   only the admiral's limit on one captain's own crew. The next round waits for every blocker and
   should fix. The Project lands after a round with no blocker and no should. Only Balazs defers a
   should, at the end of the Project; a should found outside any Project is still a should-level
   Bug.
10. **Severities** use DR 058's names exactly, replacing the inquisitor's `high`, `medium` and
    `low`. A nit is a noted nit-pick: it may or may not be correct, it carries no weight for
    immediate action, it may go away on its own while the Project changes, and it may be
    addressed in bulk.

### Illustration

Illustrative only, from the design's worked example, the map polish landing: finding F-07 sits at
`viewer/src/graph/graphDiff.ts:56`, which merges the Project's issues and the external issues into
one map, so moving an issue between them is invisible to the refresh diff. Read sees it and tags it
testable and drivable; Break proves it with a red test; Drive sees it again on the refreshed map.
It is one finding with two sightings, not two Bugs.

## Consequences

- An inquisition costs far more agents than before, by design; the user sets the price at
  commission through the models per phase and the swarm width.
- Every area is read, broken where it behaves and driven where it runs, so coverage no longer
  depends on which lenses a captain picked.
- A project with a runnable surface needs a verification skill before its first inquisition, and
  every verification gap becomes upkeep for `maintain-verification-skill`.
- The proof-only code-writer handoff disappears from the inquisition; code-writer keeps its general
  proof mode for other work.
- No release-and-reopen: the Inquisition runs its record through its own ordinary docs chain and
  closes once.
- Rounds repeat, each with its own map, Bugs and nit ticket, until one finds no blocker and no
  should.

## Supersedes

Partially supersedes [DR 047](./047-supersymmetry-hop-statuses-merge-issues-and-the-release-protocol.md)
§9, "The inquisition is an Issue": its read-only inquisitors sweeping parts and lenses, the
proof-only implementer whose only output is a test, and its one inquisition per landing offered
once the feature is integrated, with another only when the human confirms one after a walkthrough.
Partially supersedes [DR 045](./045-flow-map-hats-review-tiers-and-working-tree-contract.md)'s
Audit row and §3, "rare, fan-out across lenses", for the same question. Partially supersedes
[DR 058](./058-review-severities-blocker-should-nit.md) §1's "no Linear ticket" for a nit, for
inquisitions only: an inquisition's nits go on its round's nit ticket. Retires the separate
primary record-delivery Feature and its release-and-reopen loop, which DYD-90 added to the
working-tree contract without a Decision Record. The Inquisition stays a captain-held primary
Type, cut from the integrated feature SHA on a branch that never merges, filing outcomes rather
than a PASS or FAIL; all three DRs stay `accepted` otherwise.

## Related

- [058 - Review Severities](./058-review-severities-blocker-should-nit.md) - The blocker, should and nit names
- [Working-Tree Contract](../../guides/working-tree-contract.md) - Audit and proof branches, retention
- [Linear Workspace Standard](../../reference/linear-workspace-standard.md) - The Inquisition Type
