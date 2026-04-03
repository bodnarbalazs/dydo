---
area: general
name: fix-read-permissions-definitive
status: human-reviewed
created: 2026-04-03T12:38:04.9554874Z
assigned: Emma
---

# Task: fix-read-permissions-definitive

Worktree Read allow decision implemented. When the guard approves a Read in a worktree context (CWD contains dydo/_system/.local/worktrees/), it outputs Claude Code's explicit allow JSON to stdout, bypassing the unreliable permission pattern matching. Non-worktree and non-Read operations are completely untouched. Files changed: Commands/GuardCommand.cs (3 small additions), DynaDocs.Tests/Integration/GuardWorktreeAllowTests.cs (new, T2, 14 tests). All 14 new + 156 existing guard tests pass. Plan at dydo/agents/Emma/plan-fix-read-permissions-definitive.md.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Worktree Read allow decision implemented. When the guard approves a Read in a worktree context (CWD contains dydo/_system/.local/worktrees/), it outputs Claude Code's explicit allow JSON to stdout, bypassing the unreliable permission pattern matching. Non-worktree and non-Read operations are completely untouched. Files changed: Commands/GuardCommand.cs (3 small additions), DynaDocs.Tests/Integration/GuardWorktreeAllowTests.cs (new, T2, 14 tests). All 14 new + 156 existing guard tests pass. Plan at dydo/agents/Emma/plan-fix-read-permissions-definitive.md.

## Code Review

- Reviewed by: Jack
- Date: 2026-04-03 15:28
- Result: PASSED
- Notes: LGTM. Code is clean, tests pass. All 14 new tests pass. Security verified: allow JSON only on approved read success path. gap_check: 134/135 pass — the 1 failure (DispatchService CRAP 30.2) is pre-existing from ca09bfd, not a regression.

Awaiting human approval.