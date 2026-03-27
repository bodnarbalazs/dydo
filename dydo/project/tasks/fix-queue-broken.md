---
area: general
name: fix-queue-broken
status: human-reviewed
created: 2026-03-27T13:47:40.5415049Z
assigned: Dexter
updated: 2026-03-27T17:38:45.3857856Z
---

# Task: fix-queue-broken

Fixed race condition where watchdog cleared queue _active.json before terminal PID was written. Root cause: TryAcquireOrEnqueue wrote placeholder Pid=0, and ProcessUtils.IsProcessRunning(0) returns false, so the watchdog treated it as stale. Fix: use Environment.ProcessId as placeholder so the dispatching process keeps the entry alive until UpdateActivePid writes the terminal PID. Added regression test TryAcquireOrEnqueue_PlaceholderPid_SurvivesStaleDetection. No plan deviations.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

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