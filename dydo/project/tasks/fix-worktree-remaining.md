---
area: general
name: fix-worktree-remaining
status: human-reviewed
created: 2026-03-22T23:47:26.0992803Z
assigned: Brian
updated: 2026-03-23T00:14:48.5466369Z
---

# Task: fix-worktree-remaining

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed three worktree issues: (1) Added init-settings to inherited worktree launch path in all three platform launchers (Windows/Linux/Mac), (2) Fixed FinalizeMerge cleanup order - now uses git -C for worktree remove, adds worktree prune, and makes cleanup best-effort with warnings, (3) Added CatHeredocRegex to BashCommandAnalyzer to strip heredoc blocks before analysis, preventing false command-substitution warnings and write-on-EOF blocks. All 3013+ tests pass. No plan deviations.

## Code Review (2026-03-23 00:09)

- Reviewed by: Charlie
- Result: FAILED
- Issues: FAIL: LinuxTerminalLauncher.cs lines 32 and 68 build init-settings command inline without single-quote escaping, unlike Mac which uses the shared WorktreeInitSettingsScript() helper. Fix: replace inline construction with TerminalLauncher.WorktreeInitSettingsScript(mainProjectRoot) in both locations. Everything else is clean — FinalizeMerge git -C fix, worktree prune sequencing, CatHeredocRegex, and all tests are correct.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-23 00:19
- Result: PASSED
- Notes: LGTM. Both inline init-settings constructions in LinuxTerminalLauncher.cs (lines 32 and 67) correctly replaced with TerminalLauncher.WorktreeInitSettingsScript(mainProjectRoot), matching MacTerminalLauncher. Single-quote escaping is now handled by the shared helper. 3020/3021 tests pass (1 flaky dispose in unrelated PathUtilsDiscoveryTests). gap_check has 13 pre-existing failures in unrelated modules — no regressions from this change.

Awaiting human approval.