---
type: decision
status: accepted
date: 2026-04-18
area: platform
---

# 017 — Stale-Free Semantics: Narrow Launcher-Alive Gate to Reservation Only

## Context

Adele's fix for the stale-dispatch double-claim bug adds a launcher-alive probe to
`AgentRegistry.IsEffectivelyFree` (`Services/AgentRegistry.cs:151-155`): a stale
`Dispatched`/`Queued` agent is treated as busy when a `{agent} --inbox` launcher
process still appears in the OS process list.

`IsEffectivelyFree` has two call-site families with different intuitions:

- **Reservation (strict):** `ReserveAgent` (line 131) and — transitively —
  `AgentSelector.TryReserveFromPool`. Any false positive here double-launches a
  terminal and strands the second Claude.
- **Display / human selection (permissive):** `GetFreeAgents`,
  `GetFreeAgentsForHuman`, and their consumers: `AgentListHandler`,
  `WhoamiCommand`, `GuardCommand.HandleClaimSessionStorage` (the `claim auto`
  path), `AgentRegistry.ClaimAuto` (`AgentRegistry.cs:373`), and the
  "claimable agents" hint in `HandleExistingSession` (line 267). A human running
  `dydo agent claim auto` after a failed dispatch wants the stale agent to
  appear claimable; `dydo agent list --free` is an informational query.

The two intuitions pull in opposite directions. The launcher probe is also noisy
in practice — inquisition `stale-dydo-processes.md` F2 confirms that worktree
teardown routinely leaves stray dydo-related processes alive (watchdogs, orphan
PowerShell `finally` blocks running `dydo worktree cleanup`), and the
substring-match probe (`"{agent} --inbox"`) can pick up a departing PowerShell
launcher whose Claude has long since exited. A strict gate applied to the
display/selection path would therefore hide genuinely-reclaimable agents from
humans whenever the probe false-positives.

Three existing tests assert the old permissive semantic:
`GetFreeAgents_IncludesStaleDispatched`, `GetFreeAgents_IncludesStaleQueuedAgent`,
and a dispatch-selection test in the same neighborhood.

## Decision

**Keep `IsEffectivelyFree` permissive. Introduce a narrower predicate for
reservation only.**

Concretely:

- `IsEffectivelyFree(AgentState state)` stays as it was *before* Adele's patch:
  `Free || (stale && (Dispatched || Queued))`. No launcher probe.
- Add `IsReservable(AgentState state)` — private — that composes
  `IsEffectivelyFree(state) && !(IsStaleDispatch(state) && IsLauncherAlive(state.Name))`.
- `ReserveAgent` calls `IsReservable`. Every other call site keeps using
  `IsEffectivelyFree`.

This is option (3) from Brian's brief — "both-permissive + a new explicit
`ReserveStrict`" — chosen over option (1) "two methods, two behaviors" on
naming/clarity grounds (both options behave identically at the call sites; the
named predicate makes the strict semantic self-documenting) and over option (2)
"both-strict" because of the display-hiding failure mode described above.

## Rationale

1. **The two user intuitions are fundamentally different, and coupling them
   under one predicate compromises both.** Reservation must never double-book;
   display must not conceal plausible recovery targets. Serving both from
   `IsEffectivelyFree` forces a choice that's wrong for one audience.

2. **The launcher probe has known noise sources.** F2's evidence — 15 stranded
   worktree directories on one dev machine, a per-worktree PowerShell `finally`
   block that can take seconds to run `dydo worktree cleanup`, and the substring
   nature of the match — means false positives are rare but real. False
   positives in the display path silently hide reclaim candidates from humans
   (no error, no feedback loop). False positives in the reservation path just
   cause the caller to try another agent or see "no free agents", which is
   self-correcting and observable. Biasing the probe's failure mode toward the
   reservation path is the safer trade.

3. **`ReserveAgent`'s candidate loop is already tolerant.**
   `AgentSelector.TryReserveFromPool` picks from `GetFreeAgents*` and then calls
   `ReserveAgent` on each candidate in turn. A permissive selector that surfaces
   a stale+launcher-alive agent is harmless: `ReserveAgent` rejects it and the
   loop moves on. The only visible effect is a possibly-misleading
   `dydo agent list --free` — which is exactly the informational-noise trade
   point (2) already argues for accepting.

4. **`ClaimAgent` (the manual `dydo agent claim Grace` path) doesn't go through
   `IsEffectivelyFree` at all** — it goes through `HandleExistingSession`, which
   already permits claiming over `Dispatched`/`Queued` freely. So this decision
   has no effect on the human-manual-claim path; it only clarifies which
   automated path gets which gate.

5. **Minimal test churn.** Under this decision, the three pre-existing
   `GetFreeAgents_*` tests keep passing unchanged; Adele's three new
   `ReserveAgent_*` tests (which drive the override) keep passing; and the
   `IsReservable` rename is a private-method refactor.

## Consequences

- `dydo agent list --free` may include a stale-dispatched agent whose launcher
  is still alive. This is deemed acceptable: the worst-case UX is a user sees a
  candidate, runs `dydo dispatch --to X`, and gets "not free (status:
  dispatched)" from `ReserveAgent`. That error is self-explanatory and
  recoverable.
- `AgentSelector.TryReserveFromPool` may iterate through candidates that
  `ReserveAgent` then rejects. This is already the loop's contract
  (`ReserveAgent` can fail for lock-contention reasons too) and costs only the
  process-list probe per reject — a cold path.
- `ReserveAgent`'s error message, when rejecting a stale+launcher-alive
  candidate, should surface the reason — otherwise a human debugging a dispatch
  that "should have worked" sees only "not free (status: dispatched)" and is
  confused by the conflicting signal with `agent list`. This is a small
  implementation follow-up, captured below.

## Follow-ups

- Adele (or whoever lands the stale-dispatch fix) should extend
  `ReserveAgent`'s error text on the stale+launcher-alive branch, e.g.
  `"Agent 'X' is not free (status: dispatched, launcher still active — try again or claim by name)."`
  This prevents the list/reserve mismatch from becoming a confusing user
  experience.
- Inquisition `stale-dydo-processes.md` issues #95-#98 (watchdog CWD / per-
  worktree duplicate watchdogs / liveness / orphan PID cleanup) are the real
  long-term mitigation for launcher-probe noise. No new issue needed here.
- If future telemetry shows `ReserveAgent` routinely selecting
  stale-but-unreservable candidates (i.e., `TryReserveFromPool` looping more
  than once on the stale branch), revisit: at that point it's worth making the
  selector call `IsReservable` directly for selection (keeping
  `GetFreeAgents*` permissive for display). Not worth preempting.
