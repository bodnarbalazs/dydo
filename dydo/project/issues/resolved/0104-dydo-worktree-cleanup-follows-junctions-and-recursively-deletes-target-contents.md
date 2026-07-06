---
title: dydo worktree cleanup follows junctions and recursively deletes target contents in main repo
area: platform
fix-release: 
needs-human: false
resolution: 
severity: critical
status: resolved
work-type: 
id: 104
type: issue
found-by: manual
date: 2026-04-20
resolved-date: 2026-04-20
---

# dydo worktree cleanup follows junctions and recursively deletes target contents in main repo
Resolved critical-severity destructive bug: `dydo worktree cleanup` followed the junction reparse points back into the main repo and recursively deleted their target contents, destroying live data in `dydo/agents`, `_system/roles`, and other junctioned paths. Fixed by routing `TeardownWorktree` through `DeleteDirectoryJunctionSafe` (which unlinks reparse points via `Directory.Delete(recursive:false)` rather than `cmd rmdir`); covered by `Cleanup_WithJunctionToMainAgents_DoesNotDeleteMainAgents` and a sibling test.
## Description
(Describe the issue)
## Reproduction
(Steps to reproduce, if applicable)
## Resolution
TeardownWorktree now routes through DeleteDirectoryJunctionSafe before git worktree remove, and DeleteDirectoryJunctionSafe unlinks reparse points via Directory.Delete(recursive:false) instead of cmd rmdir. Junction-follow destruction reproduced by Cleanup_WithJunctionToMainAgents_DoesNotDeleteMainAgents and Cleanup_WithUnknownJunction_DoesNotDeleteJunctionTarget.