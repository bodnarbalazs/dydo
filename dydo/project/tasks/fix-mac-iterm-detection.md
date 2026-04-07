---
area: general
name: fix-mac-iterm-detection
status: human-reviewed
created: 2026-04-07T11:24:14.5919414Z
assigned: Charlie
updated: 2026-04-07T12:01:20.4085774Z
---

# Task: fix-mac-iterm-detection

Fixed macOS iTerm2 detection for agent dispatch. Three changes: (1) Added GetRunningTerminal() to ITerminalDetector — uses TERM_PROGRAM env var to detect the actual running terminal instead of guessing filesystem paths. (2) Updated MacTerminalLauncher.Launch() to use iTerm2 for both tab and window modes when running in iTerm2. (3) Added GetITermWindowScript() for new-window dispatches in iTerm2. Filesystem fallback (IsAvailable) preserved for backward compat when TERM_PROGRAM is not set. Five new tests cover the new behavior.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed macOS iTerm2 detection for agent dispatch. Three changes: (1) Added GetRunningTerminal() to ITerminalDetector — uses TERM_PROGRAM env var to detect the actual running terminal instead of guessing filesystem paths. (2) Updated MacTerminalLauncher.Launch() to use iTerm2 for both tab and window modes when running in iTerm2. (3) Added GetITermWindowScript() for new-window dispatches in iTerm2. Filesystem fallback (IsAvailable) preserved for backward compat when TERM_PROGRAM is not set. Five new tests cover the new behavior.

## Code Review

- Reviewed by: Emma
- Date: 2026-04-07 12:06
- Result: PASSED
- Notes: LGTM. Logic correctly prioritizes TERM_PROGRAM env var over filesystem detection, with backward-compat fallback. GetITermWindowScript follows the same pattern as GetITermTabScript. 4 meaningful tests cover the key scenarios. All 3464 tests pass, gap_check green (135/135).

Awaiting human approval.