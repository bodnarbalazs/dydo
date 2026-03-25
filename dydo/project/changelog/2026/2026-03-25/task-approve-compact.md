---
area: general
type: changelog
date: 2026-03-25
---

# Task: task-approve-compact

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\NudgeConfig.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v8.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v4.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v11.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v9.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v10.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\ConfigFactory.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigFactoryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\GuardCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\LinuxTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\DydoConfig.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ValidationService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ValidationServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\BashCommandAnalyzerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\ReviewCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MessageService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\MessageIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchWaitIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleConstraintEvaluator.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleConstraintEvaluatorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WorkspaceArchiver.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorkspaceArchiveTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorkspaceArchiverTests.cs — Modified


## Review Summary

Implemented plan: decoupled audit compaction from task approve. Removed CompactAuditSnapshots from ExecuteApprove, created TaskCompactHandler with dydo task compact command, added auto-compact via counter file in _system/compact-counter (configurable via tasks.autoCompactInterval in dydo.json, default 20, 0 disables). Updated tests: replaced Task_Approve_CompactsAuditSnapshots with Task_Approve_DoesNotCompactByDefault, added Task_Compact_CompactsAuditSnapshots, Task_Compact_NothingToCompact, Task_Approve_AutoCompact_TriggersAtInterval, Task_Approve_AutoCompact_DisabledWhenZero. All 44 task tests pass. No plan deviations.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-19 20:42
- Result: PASSED
- Notes: LGTM. Clean decoupling of compaction from approve. Counter-based auto-compact is well-designed — configurable interval, disabled-when-zero, non-blocking on failure. Tests comprehensive: 5 new/updated tests cover all paths. Code follows existing patterns. All 44 task tests pass. Gap check failures are all pre-existing (15 modules, none touched by this change).

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
