---
area: general
name: git-stash-guard-block
status: human-reviewed
created: 2026-03-16T17:24:36.4626892Z
assigned: Dexter
updated: 2026-03-16T17:34:12.0063863Z
---

# Task: git-stash-guard-block

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Added conditional git stash blocking to GuardCommand. git stash (and all variants) is blocked when the agent is NOT in a worktree, but allowed when a .worktree marker exists in the agent workspace. Implementation is in GuardCommand.AnalyzeAndCheckBashOperations with a GitStashRegex. BashCommandAnalyzer was NOT modified -- the check is guard-level because it depends on agent state. 11 integration tests added covering: blocked without worktree (8 variants), allowed with worktree (3 variants), and other git commands not affected (4 cases). Deviation from original brief: Adele sent a correction -- do not block unconditionally, only block when not in a worktree.

## Code Review

- Reviewed by: Grace
- Date: 2026-03-16 17:38
- Result: PASSED
- Notes: LGTM. Clean implementation — git stash conditionally blocked based on worktree state. Regex consistent with existing guard patterns. 15 integration tests all pass, full suite (2621 tests) green. No regressions.

Awaiting human approval.