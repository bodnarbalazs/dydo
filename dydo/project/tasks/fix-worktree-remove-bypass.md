---
area: general
name: fix-worktree-remove-bypass
status: human-reviewed
created: 2026-04-02T19:05:40.9000147Z
assigned: Brian
---

# Task: fix-worktree-remove-bypass

Implemented all 4 fixes from Adele's brief: (1) Updated GitWorktreeAdd/RemoveRegex to use git\b[^;|&]*\bworktree pattern, catching git -C path worktree bypass. (2) Added two defense-in-depth nudges to DefaultNudges — one for git worktree add/remove, one for rm on worktree dirs. (3) Moved dangerous pattern check before needless-cd coaching in HandleBashCommand so security reason takes priority. (4) rm worktree directory nudge included in FIX 2. Tests written first (11 new cases), all 3402 tests pass, gap_check passes.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented all 4 fixes from Adele's brief: (1) Updated GitWorktreeAdd/RemoveRegex to use git\b[^;|&]*\bworktree pattern, catching git -C path worktree bypass. (2) Added two defense-in-depth nudges to DefaultNudges — one for git worktree add/remove, one for rm on worktree dirs. (3) Moved dangerous pattern check before needless-cd coaching in HandleBashCommand so security reason takes priority. (4) rm worktree directory nudge included in FIX 2. Tests written first (11 new cases), all 3402 tests pass, gap_check passes.

## Code Review (2026-04-02 19:29)

- Reviewed by: Charlie
- Result: FAILED
- Issues: Two issues: (1) CheckDangerousPatterns is now called twice — original at GuardCommand.cs:651 inside AnalyzeAndCheckBashOperations is dead code since the new check at line 432 catches everything first. Remove lines 651-663. (2) Regex git\b should be \bgit\b in BashCommandAnalyzer.cs:268, :271, and ConfigFactory.cs:43 — missing leading word boundary lets git match inside other words like digit.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-02 19:44
- Result: PASSED
- Notes: LGTM. Both prior issues fixed cleanly: dead code removed from AnalyzeAndCheckBashOperations, leading word boundary added to all three regex patterns. 4 new tests are meaningful. gap_check passes. 1 pre-existing unrelated test failure (InboxServiceTests.PrintInboxItem_TaskItem_IncludesFilePath).

Awaiting human approval.