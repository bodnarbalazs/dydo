---
area: general
name: fix-worktree-lifecycle
status: human-reviewed
created: 2026-03-20T20:39:51.3022630Z
assigned: Brian
updated: 2026-03-21T13:45:05.1469404Z
---

# Task: fix-worktree-lifecycle

(No description)

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed 6 worktree lifecycle bugs: (A) Junction setup - replaced cmd /c rmdir with [IO.Directory]::Delete() to handle non-empty dirs and avoid cmd.exe slash parsing. (B) Merge hang - redirect stdin + GIT_TERMINAL_PROMPT=0 to prevent git prompts. (C) Nested worktree branch suffix - use WorktreeIdToBranchSuffix in CopyWorktreeMetadataForMerger. (D) Dispatch marker task matching - relax to accept task-prefixed names. (E) Release escape hatch - skip requires-dispatch when dispatchedByRole is already a required role. (F) Tilde paths - add Read(~/**) to init-settings. All 2998 tests pass, 2 new tests added for fix E. No plan deviations.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-21 17:31
- Result: PASSED
- Notes: PASS. 6 surgical bug fixes, all correct. 2998 tests pass. 2 new tests for the release escape hatch (fix E) cover both positive and negative cases. gap_check shows 13 pre-existing failures — none are regressions from this changeset. RoleConstraintEvaluator.cs CRAP is borderline (30.0 vs threshold 30) due to raw CC, not coverage gaps (99.1% line coverage).

Awaiting human approval.