---
area: general
name: fix-worktree-claude-permissions
status: human-reviewed
created: 2026-03-19T20:04:43.3720320Z
assigned: Charlie
updated: 2026-03-19T20:32:09.5462081Z
---

# Task: fix-worktree-claude-permissions

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented init-settings subcommand on 'dydo worktree'. When dispatching to worktrees, settings.local.json is now copied AND a Read permission for the main repo path is injected, fixing the Claude Code permission prompt caused by junction-resolved paths. Changed WorktreeCommand.cs (new subcommand), TerminalLauncher.cs (bash), WindowsTerminalLauncher.cs (Windows). Updated 7 existing tests and added 5 new ones. No plan deviations. Pre-existing gap_check failures (15 modules) are unrelated to these changes.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-19 20:35
- Result: PASSED
- Notes: LGTM. Clean implementation of init-settings subcommand. Correctly copies settings.local.json and injects Read permission for main repo path. Logic handles edge cases (missing source, invalid JSON, idempotency). Tests are thorough. No coverage regressions. Shell scripts simplified on all platforms.

Awaiting human approval.