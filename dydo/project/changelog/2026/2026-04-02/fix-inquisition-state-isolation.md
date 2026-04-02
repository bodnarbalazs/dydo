---
area: general
type: changelog
date: 2026-04-02
---

# Task: fix-inquisition-state-isolation

Implemented fix for inquisition state isolation. Changes: (1) TerminalLauncher.cs + WindowsTerminalLauncher.cs: Added junctions for dydo/project/issues and dydo/project/inquisitions in worktree setup, mirroring existing agents/roles pattern. Ensures mkdir -p on targets for first-use case. (2) WorktreeCommand.cs: Added RemoveJunction calls for new junctions in all 3 cleanup sites (cleanup, merge-finalize, prune). (3) IssueCreateHandler.cs: Wrapped ScanMaxId+WriteAllText in FileStream exclusive lock with 5x200ms retry. (4) mode-judge.template.md: Added Worktree Cleanup section before Complete. (5) mode-inquisitor.template.md: Added note that reports go to main via junction. (6) Tests: Updated 6 tests that broadly asserted no mkdir-p (intent was no worktree dir creation, not no mkdir at all). Added 2 new tests for shared-state junctions, 1 for Windows junction assertions, 2 for lock file behavior. Plan at agents/Charlie/plan-fix-inquisition-state-isolation.md

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Templates\about-dynadocs.template.md — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleDefinitionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CommandDocConsistencyTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\README.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\npm\README.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Utils\GlobMatcher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\OffLimitsService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Program.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\AgentCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\CompletionProvider.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\dydo-commands.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CompleteCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\HelpCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\EndToEnd\CliEndToEndTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\IssueCreateHandler.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-judge.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-inquisitor.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\IssueTests.cs — Modified


## Review Summary

Implemented fix for inquisition state isolation. Changes: (1) TerminalLauncher.cs + WindowsTerminalLauncher.cs: Added junctions for dydo/project/issues and dydo/project/inquisitions in worktree setup, mirroring existing agents/roles pattern. Ensures mkdir -p on targets for first-use case. (2) WorktreeCommand.cs: Added RemoveJunction calls for new junctions in all 3 cleanup sites (cleanup, merge-finalize, prune). (3) IssueCreateHandler.cs: Wrapped ScanMaxId+WriteAllText in FileStream exclusive lock with 5x200ms retry. (4) mode-judge.template.md: Added Worktree Cleanup section before Complete. (5) mode-inquisitor.template.md: Added note that reports go to main via junction. (6) Tests: Updated 6 tests that broadly asserted no mkdir-p (intent was no worktree dir creation, not no mkdir at all). Added 2 new tests for shared-state junctions, 1 for Windows junction assertions, 2 for lock file behavior. Plan at agents/Charlie/plan-fix-inquisition-state-isolation.md

## Code Review

- Reviewed by: Henry
- Date: 2026-03-30 19:21
- Result: PASSED
- Notes: LGTM. Test follows the exact pattern of existing Cleanup_LastAgent_RemovesRolesJunction. Covers both Windows rmdir and non-Windows directory removal for issues and inquisitions junctions. All 3348 tests pass, gap_check 396/396 modules clear.

Awaiting human approval.

## Approval

- Approved: 2026-04-02 18:55
