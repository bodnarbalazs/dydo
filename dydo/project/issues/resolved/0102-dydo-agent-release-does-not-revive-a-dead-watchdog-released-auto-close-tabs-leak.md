---
id: 102
area: backend
type: issue
severity: medium
status: resolved
found-by: manual
date: 2026-04-18
resolved-date: 2026-04-21
---

# dydo agent release does not revive a dead watchdog — released auto-close tabs leak until next dispatch

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

dydo agent release now re-runs WatchdogService.EnsureRunning after clearing the agent, so a released agent with --auto-close no longer leaks its terminal when the prior watchdog has died. Fix landed in commit 0f0e31a (Revive watchdog during agent release).