---
title: Watchdog re-resumes already-resumed agent on subsequent ticks: 3 terminals for the same agent
area: backend
fix-release: 
needs-human: false
resolution: 
severity: critical
status: resolved
work-type: 
id: 143
type: issue
found-by: manual
date: 2026-05-01
resolved-date: 2026-05-01
---

# Watchdog re-resumes already-resumed agent on subsequent ticks: 3 terminals for the same agent
Open critical-severity bug: the watchdog's auto-resume path (decision 022) re-fires on subsequent ticks for an already-resumed agent, spawning duplicate terminals (observed up to three). The "is this agent already resumed" check needs to short-circuit the launch trigger or update the resume-attempts counter so the cap actually kicks in.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed by 397011f (Charlie): RefreshClaimedPid in HandleExistingSession's same-SessionId short-circuit. Resumed claude updates ClaimedPid on first hook fire; preserves Decision 022 identity properties (no archive, no role reset, no workspace wipe) — only PID is refreshed. Eliminates the multi-spawn chaos.