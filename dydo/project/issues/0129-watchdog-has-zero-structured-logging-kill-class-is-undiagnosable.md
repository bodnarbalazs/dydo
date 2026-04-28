---
id: 129
area: backend
type: issue
severity: medium
status: open
found-by: inquisition
date: 2026-04-28
---

# Watchdog has zero structured logging — kill class is undiagnosable

## Description

**Mechanism.** The watchdog runs as a detached background process with no structured log output. `PollAndCleanup` swallows kill-loop errors (Services/WatchdogService.cs:273); the top-level loop swallows all exceptions (Services/WatchdogService.cs:223-226) to keep itself alive. There is no record of:

- Which PIDs were killed (and what their command lines were)
- Which state snapshot was used as the kill decision
- Whether the anchor was resolved on startup, and to which PID
- ParseStateForWatchdog torn-read returns

The only visible artifact of a kill is the side effect (terminal closes, no Release event in the agent's audit). This made findings #1, #2, and #6 indirectly verifiable only — the inquisitor could not directly observe a kill in flight.

**Bare minimum.** Append-write to `dydo/_system/.local/watchdog.log` on each kill: timestamp, agent name, matched PIDs and their command lines, the state snapshot used, the anchor PID. Plus one-shot startup line (anchor PID, poll interval). Plus a line for each `PollAndCleanup` swallowed exception.

This is a diagnosability/maintainability bug, not a correctness bug — but it gates regression detection for the entire watchdog. Without it, the next class of mid-work-death bug will also take 200+ audit-event sessions of forensic work to surface.

**Found by inquisition:** dydo/project/inquisitions/agent-deaths.md (Finding #6 — missing logging).

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)