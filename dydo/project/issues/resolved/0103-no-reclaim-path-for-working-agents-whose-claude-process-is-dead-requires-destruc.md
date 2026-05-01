---
id: 103
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-04-18
resolved-date: 2026-04-26
---

# No reclaim path for Working agents whose Claude process is dead — requires destructive agent clean --force

Resolved medium-severity workflow gap: a Working agent whose claude process had died had no non-destructive reclaim path — the only option was `agent clean --force`, which discarded their workspace. Fixed by adding a stale-working reclaim path in `HandleExistingSession` (gated on stale + dead PID), which the next `dydo agent claim` triggers automatically; per decision 018.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Non-destructive reclaim path implemented at Services/AgentRegistry.cs:308-317 (HandleExistingSession). Triggers when IsStaleWorking(state) (>StaleWorkingMinutes=5, :13/:178) and !IsSessionPidAlive(agentName) (:204-212). Next 'dydo agent claim <name>' reclaims, archiving old workspace. Per decision 018; landed 2026-04-20 (changelog/2026-04-20/stale-working-reclaim.md). Verified by Dexter.