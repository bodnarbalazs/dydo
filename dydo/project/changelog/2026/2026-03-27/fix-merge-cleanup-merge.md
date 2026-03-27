---
area: general
type: changelog
date: 2026-03-27
---

# Task: fix-merge-cleanup-merge

Merged worktree/fix-merge-cleanup changes into master. Four files changed: (1) WorktreeCommand.cs — ExecuteCleanup now calls RemoveAllMarkers instead of RemoveWorktreeMarkers, ensuring .merge-source is cleaned up. (2) DispatchService.cs — Added cleanupWorktreeId = senderWorktreeId for merge dispatches so the sender worktree gets cleaned up. (3) WorktreeCommandTests.cs — Added Cleanup_RemovesMergeSourceMarker and Cleanup_IdempotentAfterFinalizeMerge tests. (4) WorktreeDispatchTests.cs — Updated Cleanup_PreservesMergeSourceMarker to Cleanup_RemovesMergeSourceMarker with corrected assertion. All 3241 tests pass, coverage gate clear.

## Progress

- [ ] (Not started)

## Files Changed

C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-final-b.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\QueueServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\QueueService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorktreeDispatchTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ProcessUtils.Ancestry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WaitCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WaitCommand.cs — Modified


## Review Summary

Merged worktree/fix-merge-cleanup changes into master. Four files changed: (1) WorktreeCommand.cs — ExecuteCleanup now calls RemoveAllMarkers instead of RemoveWorktreeMarkers, ensuring .merge-source is cleaned up. (2) DispatchService.cs — Added cleanupWorktreeId = senderWorktreeId for merge dispatches so the sender worktree gets cleaned up. (3) WorktreeCommandTests.cs — Added Cleanup_RemovesMergeSourceMarker and Cleanup_IdempotentAfterFinalizeMerge tests. (4) WorktreeDispatchTests.cs — Updated Cleanup_PreservesMergeSourceMarker to Cleanup_RemovesMergeSourceMarker with corrected assertion. All 3241 tests pass, coverage gate clear.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-26 19:45
- Result: PASSED
- Notes: LGTM. Clean fix: RemoveAllMarkers correctly extends cleanup to include .merge-source, cleanupWorktreeId properly set for merge dispatches. Tests are meaningful and cover both the fix and idempotency. 3244 tests pass, coverage gate clear.

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
