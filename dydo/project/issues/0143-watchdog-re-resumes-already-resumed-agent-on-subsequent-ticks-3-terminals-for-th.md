---
id: 143
area: backend
type: issue
severity: critical
status: open
found-by: manual
date: 2026-05-01
---

# Watchdog re-resumes already-resumed agent on subsequent ticks: 3 terminals for the same agent

Open critical-severity bug: the watchdog's auto-resume path (decision 022) re-fires on subsequent ticks for an already-resumed agent, spawning duplicate terminals (observed up to three). The "is this agent already resumed" check needs to short-circuit the launch trigger or update the resume-attempts counter so the cap actually kicks in.

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

(Filled when resolved)