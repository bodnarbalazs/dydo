---
area: general
name: fix-wait-process-leak
status: human-reviewed
created: 2026-03-25T20:46:07.4039357Z
assigned: Dexter
updated: 2026-03-25T22:18:42.2106420Z
---

# Task: fix-wait-process-leak

Implemented parent PID liveness check in both WaitForTask and WaitGeneral loops (WaitCommand.cs). When the parent process dies, the wait exits cleanly and resets its marker. Added try/finally for marker cleanup, Console.CancelKeyPress for graceful Ctrl+C, and a general wait marker so WaitGeneral records its PID. Added IsProcessRunningOverride to ProcessUtils for testability (follows existing PowerShellResolverOverride pattern). 4 new integration tests cover parent-death exit, marker cleanup, and message-found paths. No plan deviations.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

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