---
id: 104
area: platform
type: issue
severity: critical
status: resolved
found-by: manual
date: 2026-04-20
resolved-date: 2026-04-20
---

# dydo worktree cleanup follows junctions and recursively deletes target contents in main repo

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

TeardownWorktree now routes through DeleteDirectoryJunctionSafe before git worktree remove, and DeleteDirectoryJunctionSafe unlinks reparse points via Directory.Delete(recursive:false) instead of cmd rmdir. Junction-follow destruction reproduced by Cleanup_WithJunctionToMainAgents_DoesNotDeleteMainAgents and Cleanup_WithUnknownJunction_DoesNotDeleteJunctionTarget.