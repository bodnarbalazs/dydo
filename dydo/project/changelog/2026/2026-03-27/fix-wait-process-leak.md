---
area: general
type: changelog
date: 2026-03-27
---

# Task: fix-wait-process-leak

Implemented parent PID liveness check in both WaitForTask and WaitGeneral loops (WaitCommand.cs). When the parent process dies, the wait exits cleanly and resets its marker. Added try/finally for marker cleanup, Console.CancelKeyPress for graceful Ctrl+C, and a general wait marker so WaitGeneral records its PID. Added IsProcessRunningOverride to ProcessUtils for testability (follows existing PowerShellResolverOverride pattern). 4 new integration tests cover parent-death exit, marker cleanup, and message-found paths. No plan deviations.

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
C:/Users/User/Desktop/Projects/DynaDocs/Services/DispatchService.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/Commands/WorktreeCommand.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Commands/WorktreeCommandTests.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Integration/WorktreeDispatchTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ProcessUtils.Ancestry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WaitCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WaitCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\gap_check.py — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ProcessUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchWaitIntegrationTests.cs — Modified


## Review Summary

Implemented parent PID liveness check in both WaitForTask and WaitGeneral loops (WaitCommand.cs). When the parent process dies, the wait exits cleanly and resets its marker. Added try/finally for marker cleanup, Console.CancelKeyPress for graceful Ctrl+C, and a general wait marker so WaitGeneral records its PID. Added IsProcessRunningOverride to ProcessUtils for testability (follows existing PowerShellResolverOverride pattern). 4 new integration tests cover parent-death exit, marker cleanup, and message-found paths. No plan deviations.

## Code Review (2026-03-25 22:10)

- Reviewed by: Frank
- Result: FAILED
- Issues: Two issues: (1) ProcessUtils.cs:11-17: Misplaced XML doc — the original summary for IsProcessRunning now sits above IsProcessRunningOverride, giving it two stacked <summary> blocks while IsProcessRunning at line 20 lost its doc. (2) WatchdogService.cs:188-241: TryCloseWindow and ResolveWtExe are dead code orphaned by this change — no production caller remains (grep-confirmed). Per coding standards, orphaned code must be removed. Their tests (TryCloseWindow_InvalidWindowId_DoesNotThrow, ResolveWtExe_ReturnsNonNullOnWindowsWithTerminal) should go too.

Requires rework.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-25 22:22
- Result: PASSED
- Notes: LGTM. Both review issues from Frank resolved correctly: (1) XML docs on ProcessUtils properly repositioned — each member has exactly one summary. (2) TryCloseWindow and ResolveWtExe dead code cleanly removed with their tests. No orphaned references in source. Original WaitCommand implementation is solid: parent PID liveness, try/finally cleanup, CancelKeyPress handling. 116 relevant tests pass, gap_check 129/129.

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
