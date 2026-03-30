---
area: general
type: changelog
date: 2026-03-30
---

# Task: fix-autoclose-race

Fixed the auto-close race condition in WatchdogService.PollAndCleanup. Removed the two-poll deferral that allowed agent re-dispatch between polls to leave old sessions alive (the phantom close issue it guarded against was already fixed separately). Added a test reproducing the exact race scenario from the investigation. All 43 watchdog tests pass, gap_check clean.

## Progress

- [ ] (Not started)

## Files Changed

C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-final3-a.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Commands/WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentRegistryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\AgentStatus.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\AgentListHandler.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentCrudOperations.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentStateStore.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\AgentListHandlerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardLiftCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardLiftTests.cs — Modified


## Review Summary

Fixed the auto-close race condition in WatchdogService.PollAndCleanup. Removed the two-poll deferral that allowed agent re-dispatch between polls to leave old sessions alive (the phantom close issue it guarded against was already fixed separately). Added a test reproducing the exact race scenario from the investigation. All 43 watchdog tests pass, gap_check clean.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-27 18:43
- Result: PASSED
- Notes: LGTM. Clean fix — deferral removal eliminates the race window, regression test reproduces the exact scenario. Net -72 lines, 43/43 tests pass, gap_check clean.

Awaiting human approval.

## Approval

- Approved: 2026-03-30 17:16
