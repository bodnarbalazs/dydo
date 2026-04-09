---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-iterm2-dispatch-issues

Implement iTerm2 window ID targeting fix. Read the detailed plan at dydo/agents/Grace/brief-fix-iterm2-dispatch-issues.md. Write the 9 failing tests FIRST (Step 1), then implement the fix (Step 2), then verify all tests pass (Step 3). The plan has exact code for each change. Files: Services/MacTerminalLauncher.cs, Services/TerminalLauncher.cs (proxy), DynaDocs.Tests/Services/TerminalLauncherTests.cs.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented iTerm2 window ID targeting to fix race conditions in back-to-back dispatches. GetITermWindowScript now captures window reference via 'set newWin to (create window with default profile)' and injects DYDO_WINDOW dynamically. GetITermTabScript accepts optional windowId parameter for targeting by ID with try/catch fallback. Launch() rebuilds shell components without windowName for iTerm so AppleScript injects the real ID. Added 9 new tests, updated 2 existing. One plan deviation: Test 3 asserted DoesNotContain('current window') but the on-error fallback legitimately uses it — changed assertion to DoesNotContain('tell current window') to test the primary targeting path while accepting the fallback.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-08 17:43
- Result: PASSED
- Notes: LGTM. BuildShellComponents extraction clean, iTerm window ID targeting correctly solves race conditions, GetRunningTerminal detection logic sound. 9 new tests + 3 detection + 3 security tests meaningful. gap_check 135/135 green, 3511 tests pass.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
