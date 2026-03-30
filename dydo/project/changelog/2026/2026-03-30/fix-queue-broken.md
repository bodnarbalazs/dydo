---
area: general
type: changelog
date: 2026-03-30
---

# Task: fix-queue-broken

Fixed race condition where watchdog cleared queue _active.json before terminal PID was written. Root cause: TryAcquireOrEnqueue wrote placeholder Pid=0, and ProcessUtils.IsProcessRunning(0) returns false, so the watchdog treated it as stale. Fix: use Environment.ProcessId as placeholder so the dispatching process keeps the entry alive until UpdateActivePid writes the terminal PID. Added regression test TryAcquireOrEnqueue_PlaceholderPid_SurvivesStaleDetection. No plan deviations.

## Progress

- [ ] (Not started)

## Files Changed

C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-final3-b.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\LinuxTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\QueueService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\QueueServiceTests.cs — Modified


## Review Summary

Fixed race condition where watchdog cleared queue _active.json before terminal PID was written. Root cause: TryAcquireOrEnqueue wrote placeholder Pid=0, and ProcessUtils.IsProcessRunning(0) returns false, so the watchdog treated it as stale. Fix: use Environment.ProcessId as placeholder so the dispatching process keeps the entry alive until UpdateActivePid writes the terminal PID. Added regression test TryAcquireOrEnqueue_PlaceholderPid_SurvivesStaleDetection. No plan deviations.

## Code Review (2026-03-27 14:18)

- Reviewed by: Charlie
- Result: FAILED
- Issues: XML doc comment on TryAcquireOrEnqueue (QueueService.cs:119) still says 'placeholder PID=0' but code now uses Environment.ProcessId. Comment must be updated to match.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-27 17:46
- Result: PASSED
- Notes: LGTM. XML doc comment now accurately says 'dispatching process PID'. Inline comments explain the why. gap_check passes (131/131 modules). Clean fix.

Awaiting human approval.

## Approval

- Approved: 2026-03-30 17:16
