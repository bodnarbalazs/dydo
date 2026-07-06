---
title: Nudge patterns match content inside command arguments causing false positives
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: resolved
work-type: 
id: 59
type: issue
found-by: inquisition
date: 2026-04-09
resolved-date: 2026-04-10
---

# Nudge patterns match content inside command arguments causing false positives
Resolved high-severity correctness bug: nudge patterns matched content inside `dydo` command arguments (e.g., quoted briefs that contained the trigger phrase), producing false-positive nudges. Fixed in commit `e97ebf1` by reordering guard evaluation so `dydo` commands skip nudges entirely.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in commit e97ebf1: Guard evaluation reordered so dydo commands skip nudges, preventing false positives from argument content