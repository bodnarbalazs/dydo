---
area: general
name: fix-inquisition-state-isolation
status: human-reviewed
created: 2026-03-30T17:42:04.3587070Z
assigned: Charlie
updated: 2026-03-30T19:16:14.3329198Z
---

# Task: fix-inquisition-state-isolation

Implemented fix for inquisition state isolation. Changes: (1) TerminalLauncher.cs + WindowsTerminalLauncher.cs: Added junctions for dydo/project/issues and dydo/project/inquisitions in worktree setup, mirroring existing agents/roles pattern. Ensures mkdir -p on targets for first-use case. (2) WorktreeCommand.cs: Added RemoveJunction calls for new junctions in all 3 cleanup sites (cleanup, merge-finalize, prune). (3) IssueCreateHandler.cs: Wrapped ScanMaxId+WriteAllText in FileStream exclusive lock with 5x200ms retry. (4) mode-judge.template.md: Added Worktree Cleanup section before Complete. (5) mode-inquisitor.template.md: Added note that reports go to main via junction. (6) Tests: Updated 6 tests that broadly asserted no mkdir-p (intent was no worktree dir creation, not no mkdir at all). Added 2 new tests for shared-state junctions, 1 for Windows junction assertions, 2 for lock file behavior. Plan at agents/Charlie/plan-fix-inquisition-state-isolation.md

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented fix for inquisition state isolation. Changes: (1) TerminalLauncher.cs + WindowsTerminalLauncher.cs: Added junctions for dydo/project/issues and dydo/project/inquisitions in worktree setup, mirroring existing agents/roles pattern. Ensures mkdir -p on targets for first-use case. (2) WorktreeCommand.cs: Added RemoveJunction calls for new junctions in all 3 cleanup sites (cleanup, merge-finalize, prune). (3) IssueCreateHandler.cs: Wrapped ScanMaxId+WriteAllText in FileStream exclusive lock with 5x200ms retry. (4) mode-judge.template.md: Added Worktree Cleanup section before Complete. (5) mode-inquisitor.template.md: Added note that reports go to main via junction. (6) Tests: Updated 6 tests that broadly asserted no mkdir-p (intent was no worktree dir creation, not no mkdir at all). Added 2 new tests for shared-state junctions, 1 for Windows junction assertions, 2 for lock file behavior. Plan at agents/Charlie/plan-fix-inquisition-state-isolation.md

## Code Review

- Reviewed by: Henry
- Date: 2026-03-30 19:21
- Result: PASSED
- Notes: LGTM. Test follows the exact pattern of existing Cleanup_LastAgent_RemovesRolesJunction. Covers both Windows rmdir and non-Windows directory removal for issues and inquisitions junctions. All 3348 tests pass, gap_check 396/396 modules clear.

Awaiting human approval.