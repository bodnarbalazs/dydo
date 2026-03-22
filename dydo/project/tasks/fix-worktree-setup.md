---
area: general
name: fix-worktree-setup
status: human-reviewed
created: 2026-03-22T16:37:16.6404747Z
assigned: Brian
updated: 2026-03-22T17:12:30.5870582Z
---

# Task: fix-worktree-setup

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed the git setup failure where the target directory was pre-created, causing git to fail silently. Changes in WindowsTerminalLauncher.cs and TerminalLauncher.cs: (1) mkdir/New-Item now creates the parent dir only, not the target, (2) stale target dirs are removed before setup, (3) PowerShell exit code checking added. 12 new tests verify the fix. No plan deviations. gap_check has 13 pre-existing failures in unrelated modules; all 4 terminal launcher files pass (WindowsTerminalLauncher at 100%).

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-22 17:16
- Result: PASSED
- Notes: LGTM. All three fixes (parent-only mkdir, stale dir cleanup, PS exit code check) are correct and consistent across Windows/Linux/Mac paths. 12 new tests cover the changes well. No regressions — 13 gap_check failures are pre-existing in unrelated modules.

Awaiting human approval.