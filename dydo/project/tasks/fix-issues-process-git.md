---
area: general
name: fix-issues-process-git
status: human-reviewed
created: 2026-04-08T18:06:26.7712183Z
assigned: Dexter
updated: 2026-04-08T18:50:43.1942218Z
---

# Task: fix-issues-process-git

Fixed 4 process/git safety issues. #18: RunProcessWithExitCode no longer masks exit codes via void override fallback. #19: Added double-dash separator before user-influenced arguments in all git commands. #20: ExecuteCleanup reads .worktree-root before removing markers for consistent -C usage; ExecutePrune derives mainRoot from registry path. #27: RunGitForWorktree now uses timeout with kill. 7 new tests, all 3538 pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed 4 process/git safety issues. #18: RunProcessWithExitCode no longer masks exit codes via void override fallback. #19: Added double-dash separator before user-influenced arguments in all git commands. #20: ExecuteCleanup reads .worktree-root before removing markers for consistent -C usage; ExecutePrune derives mainRoot from registry path. #27: RunGitForWorktree now uses timeout with kill. 7 new tests, all 3538 pass, gap_check green.

## Code Review

- Reviewed by: Emma
- Date: 2026-04-08 19:00
- Result: PASSED
- Notes: LGTM. All 4 fixes verified: #18 exit code isolation, #19 double-dash separators on all user-influenced git args, #20 consistent -C usage in cleanup/prune, #27 timeout+kill in RunGitForWorktree. 7 new tests, 3538/3538 pass, gap_check green.

Awaiting human approval.