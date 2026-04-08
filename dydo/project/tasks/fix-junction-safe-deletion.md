---
area: general
name: fix-junction-safe-deletion
status: human-reviewed
created: 2026-04-08T20:48:36.0074036Z
assigned: Charlie
updated: 2026-04-08T21:34:34.2590943Z
---

# Task: fix-junction-safe-deletion

Implemented junction-safe deletion across three areas: (1) DispatchService.CreateGitWorktree removes junctions via WorktreeCommand.RemoveJunction before recursive delete of stale directories, (2) WindowsTerminalLauncher uses ReparsePoint attribute checks to distinguish junctions from regular directories — junctions get plain rmdir, non-junctions get Remove-Item -Recurse -Force, (3) bash WorktreeSetupScript uses test -L to detect symlinks before removal. Changed JunctionSubpaths visibility from private to internal. Updated 4 pre-existing tests that asserted old unsafe behavior. All 3551 tests pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented junction-safe deletion across three areas: (1) DispatchService.CreateGitWorktree removes junctions via WorktreeCommand.RemoveJunction before recursive delete of stale directories, (2) WindowsTerminalLauncher uses ReparsePoint attribute checks to distinguish junctions from regular directories — junctions get plain rmdir, non-junctions get Remove-Item -Recurse -Force, (3) bash WorktreeSetupScript uses test -L to detect symlinks before removal. Changed JunctionSubpaths visibility from private to internal. Updated 4 pre-existing tests that asserted old unsafe behavior. All 3551 tests pass, gap_check green.

## Code Review

- Reviewed by: Brian
- Date: 2026-04-08 21:45
- Result: PASSED
- Notes: LGTM. Junction-safe deletion is correctly implemented across all three code paths (DispatchService C#, TerminalLauncher bash, WindowsTerminalLauncher PowerShell). EmitWorktreeAllowIfNeeded refactor is clean. All 3551 tests pass, gap_check green (135/135). Minor: stale TDD red-phase comments in two new test files, and one test loop that doesn't use its iteration variable.

Awaiting human approval.