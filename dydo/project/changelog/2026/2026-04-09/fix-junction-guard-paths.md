---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-junction-guard-paths

Implemented 3 worktree fixes: (1) Replaced hardcoded junction subpath list + Directory.Delete(recursive) with recursive DeleteDirectoryJunctionSafe that detects reparse points via File.GetAttributes at any depth. (2) Added fallback in NormalizeWorktreePath for when File.Exists can't verify worktree root — uses first segment after marker as worktree ID. (3) Added Directory.CreateDirectory before writing .guard-lift.json in GuardLiftService.Lift. All tests written first; 3649 tests pass, gap_check passes. No plan deviations.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\GuardLiftServiceTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorktreeCreationLockTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\PathUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Utils\PathUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\GuardLiftService.cs — Modified


## Review Summary

Implemented 3 worktree fixes: (1) Replaced hardcoded junction subpath list + Directory.Delete(recursive) with recursive DeleteDirectoryJunctionSafe that detects reparse points via File.GetAttributes at any depth. (2) Added fallback in NormalizeWorktreePath for when File.Exists can't verify worktree root — uses first segment after marker as worktree ID. (3) Added Directory.CreateDirectory before writing .guard-lift.json in GuardLiftService.Lift. All tests written first; 3649 tests pass, gap_check passes. No plan deviations.

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-09 22:27
- Result: PASSED
- Notes: LGTM. All 3 fixes are clean: DeleteDirectoryJunctionSafe correctly detects reparse points at any depth (15 lines), NormalizeWorktreePath fallback handles relative paths where File.Exists fails, GuardLiftService.Lift creates directory before writing marker. Tests are meaningful and cover edge cases. 3649 tests pass, gap_check 136/136 green. Out-of-scope: TeardownWorktree (WorktreeCommand.cs:406) still uses hardcoded JunctionSubpaths + RemoveZombieDirectory with Directory.Delete(recursive:true) — same vulnerability pattern, should be addressed in follow-up.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
