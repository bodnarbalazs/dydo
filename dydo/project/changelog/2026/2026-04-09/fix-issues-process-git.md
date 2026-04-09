---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-issues-process-git

Fixed 4 process/git safety issues. #18: RunProcessWithExitCode no longer masks exit codes via void override fallback. #19: Added double-dash separator before user-influenced arguments in all git commands. #20: ExecuteCleanup reads .worktree-root before removing markers for consistent -C usage; ExecutePrune derives mainRoot from registry path. #27: RunGitForWorktree now uses timeout with kill. 7 new tests, all 3538 pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\CompactionAtomicWriteTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\AuditXssTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardWorktreeAllowBashWriteTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WindowsTerminalLauncherJunctionErrorTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\SnapshotCompactionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\AuditCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\OffLimitsServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\OffLimitsService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorktreeCreationLockTests.cs — Modified


## Review Summary

Fixed 4 process/git safety issues. #18: RunProcessWithExitCode no longer masks exit codes via void override fallback. #19: Added double-dash separator before user-influenced arguments in all git commands. #20: ExecuteCleanup reads .worktree-root before removing markers for consistent -C usage; ExecutePrune derives mainRoot from registry path. #27: RunGitForWorktree now uses timeout with kill. 7 new tests, all 3538 pass, gap_check green.

## Code Review

- Reviewed by: Emma
- Date: 2026-04-08 19:00
- Result: PASSED
- Notes: LGTM. All 4 fixes verified: #18 exit code isolation, #19 double-dash separators on all user-influenced git args, #20 consistent -C usage in cleanup/prune, #27 timeout+kill in RunGitForWorktree. 7 new tests, 3538/3538 pass, gap_check green.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
