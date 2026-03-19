---
area: general
type: changelog
date: 2026-03-19
---

# Task: inquisition-file-coverage

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardIntegrationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\InboxItem.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\AgentState.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\RoleConstraint.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\InboxMetadataReader.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentStateStore.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleConstraintEvaluator.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\RoleDefinitionService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleConstraintEvaluatorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\RoleDefinitionServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\AgentStateStoreTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\LinuxTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorktreeDispatchTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\BashCommandAnalyzerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchCommandTests.cs — Modified


## Review Summary

Implemented file-level inquisition coverage heatmap. Added --files, --path, --gaps-only, --since, --summary options to dydo inquisition coverage. Created FileCoverageService with session grouping, per-file scoring (cap 3 per inquisition group), git-based decay, and Markdown rendering. Output auto-routes to agent workspace or project path. 20 unit tests + 11 integration tests, all passing. Coverage gap check passes. No plan deviations.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-18 15:08
- Result: PASSED
- Notes: LGTM. Both fixes correct: (1) Dispose re-implements IDisposable on derived class with new+base.Dispose(), prevents test pollution. (2) StaleCount now accurately counts only decay-affected files. 31/31 tests pass, 126/126 coverage modules pass. Code is clean, no slop.

Awaiting human approval.

## Approval

- Approved: 2026-03-19 13:03
