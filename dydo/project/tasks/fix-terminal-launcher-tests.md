---
area: general
name: fix-terminal-launcher-tests
status: human-reviewed
created: 2026-04-03T10:43:40.2059392Z
assigned: Charlie
---

# Task: fix-terminal-launcher-tests

Fixed unprotected StringWriter pattern in 13 test files by wrapping Console.SetOut/SetError calls with TextWriter.Synchronized(). The 2 broken TerminalLauncherTests with stale init-settings assertions were already fixed in commit ca09bfd. All 3425 tests pass, gap_check 132/132 modules at 100%. One pre-existing unrelated failure (CommandDocConsistencyTests.ReadmeClones_ContentInSync — README template out of sync).

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed unprotected StringWriter pattern in 13 test files by wrapping Console.SetOut/SetError calls with TextWriter.Synchronized(). The 2 broken TerminalLauncherTests with stale init-settings assertions were already fixed in commit ca09bfd. All 3425 tests pass, gap_check 132/132 modules at 100%. One pre-existing unrelated failure (CommandDocConsistencyTests.ReadmeClones_ContentInSync — README template out of sync).

## Code Review

- Reviewed by: Emma
- Date: 2026-04-03 11:41
- Result: PASSED
- Notes: LGTM. StringWriter thread-safety fix is mechanical and complete (zero unprotected patterns remain). Init-settings timing fix eliminates the race condition cleanly — synchronous execution in DispatchService with belt-and-suspenders in terminal scripts. Error swallowing replaced with visible warnings. Inquisitor nudge well-tested. 3425/3425 tests pass, gap_check 132/132 at 100%. One pre-existing unrelated failure (ReadmeClones_ContentInSync).

Awaiting human approval.