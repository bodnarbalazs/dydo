---
id: 127
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-04-28
---

# Watchdog anchor pinned to first dispatcher's claude only

## Description

**Mechanism.** `WatchdogService.EnsureRunning` (Services/WatchdogService.cs:72-79) short-circuits when the PID file exists and the existing watchdog PID is alive. The anchor is resolved once at first start (line 120) and stored in the watchdog's environment. Only the FIRST dispatcher's resolved `claude` ancestor is the anchor for the lifetime of the watchdog.

If that claude session exits while later-dispatched agents are still mid-work, the watchdog dies (`Run()` line 215 break) and their auto-close cleanups never run. The next dispatch then spawns a new watchdog with a different anchor — non-deterministic lifecycle tied to whichever agent dispatched first.

**Impact.** Multi-orchestrator setups (e.g. user has two `claude` tabs running simultaneously, or a long-lived oversight agent dispatches agents whose work continues past the orchestrator's exit) lose watchdog coverage at unpredictable points.

**Suggested fix.** Either (a) periodically refresh the anchor list (track all live claude ancestors, exit when none remain), or (b) anchor on the dispatcher's process group rather than a single PID, or (c) use a max-age timeout instead of a single-PID anchor.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #6 — anchor pinned to first claude).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)