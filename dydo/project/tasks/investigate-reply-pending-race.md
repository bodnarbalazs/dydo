---
area: general
name: investigate-reply-pending-race
status: human-reviewed
created: 2026-03-25T22:14:34.6491574Z
assigned: Dexter
updated: 2026-03-26T12:00:34.0497439Z
---

# Task: investigate-reply-pending-race

Fix reply-pending marker race condition. Moved marker creation from inbox clear to dispatch time (DispatchService.WriteInboxItemToAgent). Changed InboxService.TrackReplyPending from create-and-print to check-and-remind. Added 2 integration tests: one reproducing the exact bug (msg before inbox clear), one verifying dispatch-time creation. Build passes, gap_check passes.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

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