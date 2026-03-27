---
area: general
type: changelog
date: 2026-03-27
---

# Task: fix-phantom-close

Implemented Option 3 (wait-then-verify) to fix the phantom close TOCTOU race. PollAndCleanup now defers intervention on first sighting of a free auto-close agent with processes still running, letting natural exit work. On the next poll cycle, if processes persist, kills non-shell processes as fallback. TryCloseWindow is no longer called from PollAndCleanup. No plan deviations. All 3193 tests pass, coverage gate 100%.

## Progress

- [ ] (Not started)

## Files Changed

C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-final-a.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Models/QueueResult.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-comp-b.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WorkspaceCleaner.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorkspaceAndCleanTests.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/Services/ConfigFactory.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/Commands/TemplateCommand.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/Services/QueueService.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/Services/DispatchService.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Services/ConfigFactoryTests.cs — Modified
C:/Users/User/Desktop/Projects/DynaDocs/DynaDocs.Tests/Services/QueueServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ProcessUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified


## Review Summary

Implemented Option 3 (wait-then-verify) to fix the phantom close TOCTOU race. PollAndCleanup now defers intervention on first sighting of a free auto-close agent with processes still running, letting natural exit work. On the next poll cycle, if processes persist, kills non-shell processes as fallback. TryCloseWindow is no longer called from PollAndCleanup. No plan deviations. All 3193 tests pass, coverage gate 100%.

## Code Review (2026-03-25 22:11)

- Reviewed by: Dexter
- Result: FAILED
- Issues: Dead code: TryCloseWindow and ResolveWtExe have no production callers after PollAndCleanup change. Orphaned XML doc in ProcessUtils.cs. Core logic is correct.

Requires rework.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-25 22:26
- Result: PASSED
- Notes: LGTM. All three Dexter issues fixed: TryCloseWindow and ResolveWtExe dead code removed from WatchdogService.cs, orphaned XML doc repositioned in ProcessUtils.cs, two dead tests removed. 39 WatchdogService tests pass, coverage gate 129/129 (100%). Out-of-scope: Release_BlockedByReplyPending test failing in DispatchWaitIntegrationTests (unrelated).

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
