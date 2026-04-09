---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-captureall-flake-definitive

Created shared ConsoleCapture utility class with a static SemaphoreSlim that serializes all console redirect-execute-restore sequences. This eliminates the StringBuilder race condition where StringWriter.ToString() could race with concurrent writes from parallel xUnit test classes sharing the process-global Console.Out/Error. Replaced 14 duplicate capture helpers across the test project (WorktreeCommandTests, GraphDisplayHandlerTests, ValidateCommandTests, InboxServiceTests, CompletionsCommandTests, AgentListHandlerTests, WatchdogCommandTests, HelpCommandTests, CompleteCommandTests, RolesResetCommandTests, TerminalLauncherTests, ConstraintEvaluationTests, QueueCommandTests, IntegrationTestBase). All 3511 tests pass across two consecutive runs with zero flakes. Coverage gap check: 135/135 modules passing.

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


## Review Summary

Created shared ConsoleCapture utility class with a static SemaphoreSlim that serializes all console redirect-execute-restore sequences. This eliminates the StringBuilder race condition where StringWriter.ToString() could race with concurrent writes from parallel xUnit test classes sharing the process-global Console.Out/Error. Replaced 14 duplicate capture helpers across the test project (WorktreeCommandTests, GraphDisplayHandlerTests, ValidateCommandTests, InboxServiceTests, CompletionsCommandTests, AgentListHandlerTests, WatchdogCommandTests, HelpCommandTests, CompleteCommandTests, RolesResetCommandTests, TerminalLauncherTests, ConstraintEvaluationTests, QueueCommandTests, IntegrationTestBase). All 3511 tests pass across two consecutive runs with zero flakes. Coverage gap check: 135/135 modules passing.

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-08 16:05
- Result: PASSED
- Notes: LGTM. Semaphore serialization correctly fixes the process-global Console race. TextWriter.Synchronized removal justified (redundant under semaphore). Net -78 lines, 14 duplicates consolidated. All 3511 tests pass, 135/135 coverage modules green. No orphaned code, no dead methods.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
