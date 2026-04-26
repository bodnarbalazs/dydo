---
area: platform
type: changelog
date: 2026-04-26
---

# Task: worktree-cleanup-hardening

Fix critical #104 (junction-follow deletion) + #98 (prune doesn't sweep orphan watchdog.pid) + cleanup-marker + prune-subdirs bugs.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Review worktree-cleanup-hardening (issues #104, #98, and 2 unfiled bugs). Changes: Commands/WorktreeCommand.cs only. (1) TeardownWorktree reorders so RemoveZombieDirectory runs BEFORE RemoveGitWorktree — junction-safe delete happens before git's --force that may follow reparse points on Windows. (2) DeleteDirectoryJunctionSafe unlinks reparse points via Directory.Delete(recursive:false) instead of cmd rmdir — avoids the cmd-subprocess dependency that can fail silently. (3) ExecutePrune enumerates top-level dirs only (replaced CollectLeafDirectories recursion) — no longer walks into registered worktrees' .claude/.github subdirs. (4) ReportStrandedWatchdogPid added — orphan watchdog.pid inside pruned worktrees is noted (dead) or warned-on-stderr (alive) via ProcessUtils.IsProcessRunning. Reproducers in DynaDocs.Tests/Commands/WorktreeCommandTests.cs: Cleanup_WithJunctionToMainAgents_DoesNotDeleteMainAgents, Cleanup_WithUnknownJunction_DoesNotDeleteJunctionTarget, Cleanup_DirectoryLocked_StillClearsAgentMarker (validates existing correct order — brief's reorder claim was outdated), Prune_StrandedWatchdogPidInOrphanWorktree_IsReported, Prune_StrandedWatchdogPidAlive_EmitsWarningToStderr, Prune_DoesNotRecurseIntoRegisteredWorktrees, Prune_OrphanDirectory_WithNestedSubdirs_StillPrunes. Issues #104 and #98 resolved via dydo issue resolve. Full test suite: 3776/3776 passing, gap_check 100%. No other files touched. Two unfiled bugs (cleanup-marker-clear — actually already correct, test is regression guard; prune-subdirs-walk) need issue numbers from Brian.

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-20 18:38
- Result: PASSED
- Notes: LGTM. 4 changes in Commands/WorktreeCommand.cs: (1) TeardownWorktree reorder — RemoveZombieDirectory (junction-safe) runs before RemoveGitWorktree, defending against git --force following reparse points on Windows. (2) DeleteDirectoryJunctionSafe uses Directory.Delete(recursive:false) for reparse points, with RemoveJunction fallback — removes cmd-subprocess dependency. (3) ExecutePrune uses top-level Directory.GetDirectories instead of recursive leaf walk — no longer strays into registered worktrees' .claude/.github subdirs. (4) ReportStrandedWatchdogPid surfaces orphan watchdog.pid files on stderr (alive) or stdout (dead). Tests (7 new) cover junction-follow (#104 repro), unknown junction, locked-directory marker-clear, stranded pid (alive/dead), registered-worktree skip, nested-orphan prune. Full suite 3776/3776 passing; gap_check 100%. Minor nit (non-blocking): the 'alive' warning says 'investigate before relying on prune' but teardown proceeds regardless — message could be clearer, but strictly better than prior silent delete.

Awaiting human approval.

## Approval

- Approved: 2026-04-26 19:39
