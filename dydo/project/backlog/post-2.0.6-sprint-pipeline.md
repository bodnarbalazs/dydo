---
area: project
type: backlog
name: post-2.0.6-sprint-pipeline
status: open
created: 2026-07-08
created-by: Adele
origin: balazs — "organize the backlogs' tasks and fold them into sprints to be implemented (and throw out the ones which are irrelevant)... high level thinking of how many sprints does it fit into, what order"
---

# Post-2.0.6 Sprint Pipeline (chief-of-staff triage, 2026-07-08)

High-level portfolio only. Per [DR 039](../decisions/039-planner-role-review-target-subskills-and-the-plan-gate.md),
each sprint gets its own planner → plan-review gate at execution time; nothing below is a plan.

## Gate 0 — in flight now (pre-tag)

- **0250 impersonation fix** (Noah) — v2.0.6 tag HOLDS for this.
- **DR-035 docs-body sprint** (Charlie) — lands independently of the tag.
- Then: **tag v2.0.6 → live tests** (reset dry-run → throwaway → real; codex dispatch smoke;
  resume/session-id ground truth for 0233).

## The sprint queue (proposed order)

1. **Sprint C1 — Codex adoption batch** *(small, first after the smoke — produces the DR-037
   step-4 measurement data that calibrates everything later)*: issue 0239 (vendor-override
   fail-fast), 0240+0237 (dispatch role validation), exact-model-provenance-display, codex guard
   adapter (payload shapes per Noah's probe findings), 0233 e2e regression tests. First measured
   Codex-worker sprint.
2. **Sprint M0 — spine object-type completion** *(NEW, from the 2026-07-09 live docs smoke;
   Brian owns sync-model — balazs routing)*: the docs mirror correctly excludes what sync-model
   declares as DBs, and sync-model only has 7 types — so ~700 of ~831 mirrored pages are
   PM-shaped records (decisions, changelog, backlog, pitfalls) still living as doc pages.
   Complete DR-034's type work: Decision + changelog (own record types), backlog +
   future-features as status/horizon PROPERTIES per balazs, plus the live-model regen story
   (issue 0252). Shrinks the docs mirror to ~40 true docs and gives PM records their queryable
   DB home. MUST reconcile with Olivia's M1 plan (S2b backlog partition, changelog-done-rows
   deferral) — one planner round covers both.
3. **Sprint M1 — DR-034 taxonomy migration** *(the load-bearing one; Olivia's plan + dry-run
   exist — needs plan-review + M0 reconciliation)*: S2a CLI vocab/archive
   code (+ junction dead-code investigation) → S2b–S5 sequential in-branch (backlog partition,
   future-features, inquisitions yank, doc/link sweep) → S6 live smoke. Kills most of the 0249
   validator debt; absorbs the backlog-record dispositions listed below.
