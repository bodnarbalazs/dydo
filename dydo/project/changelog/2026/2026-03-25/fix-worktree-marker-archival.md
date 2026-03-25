---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-worktree-marker-archival

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\NudgeConfig.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v8.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v4.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v11.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v9.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v10.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\ConfigFactory.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigFactoryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\GuardCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\LinuxTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\DydoConfig.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ValidationService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ValidationServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\BashCommandAnalyzerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\ReviewCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MessageService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\MessageIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchWaitIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleConstraintEvaluator.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleConstraintEvaluatorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WorkspaceArchiver.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorkspaceArchiveTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorkspaceArchiverTests.cs — Modified


## Review Summary

Added 7 worktree markers (.worktree, .worktree-path, .worktree-base, .worktree-root, .worktree-hold, .merge-source, .needs-merge) to SystemManagedEntries in both AgentRegistry.cs and WorkspaceArchiver.cs so they survive ArchiveWorkspace. Added a test in each test file (WorkspaceArchiveTests and WorkspaceArchiverTests) verifying all 7 markers are preserved. All 27 archive tests pass. No plan deviations.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-19 20:54
- Result: PASSED
- Notes: LGTM. All 7 worktree markers correctly added to SystemManagedEntries in both AgentRegistry.cs and WorkspaceArchiver.cs. Tests are meaningful and all 27 pass. gap_check has 15 pre-existing failures in unrelated modules — no regressions. Note: SystemManagedEntries duplication between the two files is pre-existing.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
