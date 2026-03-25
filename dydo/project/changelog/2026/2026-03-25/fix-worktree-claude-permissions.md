---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-worktree-claude-permissions

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\TasksConfig.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TaskCompactHandler.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ProcessUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorktreeDispatchTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FileCoverageService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-code-writer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-reviewer.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-orchestrator.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Utils\PathUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorkspaceCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\PathUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentRegistryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\DydoConfig.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TaskCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TaskApproveHandler.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\TaskTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\GuardLiftService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardLiftTests.cs — Modified


## Review Summary

Implemented init-settings subcommand on 'dydo worktree'. When dispatching to worktrees, settings.local.json is now copied AND a Read permission for the main repo path is injected, fixing the Claude Code permission prompt caused by junction-resolved paths. Changed WorktreeCommand.cs (new subcommand), TerminalLauncher.cs (bash), WindowsTerminalLauncher.cs (Windows). Updated 7 existing tests and added 5 new ones. No plan deviations. Pre-existing gap_check failures (15 modules) are unrelated to these changes.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-19 20:35
- Result: PASSED
- Notes: LGTM. Clean implementation of init-settings subcommand. Correctly copies settings.local.json and injects Read permission for main repo path. Logic handles edge cases (missing source, invalid JSON, idempotency). Tests are thorough. No coverage regressions. Shell scripts simplified on all platforms.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
