---
area: general
type: changelog
date: 2026-03-27
---

# Task: fix-merge-discoverability-merge

Merged worktree/fix-merge-discoverability changes into master. Two changes to GuardCommand.cs: (1) git merge is now treated as write-like for must-read enforcement, (2) git merge block now also triggers when .merge-source marker exists (not just in worktrees). Two new integration tests added for the expanded guard behavior. All 3244 tests pass, coverage gate green.

## Progress

- [ ] (Not started)

## Files Changed

C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-final-a.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WorkspaceCleaner.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorkspaceAndCleanTests.cs — Modified


## Review Summary

Merged worktree/fix-merge-discoverability changes into master. Two changes to GuardCommand.cs: (1) git merge is now treated as write-like for must-read enforcement, (2) git merge block now also triggers when .merge-source marker exists (not just in worktrees). Two new integration tests added for the expanded guard behavior. All 3244 tests pass, coverage gate green.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-26 19:51
- Result: PASSED
- Notes: LGTM. Guard correctly extended to treat git merge as write-like for must-read enforcement and to block git merge when .merge-source marker exists. Cleanup properly removes .merge-source. DispatchService merge branch now sets cleanupWorktreeId. All 3244 tests pass, coverage gate green (131/131). Code is clean and surgical.

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
