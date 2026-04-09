---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-workflow-roles

Fixed 3 workflow/role issues: (1) Added 'inquisitor' to reviewer dispatch-restriction RequiredRoles in RoleDefinitionService.cs, breaking the circular deadlock where reviewers dispatched by inquisitors couldn't dispatch code-writers for merges. (2) Judge role source already has correct writable paths (issues + inquisitions); on-disk judge.role.json is stale — human must run 'dydo roles reset' to regenerate. (3) Added GenerateRoleFilesIfMissing to InitCommand.cs ScaffoldProject and PerformJoin, so 'dydo init' auto-generates role files when _system/roles/ is empty. All 7 new tests pass. 2 pre-existing failures in WorktreeCommandTests (Merge_CleanMerge_AutoFinalizes, Cleanup_WorktreeHold_CountsAsReference) are unrelated to these changes. gap_check passes (exit 0, all 135 modules pass).

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\ConsoleCapture.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\GraphDisplayHandlerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\ValidateCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\InboxServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CompletionsCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\AgentListHandlerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WatchdogCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\HelpCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CompleteCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\RolesResetCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConstraintEvaluationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\QueueCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\IntegrationTestBase.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-co-thinker.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\FileCoverageServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FileCoverageService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\FileCoverageTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleDefinitionServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleConstraintEvaluatorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\InitCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleDefinitionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified


## Review Summary

Fixed 3 workflow/role issues: (1) Added 'inquisitor' to reviewer dispatch-restriction RequiredRoles in RoleDefinitionService.cs, breaking the circular deadlock where reviewers dispatched by inquisitors couldn't dispatch code-writers for merges. (2) Judge role source already has correct writable paths (issues + inquisitions); on-disk judge.role.json is stale — human must run 'dydo roles reset' to regenerate. (3) Added GenerateRoleFilesIfMissing to InitCommand.cs ScaffoldProject and PerformJoin, so 'dydo init' auto-generates role files when _system/roles/ is empty. All 7 new tests pass. 2 pre-existing failures in WorktreeCommandTests (Merge_CleanMerge_AutoFinalizes, Cleanup_WorktreeHold_CountsAsReference) are unrelated to these changes. gap_check passes (exit 0, all 135 modules pass).

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-07 17:56
- Result: PASSED
- Notes: LGTM. All 3 fixes are clean and correct: (1) inquisitor added to reviewer dispatch RequiredRoles — breaks the deadlock, (2) judge writable paths verified in source with round-trip test, (3) GenerateRoleFilesIfMissing is minimal and well-placed. 7 new tests are meaningful. 3477 tests pass, gap_check 135/135 green.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
