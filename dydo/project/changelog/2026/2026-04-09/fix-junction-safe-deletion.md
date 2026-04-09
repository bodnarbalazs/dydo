---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-junction-safe-deletion

Implemented junction-safe deletion across three areas: (1) DispatchService.CreateGitWorktree removes junctions via WorktreeCommand.RemoveJunction before recursive delete of stale directories, (2) WindowsTerminalLauncher uses ReparsePoint attribute checks to distinguish junctions from regular directories — junctions get plain rmdir, non-junctions get Remove-Item -Recurse -Force, (3) bash WorktreeSetupScript uses test -L to detect symlinks before removal. Changed JunctionSubpaths visibility from private to internal. Updated 4 pre-existing tests that asserted old unsafe behavior. All 3551 tests pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Commands\HelpCommand.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardSecurityTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardWorktreeAllowBashWriteTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Program.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\HelpCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CommandSmokeTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\dydo-commands.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CommandDocConsistencyTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\OffLimitsService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\IBashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified


## Review Summary

Implemented junction-safe deletion across three areas: (1) DispatchService.CreateGitWorktree removes junctions via WorktreeCommand.RemoveJunction before recursive delete of stale directories, (2) WindowsTerminalLauncher uses ReparsePoint attribute checks to distinguish junctions from regular directories — junctions get plain rmdir, non-junctions get Remove-Item -Recurse -Force, (3) bash WorktreeSetupScript uses test -L to detect symlinks before removal. Changed JunctionSubpaths visibility from private to internal. Updated 4 pre-existing tests that asserted old unsafe behavior. All 3551 tests pass, gap_check green.

## Code Review

- Reviewed by: Brian
- Date: 2026-04-08 21:45
- Result: PASSED
- Notes: LGTM. Junction-safe deletion is correctly implemented across all three code paths (DispatchService C#, TerminalLauncher bash, WindowsTerminalLauncher PowerShell). EmitWorktreeAllowIfNeeded refactor is clean. All 3551 tests pass, gap_check green (135/135). Minor: stale TDD red-phase comments in two new test files, and one test loop that doesn't use its iteration variable.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
