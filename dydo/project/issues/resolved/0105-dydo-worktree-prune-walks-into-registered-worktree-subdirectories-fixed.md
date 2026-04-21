---
id: 105
area: platform
type: issue
severity: low
status: resolved
found-by: manual
date: 2026-04-20
resolved-date: 2026-04-21
---

# dydo worktree prune walks into registered worktree subdirectories (fixed)

## Description

(Describe the issue)

## Reproduction

(Steps to reproduce, if applicable)

## Resolution

Duplicate of the fix landed in def1fa4: WorktreeCommand.ExecutePrune now enumerates top-level dirs only (replaced CollectLeafDirectories recursion), so prune no longer walks into registered worktrees' subdirectories. Covered by Prune_DoesNotRecurseIntoRegisteredWorktrees and Prune_OrphanDirectory_WithNestedSubdirs_StillPrunes in WorktreeCommandTests.