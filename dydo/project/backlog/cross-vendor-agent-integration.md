---
area: general
type: context
status: open
created: 2026-07-07
created-by: Adele
origin: balazs — heuristic for same-vendor dispatch with a cross-vendor reroute on model-limit, and how to integrate Codex agents into the flock cooperatively.
related-issues: [0214, 0239]
related-decisions: [024, 028, 037]
---

# Cross-vendor agent integration (Claude + Codex in one flock)

FutureFeature-class. Keep for later; develop via a co-thinker → decision record before any build.

> **2026-07-08 — co-thinker round concluded: [DR 037](../decisions/037-cross-vendor-dispatch-same-vendor-default.md) (accepted).**
> Ruling: no new abstraction — same-vendor dispatch by default, cross-vendor as an explicit dispatch-time
> override; vendor binds only at the dispatch boundary (roles vendor-free, workflows vendor-homogeneous);
> sprints may mix vendors across slices, workflows never do. Hardening: [[0239]] (friendly error on
> unavailable vendor override). Still open here: the Codex guard adapter (Codex has pre/post tool-use
> hooks, GA — install `dydo guard` there at init/sync), and the reroute-on-limit heuristic below, which
> stays deferred until manual overrides prove routine.

## balazs's heuristic (to preserve)

1. **Same-vendor by default.** An agent dispatches within its own vendor — Claude dispatches Claude-model
   agents, GPT/Codex dispatches GPT-model agents. Each agent stays on the toolchain/models it knows.
2. **Clever exception — cross-vendor reroute on limit.** When the *stronger* model is capped somewhere,
   reroute the task to the other vendor rather than stall.

## The caveat balazs caught (must be assessed)

The reroute **cannot happen inside a running Claude Code dynamic workflow.** run-sprint / inquisition
bind their reviewer/auditor stages to **Fable 5** (tier, DR 028); you can't substitute gpt-5.6 (Codex)
into a Claude workflow's subagent. So a naive "swap the model when Fable is capped" fails at the
workflow-internal layer.

**Resolution direction:** operate the cross-vendor reroute at the **task/dispatch boundary**, not by
model-substitution inside a live workflow. I.e. when Claude's tier is exhausted, route the *whole job*
to a Codex agent, which runs its *own* vendor-native workflow with GPT models. For the *workflow-internal*
case, keep the **same-vendor tier fallback** (Fable → Sonnet) — that's exactly issue [[0214]]'s Option A.
So: within a vendor, tier-fallback; across vendors, task-level reroute. Two different levers at two levels.

## Seed ideas for seamless integration (cooperate, don't compete)

- **The seam is dydo itself.** dydo is a vendor-neutral **coordination** layer (identity, claim,
  reservation, inbox, `dydo wait`, task lifecycle, the board). Execution is vendor-specific. If Codex
  agents are first-class dydo citizens — claim/reserve/message through the same mechanism — they can't
  double-grab work (the reservation model already prevents two agents taking one task). Foundation is
  already forming: `f444ae2` compiled `.codex/agents/*.toml` + `.agents/skills/` from the **same role
  definitions** that `dydo sync` compiles Claude skills from — one role model, two vendor bodies.
- **Cooperation patterns worth designing:**
  - *Cross-vendor review* — a Claude reviewer **and** a Codex reviewer on high-stakes changes catch each
    other's vendor-specific blind spots (diverse-perspective verify, cross-family).
  - *Load/limit reroute* — new implementation tasks route to Codex when Claude's strong tier is capped
    (task-boundary, per above).
  - *Vendor affinity* — Codex-specific integration work → Codex agents; Claude-specific → Claude. Route
    by the nature of the work, not just availability.
- **Anti-competition invariant:** one shared board + one reservation system + one message bus; different
  engines. Don't run two parallel rosters — one roster, vendor-tagged (the new `Host` column in
  `dydo agent list` may already be the hook).

## Open questions to assess (co-thinker → DR)

- How does a Codex agent onboard into dydo identity/claim/wait? Does it honor the guard hooks?
- Detection that drives the reroute — ties to [[0214]] (a capped Fable surfaces as an API-error null /
  OTel `api_error`). Same signal can trip a cross-vendor reroute, not just a same-vendor fallback.
- Task-boundary reroute mechanics: does the dispatcher pick vendor at dispatch time, and on what signal?
- Cost/quality parity: when is a Codex substitution acceptable vs. worth waiting for the Claude tier?
