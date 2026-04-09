---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-inquisition-coverage-denominator

Fixed inquisition coverage integration tests. The source filter in FileCoverageService (filtering tracked files via paths.source from dydo.json) was already committed in 82604ed but integration tests were not updated - they used default config with src/** patterns that didn't match the test files (Commands/Foo.cs, etc.). Added PatchSourcePaths helper to set correct source patterns after InitProjectAsync. All 36 FileCoverage tests pass. Coverage gap_check passes (135/135 modules). Note: 2 pre-existing CommandDocConsistencyTests failures exist (template/reference doc sync issues from prior commits, unrelated to this task).

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


## Review Summary

Fixed inquisition coverage integration tests. The source filter in FileCoverageService (filtering tracked files via paths.source from dydo.json) was already committed in 82604ed but integration tests were not updated - they used default config with src/** patterns that didn't match the test files (Commands/Foo.cs, etc.). Added PatchSourcePaths helper to set correct source patterns after InitProjectAsync. All 36 FileCoverage tests pass. Coverage gap_check passes (135/135 modules). Note: 2 pre-existing CommandDocConsistencyTests failures exist (template/reference doc sync issues from prior commits, unrelated to this task).

## Code Review

- Reviewed by: Charlie
- Date: 2026-04-07 21:24
- Result: PASSED
- Notes: LGTM. Clean, minimal fix — PatchSourcePaths helper correctly aligns test config source patterns with test file structure. All 3483 tests pass. gap_check green (135/135). Note: WorktreeCommandTests.Merge_ConflictDetected flaked once during review (StringBuilder race in CaptureAll) — passed on re-run.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
