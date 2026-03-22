---
area: general
name: fix-worktree-lifecycle-v2
status: human-reviewed
created: 2026-03-22T14:35:06.1310110Z
assigned: Brian
updated: 2026-03-22T15:17:23.4673350Z
---

# Task: fix-worktree-lifecycle-v2

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed 2 of 4 worktree lifecycle issues with confirmed root causes. Issue 1: DispatchService.CompleteDispatch created dispatch markers with the dispatched task name instead of the sender task, causing HasDispatchMarker lookup to fail and code-writers to dispatch duplicate reviewers. Fixed by using sender.Task instead of task. Issue 2: MessageService.CheckTargetActive blocked reply-pending message sends to inactive targets, creating a Catch-22 where the marker could never be cleared. Fixed by allowing sends when hasReplyPending is true. Issues 3+4: code analysis showed correct logic; may need runtime verification. Added 3 integration tests covering both fixes. All 3000 tests pass.

## Code Review (2026-03-22 15:10)

- Reviewed by: Charlie
- Result: FAILED
- Issues: BuildReplyPendingMessage (MessageService.cs:116-133) is now dead code — its only call site was replaced by 'return null'. Per coding standards, YOUR changes made it unused so it must be deleted. Minor: no-subject sends bypass marker cleanup (see workspace for details).

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-22 15:24
- Result: PASSED
- Notes: LGTM. Dead code removed, reply-pending bypass fixed, no-subject marker cleanup correct. All 3000 tests pass. Coverage gate failures are pre-existing (0 regressions).

Awaiting human approval.