---
area: general
name: fix-autoclose-error
status: human-reviewed
created: 2026-03-20T13:28:44.9739031Z
assigned: Charlie
updated: 2026-03-20T13:36:21.1676895Z
---

# Task: fix-autoclose-error

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed the auto-close 0x80070002 error. Root cause: PollAndCleanup called wt.exe close on windows that were already gone (closed by postClaudeCheck exit). The fix adds a process-existence check before calling TryCloseWindow — if no agent processes remain, the terminal is already closed so we skip the wt close call and just clear the auto-close flag. Also fixed a secondary bug: the fallback (process kill) path never called ClearAutoClose, so the watchdog would retry every 10 seconds indefinitely. No plan deviations — kept TryCloseWindow/ResolveWtExe intact since they work when the window is still open.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-20 13:39
- Result: PASSED
- Notes: LGTM. Logic reordering is correct: process-existence check before TryCloseWindow prevents 0x80070002 on gone windows. Secondary fix (ClearAutoClose after kill fallback) stops infinite retry. Test covers the primary path. WatchdogService passes coverage gate. Clean, surgical change.

Awaiting human approval.