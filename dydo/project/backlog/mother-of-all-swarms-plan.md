---
area: project
type: context
name: mother-of-all-swarms-plan
status: open
created: 2026-07-11
created-by: Adele
---

# Mother of All Swarms — launch-ready plan

The plan to crush the open backlog (78 open issues: 15 high / 28 medium / 35 low), the green-lit
sprints, and the campaigns using the codex-workhorse + claude-gate model proven across v2.0.7/2.0.8.
Written so launch is "say go." Supersedes the sequencing half of
[post-2.0.6-sprint-pipeline](./post-2.0.6-sprint-pipeline.md) (that record keeps the campaign/DR map).

## Readiness gates (ALL must be true before the swarm launches)

1. **Codex enablement landed + installed** (the 2.0.9 wave): 0277-r2 (model + injection validation),
   0281 (hook-trust no-thrash), 0282 (python-spawn sandbox friction). Then a HEAD/2.0.9 binary
   installed so every dispatched codex runs Terra, no recurring trust prompt, no per-test escalation.
2. **One clean codex acceptance re-smoke** on the installed binary: Terra model confirmed, guard
   fires + off-limits blocked, no trust re-prompt on a SECOND dispatch, gap_check runs without
   escalation. This is the go/no-go.
3. **Triage pass complete** (swarm Wave 0 below) — the 78 issues deduped/staleness-checked and
   clustered into file-disjoint batches. Do NOT fire codex at raw pre-2.0 issues; many are moot.

## Friction-management doctrine (learned in the enablement phase — bake into every dispatch)

- **Codex = implement (fast rail, Terra). Claude = plan + review (strong rail). Adele = sequence
  landings.** Every codex fix is Claude-reviewed before it lands (the review has caught real bugs
  every time — a shell-injection blocker on 0277, security-path reorders — do NOT skip it).
- **Self-release + task-boundary briefs** (0279 workaround): codex briefs say "report, then release
  yourself." Coordination happens at DISPATCH time (self-contained brief), not mid-task. Their
  uncommitted work persists in the tree; Adele reviews + sequences.
- **File-disjointness is the parallelism unit.** Two codex agents may run in parallel ONLY if their
  briefs touch disjoint files. The triage pass assigns each batch a disjoint file footprint. Adele
  holds the map and refuses overlapping concurrent dispatches (the DispatchPreflight.cs collision
  between 0281 and 0277-r2 is the cautionary tale — sequence same-file work).
- **Sequenced landings through Adele.** Codex holds commits; Adele stages EXPLICIT paths per fix and
  commits one at a time (never git add -A; the shared main tree carries multiple agents' uncommitted
  work at once). Verify-the-landing: `git show HEAD:<file>` after each (0266 — single-slice runs
  don't self-commit).
- **Trust-click reality:** each codex dispatch triggers ONE human trust-click on launch until 0281 is
  installed + one clean trust. Batch dispatches so the human clicks a run of prompts once, not
  scattered. Post-0281 this should vanish.
- **Gates:** `python DynaDocs.Tests/coverage/run_tests.py` (+ `gap_check.py --force-run` when Sync/
  or unwatched dirs change — 0217) green per fix before landing.

## Wave structure

### Wave 0 — TRIAGE (first swarm activity; claude/inquisitor, read-only, parallel by area)
Sweep the 78 open issues. For each: is it (a) already fixed/moot post-2.0.8, (b) a live self-contained
bug fix, (c) sprint/campaign-scale, or (d) a duplicate. Output: a batched worklist — clusters of
file-disjoint (b)-issues sized for one codex fix each, high-severity first; a "close as stale" list
for Adele to resolve; and the (c) items routed to their sprint/campaign. Dedup against the
enablement issues (0269-0282) already resolved. Deliver the worklist to Adele.

### Wave 1 — PARALLEL CODEX BUG-FIX BATCHES (the bulk; the measured workhorse run)
From Wave 0's disjoint batches, dispatch parallel codex code-writers (Terra), high-severity first.
Per issue: codex implements + tests + gates → Claude reviewer → Adele sequences the landing.
Cap concurrency at the number of trust-clicks the human will tolerate per batch (or unlimited once
0281 lands). This IS the DR-037 measurement — record rounds-per-fix + wall-clock vs the claude baseline.
High-severity clusters to expect (confirm in triage): identity/claim lifecycle (0110/0199/0198/0211),
guard/off-limits (0155/0192), auto-resume (0150), docs-mirror hardening (0220/0221 — coordinate with
the docs track), gap_check staleness (0217).

### Wave 2 — SPRINTS (planner → plan-review → codex implementation)
- **M0 spine-types-completion** — GREEN-LIT (DR-039 gate passed). Launch: codex implements its 6
  slices per the sprint record, Claude reviews, Adele sequences. Includes the FutureFeature title +
  status-color fixes (0278) and 0252 model-update command.
- **M1 DR-034 migration** — Olivia's plan exists; gated on M0. Re-run its plan-review (DR-039), then
  codex-implement the slices.
- **DR-036 R1-R3** (approval reform) and **DR-039 R1-R4** (planner role + reviewer subskills) — each
  needs a planner pass then codex implementation; DR-039 R1 role files need a human/guard-sanctioned
  path for _system.
- **0266-systemic** run-sprint/orchestrator skill patch (verify-the-landing doctrine) — balazs
  flagged as a required system fix.

### Wave 3 — CAMPAIGNS (design round with balazs first, then sprints)
Agent board & cockpit (notion-agent-board + reverse-messaging, adopting the Notion 3.6 assignment
pattern), portable-skill-definitions, the docs-mirror convergence (waits on M0), the codex-MCP
delegation experiment + Mia's app-server message-delivery design (0279) if we pursue full codex
coordination, review-tiers-and-attention.

## The orchestration loop (Adele, per wave)
dispatch batch (disjoint) → collect reports (self-released) → Claude-review each → sequence landings
(explicit paths, verify HEAD) → gate green → next batch. Hold same-file work serial. Record the
speed measurement. Escalate to balazs only on: a review FAIL needing a design call, a cross-cutting
decision, or a destructive/irreversible step.

## Board hygiene owed before/during (Adele, autonomous)
Reclaim stranded agents (Kate + any dispatched-state leftovers); resolve the stale legacy sprint
records (notion-sync/runtime-slim show non-terminal status but are dydo-2-0-era done); the enablement
follow-ups (0280 empty-target, 0282 python-spawn) fold into Wave 1.
