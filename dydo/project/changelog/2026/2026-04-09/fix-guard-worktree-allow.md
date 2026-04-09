---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-guard-worktree-allow

Implemented 3 fixes: (1) Guard now emits worktree allow JSON for Bash dydo commands via EmitWorktreeAllowIfNeeded in HandleDydoBashCommand, (2) same for Write/Edit operations in HandleWriteOperation, (3) removed | Out-Null from all junction and directory creation lines in WindowsTerminalLauncher.cs. Extracted EmitWorktreeAllowIfNeeded helper to consolidate the worktree allow pattern across Read, Write, and Bash handlers (also removed unused isWorktree parameter from HandleReadOperation). Updated GuardWorktreeAllowTests to assert new behavior. All 3551 tests pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\GuardLiftServiceTests.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-final5-a.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\CompletionProviderTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorktreeCreationLockTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\PathUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Utils\PathUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\GuardLiftService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\CompletionProvider.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Utils\FileReadRetryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardWorktreeAllowTests.cs — Modified


## Review Summary

Implemented 3 fixes: (1) Guard now emits worktree allow JSON for Bash dydo commands via EmitWorktreeAllowIfNeeded in HandleDydoBashCommand, (2) same for Write/Edit operations in HandleWriteOperation, (3) removed | Out-Null from all junction and directory creation lines in WindowsTerminalLauncher.cs. Extracted EmitWorktreeAllowIfNeeded helper to consolidate the worktree allow pattern across Read, Write, and Bash handlers (also removed unused isWorktree parameter from HandleReadOperation). Updated GuardWorktreeAllowTests to assert new behavior. All 3551 tests pass, gap_check green.

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-08 21:35
- Result: PASSED
- Notes: LGTM. All 3 fixes correct: EmitWorktreeAllowIfNeeded consolidation, junction-safe cleanup (ReparsePoint/symlink checks), Out-Null removal. Tests comprehensive with security coverage (blocked ops never emit allow). 3551 tests pass, gap_check green. Minor: missing blank line at GuardCommand.cs:92-93. Out-of-scope: HandleSearchTool and AnalyzeAndCheckBashOperations lack worktree allow emission (pre-existing gap).

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
