---
id: 129
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-04-28
resolved-date: 2026-04-30
---

# Watchdog has zero structured logging — kill class is undiagnosable

Resolved medium-severity diagnosability gap: the watchdog ran as a detached background process with no logging — kill loop errors were swallowed, the top-level loop ate all exceptions, and the only artifact of a kill was its side effects. Fixed in commits `3532bd9` + `4dd5d03` by adding a structured JSONL event log at `dydo/_system/.local/watchdog.log` (start/tick/kill/parse_failure/poll_error/exit events) with 2MB rotation × 3 backups and a never-throws contract.

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

Fixed by 3532bd9 + 4dd5d03 (structured JSONL event log at dydo/_system/.local/watchdog.log with start/tick/kill/parse_failure/poll_error/exit events; 2MB rotation × 3 backups; never-throws contract).