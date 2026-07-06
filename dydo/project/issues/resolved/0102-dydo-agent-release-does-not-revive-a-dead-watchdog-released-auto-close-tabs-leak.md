---
title: dydo agent release does not revive a dead watchdog — released auto-close tabs leak until next dispatch
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 102
type: issue
found-by: manual
date: 2026-04-18
resolved-date: 2026-04-21
---

# dydo agent release does not revive a dead watchdog — released auto-close tabs leak until next dispatch
Resolved medium-severity correctness bug: if the watchdog had died, a released `--auto-close` agent's terminal leaked because nothing revived the watchdog until the next dispatch. Fixed in commit `0f0e31a` by having `dydo agent release` re-run `WatchdogService.EnsureRunning` after clearing the agent.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
dydo agent release now re-runs WatchdogService.EnsureRunning after clearing the agent, so a released agent with --auto-close no longer leaks its terminal when the prior watchdog has died. Fix landed in commit 0f0e31a (Revive watchdog during agent release).