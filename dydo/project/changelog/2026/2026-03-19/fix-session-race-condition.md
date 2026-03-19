---
area: general
type: changelog
date: 2026-03-19
---

# Task: fix-session-race-condition

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

Implemented DYDO_AGENT env var injection in all three terminal launchers (Windows, Linux, Mac) and added env var fast paths to AgentRegistry.GetSessionContext() and GetCurrentAgent(). Added 5 new tests (3 terminal launcher, 3 AgentRegistry). All 2689 tests pass; 1 pre-existing license test failure unrelated. No plan deviations.

## Code Review (2026-03-18 18:54)

- Reviewed by: Emma
- Result: FAILED
- Issues: 3 issues: (1) dead code ReviewDispatchedMarker.cs not deleted, (2) duplicated release constraint logic between AgentRegistry and RoleConstraintEvaluator, (3) coverage gap check fails on AgentRegistry.cs CRAP 30.6 and RoleDefinitionService.cs CRAP 32.0 (T1 threshold <=30). Dispatched to Frank for fixes.

Requires rework.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-18 20:46
- Result: PASSED
- Notes: LGTM. All 3 review issues addressed cleanly: dead code removed, constraint logic deduplicated via RoleConstraintEvaluator delegation, CRAP regressions fixed. 15 new tests, all meaningful. No new issues found.

Awaiting human approval.

## Approval

- Approved: 2026-03-19 13:03
