---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-critical-bugs

(No description)

## Progress

- [ ] (Not started)

## Files Changed

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
C:\Users\User\Desktop\Projects\DynaDocs\Services\GuardLiftService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardLiftTests.cs — Modified


## Review Summary

Fixed three bugs: (1) Guard lift marker moved from dydo/_system/.local/guard-lifts/ to dydo/agents/<name>/.guard-lift.json so it resolves through worktree junctions — fixes cross-directory lift. Added UTC test confirming ExpiresAt round-trips as DateTimeKind.Utc. (2) WatchdogService.TryCloseWindow now resolves full wt.exe path via LocalAppData/Microsoft/WindowsApps before falling back to PATH — fixes 0x80070002 error in background processes.

## Code Review (2026-03-19 21:05)

- Reviewed by: Brian
- Result: FAILED
- Issues: Three issues: (1) 'task compact' missing from Program.cs help text — fails AllCommands_AppearInHelpText. (2) 'task compact' missing from about-dynadocs.md and about-dynadocs.template.md — fails AboutQuickReference_IncludesAllCommands. (3) TaskCompactHandler.cs at 75% line coverage, T1 requires >= 80%.

Requires rework.

## Approval

- Approved: 2026-03-25 17:24
