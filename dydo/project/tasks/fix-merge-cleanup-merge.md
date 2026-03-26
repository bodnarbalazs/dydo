---
area: general
name: fix-merge-cleanup-merge
status: review-pending
created: 2026-03-26T19:18:46.2735187Z
assigned: Dexter
updated: 2026-03-26T19:39:16.3820024Z
---

# Task: fix-merge-cleanup-merge

Merged worktree/fix-merge-cleanup changes into master. Four files changed: (1) WorktreeCommand.cs — ExecuteCleanup now calls RemoveAllMarkers instead of RemoveWorktreeMarkers, ensuring .merge-source is cleaned up. (2) DispatchService.cs — Added cleanupWorktreeId = senderWorktreeId for merge dispatches so the sender worktree gets cleaned up. (3) WorktreeCommandTests.cs — Added Cleanup_RemovesMergeSourceMarker and Cleanup_IdempotentAfterFinalizeMerge tests. (4) WorktreeDispatchTests.cs — Updated Cleanup_PreservesMergeSourceMarker to Cleanup_RemovesMergeSourceMarker with corrected assertion. All 3241 tests pass, coverage gate clear.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Merged worktree/fix-merge-cleanup changes into master. Four files changed: (1) WorktreeCommand.cs — ExecuteCleanup now calls RemoveAllMarkers instead of RemoveWorktreeMarkers, ensuring .merge-source is cleaned up. (2) DispatchService.cs — Added cleanupWorktreeId = senderWorktreeId for merge dispatches so the sender worktree gets cleaned up. (3) WorktreeCommandTests.cs — Added Cleanup_RemovesMergeSourceMarker and Cleanup_IdempotentAfterFinalizeMerge tests. (4) WorktreeDispatchTests.cs — Updated Cleanup_PreservesMergeSourceMarker to Cleanup_RemovesMergeSourceMarker with corrected assertion. All 3241 tests pass, coverage gate clear.