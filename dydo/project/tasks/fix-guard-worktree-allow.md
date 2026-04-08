---
area: general
name: fix-guard-worktree-allow
status: human-reviewed
created: 2026-04-08T20:41:29.3724490Z
assigned: Brian
updated: 2026-04-08T21:27:51.0891660Z
---

# Task: fix-guard-worktree-allow

Implemented 3 fixes: (1) Guard now emits worktree allow JSON for Bash dydo commands via EmitWorktreeAllowIfNeeded in HandleDydoBashCommand, (2) same for Write/Edit operations in HandleWriteOperation, (3) removed | Out-Null from all junction and directory creation lines in WindowsTerminalLauncher.cs. Extracted EmitWorktreeAllowIfNeeded helper to consolidate the worktree allow pattern across Read, Write, and Bash handlers (also removed unused isWorktree parameter from HandleReadOperation). Updated GuardWorktreeAllowTests to assert new behavior. All 3551 tests pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented 3 fixes: (1) Guard now emits worktree allow JSON for Bash dydo commands via EmitWorktreeAllowIfNeeded in HandleDydoBashCommand, (2) same for Write/Edit operations in HandleWriteOperation, (3) removed | Out-Null from all junction and directory creation lines in WindowsTerminalLauncher.cs. Extracted EmitWorktreeAllowIfNeeded helper to consolidate the worktree allow pattern across Read, Write, and Bash handlers (also removed unused isWorktree parameter from HandleReadOperation). Updated GuardWorktreeAllowTests to assert new behavior. All 3551 tests pass, gap_check green.

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-08 21:35
- Result: PASSED
- Notes: LGTM. All 3 fixes correct: EmitWorktreeAllowIfNeeded consolidation, junction-safe cleanup (ReparsePoint/symlink checks), Out-Null removal. Tests comprehensive with security coverage (blocked ops never emit allow). 3551 tests pass, gap_check green. Minor: missing blank line at GuardCommand.cs:92-93. Out-of-scope: HandleSearchTool and AnalyzeAndCheckBashOperations lack worktree allow emission (pre-existing gap).

Awaiting human approval.