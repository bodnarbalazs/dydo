---
area: general
name: fix-security-path-traversal
status: human-reviewed
created: 2026-04-07T14:24:11.3234684Z
assigned: Charlie
updated: 2026-04-07T18:45:27.6952418Z
---

# Task: fix-security-path-traversal

Fixed 2 path traversal vulnerabilities. (1) TerminalLauncher.GenerateWorktreeId now rejects '.' and '..' task names that would escape the worktrees directory. (2) WorktreeCommand.ExecuteCleanup now validates the worktreeId via a new ValidateWorktreeId method before using it in path operations — rejects '..', '.', backslashes, and unsafe characters in any component. Added 7 tests (6 vulnerability demonstrations + 1 parent-context test). All 3477 tests pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed 2 path traversal vulnerabilities. (1) TerminalLauncher.GenerateWorktreeId now rejects '.' and '..' task names that would escape the worktrees directory. (2) WorktreeCommand.ExecuteCleanup now validates the worktreeId via a new ValidateWorktreeId method before using it in path operations — rejects '..', '.', backslashes, and unsafe characters in any component. Added 7 tests (6 vulnerability demonstrations + 1 parent-context test). All 3477 tests pass, gap_check green.

## Code Review (2026-04-07 17:46)

- Reviewed by: Henry
- Result: FAILED
- Issues: 3 test failures in FinalizeMerge flow. Root cause: branch deletion moved inside TeardownWorktree (conditional on worktreePath != null), but was previously unconditional. Also: out-of-scope changes in InitCommand.cs and RoleDefinitionService.cs.

Requires rework.

## Code Review

- Reviewed by: Grace
- Date: 2026-04-07 18:51
- Result: PASSED
- Notes: LGTM. Both path traversal fixes are correct — ValidateWorktreeId properly rejects traversal components, GenerateWorktreeId rejects . and .. task names. TeardownWorktree extraction is clean, branch deletion remains unconditional in FinalizeMerge (line 582). Terminal launcher dedup via ApplyOverrides/BuildShellComponents preserves exact behavior. 7 new security tests, all 3477 tests pass, gap_check green (135/135 modules). Henry's prior FAIL was a misread — branch -D was never moved into TeardownWorktree.

Awaiting human approval.