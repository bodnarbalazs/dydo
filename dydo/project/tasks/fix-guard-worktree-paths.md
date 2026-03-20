---
area: general
name: fix-guard-worktree-paths
status: human-reviewed
created: 2026-03-20T13:29:00.5369135Z
assigned: Dexter
updated: 2026-03-20T19:12:16.3540062Z
---

# Task: fix-guard-worktree-paths

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented 3-part fix for guard worktree path normalization. (1) Added PathUtils.GetMainProjectRoot to detect worktree CWD and return main project root. (2) Added ResolveWorktreePath in GuardCommand that resolves relative paths to absolute (only when CWD is a worktree) before applying NormalizeWorktreePath. (3) Fixed GetRelativePath in both AgentRegistry and PathPermissionChecker to use main project root when in a worktree. Added tests for GetMainProjectRoot and PathPermissionChecker worktree scenario. All 225 related tests pass. Gap check failures are all pre-existing (15 modules). See brief at dydo/agents/Dexter/brief-fix-guard-worktree-paths.md for full analysis.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-20 19:15
- Result: PASSED
- Notes: LGTM. Three-part fix is clean, correct, and well-tested. GetMainProjectRoot, ResolveWorktreePath, and GetRelativePath fixes are all minimal and surgical. Tests cover positive/negative cases for all three path resolution scenarios. No coverage regressions (3 gap_check failures are all pre-existing in untouched modules).

Awaiting human approval.