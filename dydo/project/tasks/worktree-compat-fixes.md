---
area: general
name: worktree-compat-fixes
status: human-reviewed
created: 2026-03-20T13:38:41.7780678Z
assigned: Charlie
updated: 2026-03-20T19:02:02.0263211Z
---

# Task: worktree-compat-fixes

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented three worktree compatibility fixes: (1) Added PathUtils.EnsureLocalDirExists and called it from GuardCommand and WatchdogService before writing to _system/.local/; (2) Fixed AgentRegistry.IsWorktreeStale to use _configService.GetDydoRoot instead of _basePath directly, plus call EnsureLocalDirExists; (3) Added PathUtils.IsInsideWorktree and guards in InitCommand, TemplateCommand, and WorkspaceCommand init subcommand. Updated 4 existing tests to match corrected IsWorktreeStale path resolution. Added tests for EnsureLocalDirExists and IsInsideWorktree.

## Code Review (2026-03-20 14:11)

- Reviewed by: Grace
- Result: FAILED
- Issues: FAIL — gap_check exits non-zero. GuardCommand CRAP 30.8>30, WatchdogService line 75.9%<80% + CRAP 32.1>30. Also: EnsureLocalDirExists inside IsWorktreeStale is a side-effect on a query method — remove it, Directory.Exists works fine without the parent existing.

Requires rework.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-20 19:07
- Result: PASSED
- Notes: LGTM. All 3 Grace review issues fixed correctly: GuardCommand CRAP resolved (internal helpers + tests), WatchdogService coverage met (StartProcessOverride/FindProcessesOverride hooks + 7 PollAndCleanup tests), EnsureLocalDirExists removed from IsWorktreeStale (uses GetDydoRoot). Additional scope (roles junction, Read(**) wildcard, worktree-refuse guards, merge docs) is clean and well-tested. 126/129 gap_check — 3 pre-existing failures untouched by this task. 2993/2995 tests pass — 2 pre-existing CommandDocConsistency failures.

Awaiting human approval.