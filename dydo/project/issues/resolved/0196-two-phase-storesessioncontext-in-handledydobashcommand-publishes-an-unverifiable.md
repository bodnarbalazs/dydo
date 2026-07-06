---
title: Two-phase StoreSessionContext in HandleDydoBashCommand publishes an unverifiable single-line session id between phase 1 and phase 2
id: 196
area: backend
type: issue
severity: medium
status: resolved
found-by: inquisition
date: 2026-05-19
resolved-date: 2026-07-04
---

# Two-phase StoreSessionContext in HandleDydoBashCommand publishes an unverifiable single-line session id between phase 1 and phase 2

GuardCommand.HandleDydoBashCommand writes .session-context in legacy single-line format at line 630 (no agent name) then re-writes with the verified two-line format at line 672-673. AgentSessionManager.GetSessionContext returns the single-line value AS-IS without verification (line 167-168), so a concurrent reader racing into the window between phases bypasses verification and ResolveSessionFallback. Two concurrent dydo commands from different terminals interleave their phase-1 writes; combined with F5 (missing human filter) the race surface widens. Latent race the April-09 fix knowingly left in place.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Fixed at HEAD: the phase-1 single-line publish window was removed and the reader discards unverifiable single-line content (GuardCommand.cs:851 '#0196'; AgentSessionManager.cs:167-170). Triage sweep 2026-07-04 (Brian, CoS).