---
title: run-sprint / inquisition reviewer + auditor stages have no model fallback on a bound-model outage
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: open
work-type: 
id: 214
type: issue
found-by: review
date: 2026-07-06
---

# run-sprint / inquisition reviewer + auditor stages have no model fallback on a bound-model outage

Surfaced repeatedly during the DR 033 docs-mirror sprints: the `run-sprint` **reviewer** and
**sprint-auditor** stages (and the `inquisition` workflow's equivalents) run as the compiled
`reviewer` / `sprint-auditor` agent types, whose model is bound by their declared tier (DR 028). When
that bound model is unavailable — e.g. the account hits the **Fable 5 monthly spend limit** — the
`agent()` call returns nothing and the stage **hard-fails**: the slice escalates with
`reason: "reviewer did not return a result"` and `auditVerdict: skipped`. The whole quality gate of
every sprint is down until a human raises the limit or rebinds the tier. See [[fable-limit-blocks-sprint-reviewer]].

## Description

**Mechanism.** The run-sprint script calls `agent(reviewPrompt, { agentType: 'reviewer', ... })` with
no `model` override, so it uses the tier-bound model. There is **no fallback**: an empty/error result
is treated identically to a genuine no-result and escalates. A model outage (spend limit, provider
error, capacity) therefore takes out the review/audit gate entirely.

**Impact.** Every sprint's QA gate is a single point of failure on one model's availability. Work
lands **unreviewed** (the code-writer stage still completes in-tree), forcing the Tier-1 owner to
supply the review out-of-band (e.g. `Agent(subagent_type: reviewer, model: sonnet)`), which is exactly
the manual workaround used across the DR 033 cycle.

**Fix approach:** deliberately **not** a runtime failover interceptor — see **Agreed design** below.
We do not reimplement model-failover that Anthropic will likely ship natively; instead we lean on the
model abstraction we already own plus a small operational swap command.

## Harness findings (why dydo must implement this itself)

Confirmed against Claude Code / Agent SDK docs (claude-code-guide, 2026-07-06):
- **Claude Code CLI has native fallback** — `fallbackModel` (settings.json array, ≤3 models) and
  `--fallback-model`. BUT it is **session-level**, and documented for model **overload/unavailability
  (529)**, retried transparently — *not* for hard **spend/usage limits**, which the API blocks by
  design with **no retry and no fallback** (our Fable case).
- **Agent SDK subagents take a single `model` only** — no fallback list per subagent. dydo's compiled
  `reviewer`/`sprint-auditor` agents and workflow `agent()` calls are exactly this path, so the CLI
  `fallbackModel` does not cover them.
- On a spend-limit mid-run with no text yet emitted, the subagent fails hard (`Agent terminated early
  due to an API error`) and the error surfaces to the caller. No auto-retry.

Also verified: `fallbackModel` is documented "overloaded or unavailable only" and is **not** documented
to propagate to subagents. So neither native mechanism recovers a spend-capped model inside a workflow
subagent — but we still will **not** build a runtime interceptor (Anthropic will likely ship native
spend-cap failover; a hot-path interceptor becomes dead weight the day they do).

## Agreed design (settled with Balazs, 2026-07-06)

A **declared fallback in the abstraction** + a **time-boxed operational model swap**. No runtime
error-catching; stays out of Anthropic's lane; disposable when they ship native failover.

1. **Declare the fallback in `Models/ModelsConfig.cs`** — an optional, vendor-agnostic fallback the
   tiers resolve to. A legitimate **second-line defense**, not throwaway: useful for any provider when
   the primary "isn't there or doesn't work."
2. **`dydo model cap <model> --until <T> [--fallback <m>]`** — writes a marker
   (`_system/.local/model-caps/<model>.json`), rebinds every tier currently pointing at `<model>` to the
   declared fallback, and **re-runs `dydo sync`** so the compiled agent files use the fallback. `<T>` is
   **user-specified** (these are weekly caps; the reset time is usually stated in the error). Parse
   generously; expected format **`[yyyy-]mm-dd hh:mm`** (year omittable).
3. **`dydo model uncap <model>`** — manual restore: restore the original binding, clear the marker,
   re-sync.
4. **Watchdog auto-restore** — the watchdog already reconciles derived state each tick; add: marker
   exists and `now > T` → restore binding, clear marker, re-sync. No new daemon.
5. **Scope is project-wide/generic** — one swap covers all tiers on that model (generic beats
   per-agent). Keep it simple.

Touches `ModelsConfig` (schema + fallback), a new `dydo model` command group, and the watchdog
reconcile — Brian's compiler/sync territory.

## Reproduction
1. Hit the Fable 5 monthly spend limit (or otherwise make the reviewer tier's model unavailable).
2. Run any `run-sprint`.
3. Observe the reviewer stage escalate with "reviewer did not return a result" and the audit skipped,
   despite the code-writer stage completing.

## Resolution
(Filled when resolved)
