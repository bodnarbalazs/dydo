---
title: CollectLeafDirectories misidentifies worktree roots for non-empty orphaned directories
area: backend
fix-release: 
needs-human: false
resolution: 
severity: low
status: resolved
work-type: 
id: 91
type: issue
found-by: inquisition
date: 2026-04-10
resolved-date: 2026-04-26
---

# CollectLeafDirectories misidentifies worktree roots for non-empty orphaned directories
Resolved low-severity correctness bug: `CollectLeafDirectories` recursed into subdirectories of orphaned worktrees and treated their leaves as separate roots, which prune then walked into. Fixed in commit `def1fa4` by removing `CollectLeafDirectories` and switching `ExecutePrune` to enumerate top-level worktree dirs only; coupled with resolved issue #0105.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
CollectLeafDirectories function removed entirely. ExecutePrune (Commands/WorktreeCommand.cs:934-966) now enumerates top-level dirs only via Directory.GetDirectories(worktreesDir). Fix commit def1fa4; also referenced in resolved issue #0105. Verified by Dexter.