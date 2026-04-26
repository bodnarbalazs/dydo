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

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Non-destructive reclaim path implemented at Services/AgentRegistry.cs:308-317 (HandleExistingSession). Triggers when IsStaleWorking(state) (>StaleWorkingMinutes=5, :13/:178) and !IsSessionPidAlive(agentName) (:204-212). Next 'dydo agent claim <name>' reclaims, archiving old workspace. Per decision 018; landed 2026-04-20 (changelog/2026-04-20/stale-working-reclaim.md). Verified by Dexter.