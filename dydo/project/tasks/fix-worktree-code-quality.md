---
area: general
name: fix-worktree-code-quality
status: human-reviewed
created: 2026-04-07T14:24:37.7743784Z
assigned: Grace
updated: 2026-04-07T17:49:39.3013957Z
---

# Task: fix-worktree-code-quality

Refactored three worktree code quality issues from inquisition report. (1) Extracted TeardownWorktree method in WorktreeCommand.cs — shared sequence of PreserveAuditFiles, RemoveJunction (4 junctions now in a single JunctionSubpaths array), RemoveGitWorktree, RemoveZombieDirectory. Branch deletion stays outside since FinalizeMerge uses mergeSource directly. (2) Consolidated Linux/Mac terminal launcher argument duplication — LinuxTerminalLauncher now uses private ApplyOverrides method shared by GetArguments and TryLaunch; MacTerminalLauncher uses private BuildShellComponents shared by GetArguments and Launch. (3) Test coverage blind spot is automatically fixed: tests exercising GetArguments now cover the same code path production uses. Also moved duplicate BashPostClaudeCheck to TerminalLauncher as shared method. All 3477 tests pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Refactored three worktree code quality issues from inquisition report. (1) Extracted TeardownWorktree method in WorktreeCommand.cs — shared sequence of PreserveAuditFiles, RemoveJunction (4 junctions now in a single JunctionSubpaths array), RemoveGitWorktree, RemoveZombieDirectory. Branch deletion stays outside since FinalizeMerge uses mergeSource directly. (2) Consolidated Linux/Mac terminal launcher argument duplication — LinuxTerminalLauncher now uses private ApplyOverrides method shared by GetArguments and TryLaunch; MacTerminalLauncher uses private BuildShellComponents shared by GetArguments and Launch. (3) Test coverage blind spot is automatically fixed: tests exercising GetArguments now cover the same code path production uses. Also moved duplicate BashPostClaudeCheck to TerminalLauncher as shared method. All 3477 tests pass, gap_check green.

## Code Review

- Reviewed by: Henry
- Date: 2026-04-07 17:57
- Result: PASSED
- Notes: LGTM. Clean refactoring: TeardownWorktree correctly consolidates 3 copy-paste sites, Linux/Mac launcher duplication eliminated via ApplyOverrides/BuildShellComponents, BashPostClaudeCheck properly centralized. ValidateWorktreeId is a solid security addition. All 3477 tests pass, gap_check 135/135 green. Minor: DeleteWorktreeBranch mainRoot param is unused but harmless.

Awaiting human approval.