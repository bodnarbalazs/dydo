---
id: 181
area: backend
type: issue
severity: low
status: open
found-by: inquisition
date: 2026-05-08
---

# Saturate-vs-claim race: watchdog overwrites freshly-claimed agent attempts=cap

Watchdog's SaturateResumeAttempts can saturate ResumeAttempts to cap on a freshly-claimed agent because the per-agent claim lock is dropped between TryReadResumeContext / TryReadGaveUpContext and the Saturate call. Pre-existing in v1.4.6; PR3 widens the footprint by adding the gave_up tick-check call site. Microsecond window, low impact (only the next-crash auto-resume budget is lost; user keeps the active claim and can re-claim manually). Audit field resume_attempts_at_claim preserves the prior state for inquisitor analysis.

## Description

Surfaced by Dexter's pre-tag v1.4.7 audit (Finding #2) and confirmed by Emma's judge ruling.

## Race window

In Services/WatchdogService.cs:

- TryReadResumeContext acquires the per-agent .claim.lock at :570 and releases it at :596 (finally).
- TryReadGaveUpContext acquires/releases the same lock at :615/:636.
- Both return a context object describing the agent state at lock-release time.
- The watchdog then re-enters AgentRegistry.SaturateResumeAttempts (Services/AgentRegistry.cs:1694-1709) which acquires the lock fresh.

Between the lock-release inside TryRead*Context and the lock-acquire inside SaturateResumeAttempts, a competing dydo agent claim can run AgentRegistry.HandleExistingSession (:340-359) → ResetResumeBookkeeping (:211-219, attempts=0). The watchdog's stale Saturate then overwrites attempts=cap on the freshly-claimed agent.

Result: the claim succeeded but the next crash of this same session won't auto-resume.

## Pre-existing scope

Pre-PR3 the same race existed via the IsBadSessionFailFast → Saturate path (single call site at WatchdogService.cs:501-516).

PR3 (commit 036b88c) ADDS one more call site at WatchdogService.cs:484-491 (the gave_up tick-check), slightly widening the window over the prior baseline.

## Why low severity

- Microsecond-scale window between lock release and Saturate's own re-acquire.
- The user already has an active claim — the only loss is the resume budget for the next crash of this session.
- The user can re-claim manually at any time.
- The audit Claim event's resume_attempts_at_claim field captures the prior state for after-the-fact analysis.

## Suggested fix (v1.4.8)

Either:
1. Have TryRead*Context return the context AND keep the lock held, so Saturate runs under the same lock; OR
2. Add a guard in SaturateResumeAttempts that re-validates the precondition (e.g. status != freshly-claimed) inside the new lock before writing.

Option 2 is local to AgentRegistry and fits the existing per-agent lock model better.

## Tests

The race is microsecond-scale and would need an instrumented harness to reproduce live. Defer to inspection plus an explicit comment block on the lock-drop in WatchdogService.cs near the call sites.

## Pre-tag posture

Not a v1.4.7 blocker. Dexter's audit verdict GO is unaffected; this issue tracks a pre-existing latent bug that should be addressed in v1.4.8.

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)