---
area: general
type: changelog
date: 2026-03-27
---

# Task: fix-zombie-worktrees

Implemented dydo worktree prune command and enhanced dydo clean --all. Changes: (1) New prune subcommand in WorktreeCommand.cs that scans for orphaned worktree directories, removes them (audit preservation, junction removal, git worktree remove, branch delete, directory delete), and cleans stale .worktree-hold/.merge-source markers. (2) WorkspaceCleaner.CleanAll now removes all 7 worktree marker types from agent workspaces. No plan deviations. 9 new tests (7 unit, 2 integration), all 3275 tests pass, coverage gate clear.

## Progress

- [ ] (Not started)

## Files Changed

C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-final-a.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WorkspaceCleaner.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorkspaceAndCleanTests.cs — Modified


## Review Summary

Implemented dydo worktree prune command and enhanced dydo clean --all. Changes: (1) New prune subcommand in WorktreeCommand.cs that scans for orphaned worktree directories, removes them (audit preservation, junction removal, git worktree remove, branch delete, directory delete), and cleans stale .worktree-hold/.merge-source markers. (2) WorkspaceCleaner.CleanAll now removes all 7 worktree marker types from agent workspaces. No plan deviations. 9 new tests (7 unit, 2 integration), all 3275 tests pass, coverage gate clear.

## Code Review (2026-03-26 23:11)

- Reviewed by: Brian
- Result: FAILED
- Issues: Unused import: using DynaDocs.Commands in Services/WorkspaceCleaner.cs:3. No type from Commands is referenced. Remove it.

Requires rework.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-26 23:23
- Result: PASSED
- Notes: LGTM. Unused import removed cleanly. Single-line diff, no side effects. All 3275 tests pass, coverage gate clear (131/131 modules).

Awaiting human approval.

## Approval

- Approved: 2026-03-27 13:14
