---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-bash-write-allow

Extended worktree allow JSON to non-dydo bash commands (AnalyzeAndCheckBashOperations) and guard-lifted write path (HandleWriteOperation). Renamed WorktreeReadAllowJson to WorktreeAllowJson. Added 4 new tests: non-dydo bash worktree allow, non-dydo bash non-worktree no-allow, guard-lifted write worktree allow, dangerous command blocked no-allow. All 24 worktree allow tests pass. Coverage gate green. Note: 4 pre-existing flaky WorktreeCreationLockTests junction failures in full suite (pass in isolation).

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardWorktreeAllowBashWriteTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified


## Review Summary

Extended worktree allow JSON to non-dydo bash commands (AnalyzeAndCheckBashOperations) and guard-lifted write path (HandleWriteOperation). Renamed WorktreeReadAllowJson to WorktreeAllowJson. Added 4 new tests: non-dydo bash worktree allow, non-dydo bash non-worktree no-allow, guard-lifted write worktree allow, dangerous command blocked no-allow. All 24 worktree allow tests pass. Coverage gate green. Note: 4 pre-existing flaky WorktreeCreationLockTests junction failures in full suite (pass in isolation).

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-09 22:13
- Result: PASSED
- Notes: LGTM. All 3649 tests pass, gap_check green (136/136). Security improvements: OffLimitsService correctly prioritizes system patterns over whitelist; junction-safe delete handles unknown junctions. Out-of-scope finding: RemoveZombieDirectory (WorktreeCommand.cs:480) still uses Directory.Delete(recursive) without junction-safe handling.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
