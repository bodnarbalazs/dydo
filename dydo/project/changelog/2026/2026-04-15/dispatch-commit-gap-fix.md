---
area: general
type: changelog
date: 2026-04-15
---

# Task: dispatch-commit-gap-fix

Adds a pre-merge safety check to  that refuses when the source branch has 0 commits ahead of base OR the source worktree has uncommitted/untracked files. Fix for the silent-data-loss incident reported 2026-04-14 (code-writer -> reviewer -> merge flow destroying uncommitted work). Changes: (1) Commands/WorktreeCommand.cs — new --force option, new RunProcessCapture helper, new CheckMergeSafety called before git merge; guard skipped on --finalize and --force. Error message is agent-oriented: lists issues, points at worktree path, shows rescue commands. (2) Services/ConfigFactory.cs — new warn-severity default nudge on 'dydo worktree merge --force' so the first invocation blocks with a warning and a retry proceeds. (3) Tests — 5 new WorktreeCommand tests (branch-not-advanced blocks, dirty tree blocks, --force bypasses, --finalize skips guard, rev-list failure blocks), 2 new ConfigFactory theories for the nudge, 6 existing merge tests updated with a MockMergeSafetyChecks helper. Out of scope per the human: dispatch-time guard, code-writer mode template update, dydo wait notification issue. Build clean, 3701/3701 tests pass, gap_check 135/135 modules at 100%.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Adds a pre-merge safety check to  that refuses when the source branch has 0 commits ahead of base OR the source worktree has uncommitted/untracked files. Fix for the silent-data-loss incident reported 2026-04-14 (code-writer -> reviewer -> merge flow destroying uncommitted work). Changes: (1) Commands/WorktreeCommand.cs — new --force option, new RunProcessCapture helper, new CheckMergeSafety called before git merge; guard skipped on --finalize and --force. Error message is agent-oriented: lists issues, points at worktree path, shows rescue commands. (2) Services/ConfigFactory.cs — new warn-severity default nudge on 'dydo worktree merge --force' so the first invocation blocks with a warning and a retry proceeds. (3) Tests — 5 new WorktreeCommand tests (branch-not-advanced blocks, dirty tree blocks, --force bypasses, --finalize skips guard, rev-list failure blocks), 2 new ConfigFactory theories for the nudge, 6 existing merge tests updated with a MockMergeSafetyChecks helper. Out of scope per the human: dispatch-time guard, code-writer mode template update, dydo wait notification issue. Build clean, 3701/3701 tests pass, gap_check 135/135 modules at 100%.

## Code Review (2026-04-15 16:16)

- Reviewed by: Brian
- Result: FAILED
- Issues: FAIL: rev-list command is malformed — will ALWAYS fail in production. See dispatch brief for details.

Requires rework.

## Approval

- Approved: 2026-04-15 16:19
