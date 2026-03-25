---
area: general
type: changelog
date: 2026-03-25
---

# Task: worktree-compat-fixes

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ProcessUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorktreeDispatchTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FileCoverageService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Utils\PathUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorkspaceCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\PathUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentRegistryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified


## Review Summary

Implemented three worktree compatibility fixes: (1) Added PathUtils.EnsureLocalDirExists and called it from GuardCommand and WatchdogService before writing to _system/.local/; (2) Fixed AgentRegistry.IsWorktreeStale to use _configService.GetDydoRoot instead of _basePath directly, plus call EnsureLocalDirExists; (3) Added PathUtils.IsInsideWorktree and guards in InitCommand, TemplateCommand, and WorkspaceCommand init subcommand. Updated 4 existing tests to match corrected IsWorktreeStale path resolution. Added tests for EnsureLocalDirExists and IsInsideWorktree.

## Code Review (2026-03-20 14:11)

- Reviewed by: Grace
- Result: FAILED
- Issues: FAIL — gap_check exits non-zero. GuardCommand CRAP 30.8>30, WatchdogService line 75.9%<80% + CRAP 32.1>30. Also: EnsureLocalDirExists inside IsWorktreeStale is a side-effect on a query method — remove it, Directory.Exists works fine without the parent existing.

Requires rework.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-20 19:07
- Result: PASSED
- Notes: LGTM. All 3 Grace review issues fixed correctly: GuardCommand CRAP resolved (internal helpers + tests), WatchdogService coverage met (StartProcessOverride/FindProcessesOverride hooks + 7 PollAndCleanup tests), EnsureLocalDirExists removed from IsWorktreeStale (uses GetDydoRoot). Additional scope (roles junction, Read(**) wildcard, worktree-refuse guards, merge docs) is clean and well-tested. 126/129 gap_check — 3 pre-existing failures untouched by this task. 2993/2995 tests pass — 2 pre-existing CommandDocConsistency failures.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