3. **Sprint A1 — DR-036 approval reform R1–R3** *(depends on M1's S2a vocab)*: `dydo task done`
   / `dydo task archive` + guards, approve/reject removal, acceptance checklist into dispatcher
   skills. Well-specified — Codex candidate.
4. **Sprint P1 — DR-039 planner + subskills R1–R4** *(R1 role files are human-gated —
   `_system/roles/` is off-limits to agents)*: planner role, reviewer skill-folder restructure
   (the merge/sprint target reference **absorbs the DR-031 sprint-auditor charter rewrite** from
   review-tiers-and-attention), run-sprint green-lit-rows contract (subsumes
   notion-board-followups §A's runtime→board bridge in part), docs per the new-role surface set.
5. **Sprint S1 — portable skill definitions** *(feature; DR-039 R2's targets migrate onto it as
   dogfood)*: vendor-neutral skill definitions compiled by `dydo sync`; design round first.
6. **Sprint H1 — debt & hygiene sweep** *(mechanical batch, strong Codex candidate)*: audit-cruft
   purge (~1644 tracked files under `_system/audit/`), 0248 dir-scoped `dydo fix`, 0238 model
   cap status, 0242/0243/0244 small ergonomics, flaky-test cluster (0119/0120/0135/0136/0137/
   0165), 0249 residue post-M1.

## Parallel stress-test posture (balazs, 2026-07-08)

His intent, verbatim in substance: once 2.0.6 lands and holds, **stress-test — run as many of
these in parallel as possible**; use the DR-039 planning paradigm (plan → plan-review →
green-lit rows), then "unleash codex to do the grunt work fast. No hours-long sprints."
Consequences for how the plans get written:

- **Parallelism is bought at plan time**: every plan must declare its full file footprint, and
  the plan-review gate checks CROSS-SPRINT disjointness against the other in-flight sprints'
  footprints, not just internal slice disjointness.
- **Parallel-safe pairs today**: C1 ∥ H1 (disjoint code + mechanical debt), C1 ∥ M1's doc slices.
  **Collision pairs**: C1's dispatch-validation files vs M1-S2a's command handlers (both under
  Commands/) — resolvable by slicing, must be checked at plan review. A1 stays hard-serial
  behind M1-S2a (vocab dependency).
- **The junction investigation (M1-S2a) is the parallelism multiplier**: if the junction
  machinery is confirmed dead and removed, PM-dir moves stop being in-branch-only and M1's
  S2b–S5 open up. Prioritize that investigation INSIDE S2a's first hours.
- **The global test gate stays the serial choke point**: parallel implementation, SEQUENCED
  landings through the chief-of-staff (one merge-back at a time, full gates per landing). Wall
  clock parallelizes; landing order does not.
- **Hours-long sprints are a smell**: slices sized to one review round (DR-039 rubric), Codex
  dispatch for implementation per the DR-037 addendum, workflows reserved for background QA.
  If a sprint forecast exceeds ~1h wall-clock, split it or parallelize it at plan review.

## Campaigns (design round with balazs first, not yet sprints)

- **Agent board & cockpit** *(post-M1; wants the Task DB)*: live Agent DB view, reverse
  messaging (adopting the Notion 3.6 assignment UX per balazs's ruling), the remaining
  runtime→board bridge pieces (needs-human detectors, gate-result writer), `dydo ask`
  answer-from-Notion loop, chief-of-staff watchdog materialization, idea-funnel capture
  surfaces. Sources: notion-agent-board-and-reverse-messaging, notion-board-followups §A,
  dydo-2-vision-followups, notion-external-agents-integration (ruling).
- **QA & attention** *(post-P1)*: attention-ledger build steps 1–3 (DR 032), legacy-sweep
  inquisition (dydo-2-hardening), Notion re-provision/model-evolution robustness live-smoke
  (notion-board-followups §B — pairs naturally with post-reset live testing).
- **Notion onboarding** *(when open-sourcing pressure returns)*: `dydo notion connect/setup` +
  project-scoped config, agent-first install doc.

## Scheduled decisions & gated experiments

- **0236 spine → native markdown** — scheduled decision with Brian after DR-035's live smoke
  proves the endpoints.
- **Codex-MCP delegation experiment** — gated behind C1's measured data
  (codex-mcp-delegation-experiment.md; Noah's probe findings incorporated).

## Backlog-record dispositions (execute mostly via M1's S2b)

- **Archive as done:** notion-reset-command (shipped 6d98588); notion-docs-nested-pages (when
  Charlie lands); task-approve-workflow-rethink (resolved → DR 036, keep as history).
- **Delete as superseded:** pm-record-folder-taxonomy (→ DR 034); the "docs-tree → Notion body
  sync" and "codex paving" sections of dydo-2-vision-followups (shipped as DR 033/035 and the
  codex-first-class campaign respectively).
- **Fold:** dydo-2-campaign-roadmap → dydo-2.0 Campaign record; review-tiers-and-attention's
  auditor rewrite → P1; its attention-ledger half → QA campaign; dydo-2-vision-followups
  survivors (dydo ask, idea funnel, chief watchdog, install doc) → Agent-board & onboarding
  campaigns; cross-vendor-agent-integration items → C1 + the MCP experiment record.
- **Keep as-is:** auto-memory-policy (TemplateGenerator line → H1 or C1; CoS sweep methodology →
  P1's skill surfaces), portable-skill-definitions (S1), exact-model-provenance-display (C1),
  codex-mcp-delegation-experiment (gated), post-2.0.6-sprint-pipeline (this file — retire when
  the queue empties).
