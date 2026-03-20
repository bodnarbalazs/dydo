---
area: general
name: fix-worktree-read-permissions
status: human-reviewed
created: 2026-03-20T12:15:21.1311866Z
assigned: Brian
updated: 2026-03-20T12:19:55.6032694Z
---

# Task: fix-worktree-read-permissions

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed Read permission path format in worktree init-settings. The bug: ExecuteInitSettings was normalizing backslashes to forward slashes (Replace('\', '/')) before writing the Read permission entry. On Windows, Claude Code uses backslash paths in Read tool calls, so the forward-slash glob never matched. Fix: changed to TrimEnd('/', '\') to preserve native path format. Added test InitSettings_PreservesBackslashes_OnWindowsPaths. Updated two existing tests to match the new normalization. All 38 WorktreeCommandTests pass.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-20 12:28
- Result: PASSED
- Notes: LGTM. 1-line fix is correct and minimal — removes bad Replace('\','/') normalization, preserves native path format. New test validates the fix. All 38 WorktreeCommandTests pass. gap_check failures are all pre-existing in unrelated modules (no regression).

Awaiting human approval.