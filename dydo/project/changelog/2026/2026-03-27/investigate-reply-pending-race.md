---
area: general
type: changelog
date: 2026-03-27
---

# Task: investigate-reply-pending-race

Fix reply-pending marker race condition. Moved marker creation from inbox clear to dispatch time (DispatchService.WriteInboxItemToAgent). Changed InboxService.TrackReplyPending from create-and-print to check-and-remind. Added 2 integration tests: one reproducing the exact bug (msg before inbox clear), one verifying dispatch-time creation. Build passes, gap_check passes.

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
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchWaitIntegrationTests.cs — Modified


## Review Summary

Fix reply-pending marker race condition. Moved marker creation from inbox clear to dispatch time (DispatchService.WriteInboxItemToAgent). Changed InboxService.TrackReplyPending from create-and-print to check-and-remind. Added 2 integration tests: one reproducing the exact bug (msg before inbox clear), one verifying dispatch-time creation. Build passes, gap_check passes.

## Code Review (2026-03-26 11:46)

- Reviewed by: Charlie
- Result: FAILED
- Issues: FAIL: .reply-pending missing from SystemManagedEntries (AgentRegistry.cs:2082). ArchiveWorkspace destroys the marker on target agent claim, defeating the guardrail. Existing test Release_BlockedByReplyPending fails. New test Release_SucceedsWhenReplyBeforeInboxClear is a false positive (passes because archive wiped the marker, not because msg cleared it). Fix: add .reply-pending to SystemManagedEntries.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-26 12:09
- Result: PASSED
- Notes: LGTM. Marker creation correctly moved to dispatch time. SystemManagedEntries updated in both AgentRegistry and WorkspaceArchiver. InboxService now check-and-remind instead of create-and-print. New test covers exact race scenario with marker-survival assertion after claim — false-positive from previous review is fixed. All 4 reply-pending tests pass. gap_check exit 1 is from QueueCommand.cs (dispatch-queue task), not this fix.

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
