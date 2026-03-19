---
area: general
name: task-approve-compact
status: human-reviewed
created: 2026-03-19T19:59:38.7619349Z
assigned: Brian
updated: 2026-03-19T20:36:50.3708866Z
---

# Task: task-approve-compact

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented plan: decoupled audit compaction from task approve. Removed CompactAuditSnapshots from ExecuteApprove, created TaskCompactHandler with dydo task compact command, added auto-compact via counter file in _system/compact-counter (configurable via tasks.autoCompactInterval in dydo.json, default 20, 0 disables). Updated tests: replaced Task_Approve_CompactsAuditSnapshots with Task_Approve_DoesNotCompactByDefault, added Task_Compact_CompactsAuditSnapshots, Task_Compact_NothingToCompact, Task_Approve_AutoCompact_TriggersAtInterval, Task_Approve_AutoCompact_DisabledWhenZero. All 44 task tests pass. No plan deviations.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-19 20:42
- Result: PASSED
- Notes: LGTM. Clean decoupling of compaction from approve. Counter-based auto-compact is well-designed — configurable interval, disabled-when-zero, non-blocking on failure. Tests comprehensive: 5 new/updated tests cover all paths. Code follows existing patterns. All 44 task tests pass. Gap check failures are all pre-existing (15 modules, none touched by this change).

Awaiting human approval.