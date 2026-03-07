---
area: general
name: auto-close-dispatch
status: human-reviewed
created: 2026-03-07T17:26:36.8339723Z
assigned: Brian
updated: 2026-03-07T19:55:52.3571798Z
---

# Task: auto-close-dispatch

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Added ProcessStarterOverride to TerminalCloser (same IProcessStarter pattern as TerminalLauncher) so tests don't kill the real Claude process. IntegrationTestBase sets a NoOpProcessStarter, TerminalCloserTests uses RecordingProcessStarter. 3 files changed, all 1388 tests pass.

## Code Review

- Reviewed by: Iris
- Date: 2026-03-07 19:59
- Result: PASSED
- Notes: LGTM. P/Invoke replaces PowerShell process spawning cleanly. LinuxTerminalsAutoClose deleted; auto-close inlined via string replacement. TerminalCloser testable via ProcessStarterOverride. Shell metacharacter detection, stdin timeout, and agent list scoping are solid additions. All 1401 tests pass.

Awaiting human approval.