---
title: RemoveZombieDirectory uses junction-unsafe Directory.Delete
area: backend
fix-release: 
needs-human: false
resolution: 
severity: high
status: resolved
work-type: 
id: 84
type: issue
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-10
---

# RemoveZombieDirectory uses junction-unsafe Directory.Delete
Resolved high-severity destructive bug: `RemoveZombieDirectory` called `Directory.Delete(recursive: true)` against paths that could contain Windows junction reparse points, causing the delete to follow into the target and destroy unrelated files. Fixed in commit `b799726` by replacing it with `DeleteDirectoryJunctionSafe`, which detects junctions via the ReparsePoint attribute and unlinks them without recursion.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
Fixed in commit b799726: RemoveZombieDirectory replaced with DeleteDirectoryJunctionSafe that detects junctions via ReparsePoint