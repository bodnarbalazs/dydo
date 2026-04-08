---
area: general
name: fix-captureall-flake-definitive
status: human-reviewed
created: 2026-04-08T15:37:01.3312021Z
assigned: Emma
updated: 2026-04-08T15:56:25.2866528Z
---

# Task: fix-captureall-flake-definitive

Created shared ConsoleCapture utility class with a static SemaphoreSlim that serializes all console redirect-execute-restore sequences. This eliminates the StringBuilder race condition where StringWriter.ToString() could race with concurrent writes from parallel xUnit test classes sharing the process-global Console.Out/Error. Replaced 14 duplicate capture helpers across the test project (WorktreeCommandTests, GraphDisplayHandlerTests, ValidateCommandTests, InboxServiceTests, CompletionsCommandTests, AgentListHandlerTests, WatchdogCommandTests, HelpCommandTests, CompleteCommandTests, RolesResetCommandTests, TerminalLauncherTests, ConstraintEvaluationTests, QueueCommandTests, IntegrationTestBase). All 3511 tests pass across two consecutive runs with zero flakes. Coverage gap check: 135/135 modules passing.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Created shared ConsoleCapture utility class with a static SemaphoreSlim that serializes all console redirect-execute-restore sequences. This eliminates the StringBuilder race condition where StringWriter.ToString() could race with concurrent writes from parallel xUnit test classes sharing the process-global Console.Out/Error. Replaced 14 duplicate capture helpers across the test project (WorktreeCommandTests, GraphDisplayHandlerTests, ValidateCommandTests, InboxServiceTests, CompletionsCommandTests, AgentListHandlerTests, WatchdogCommandTests, HelpCommandTests, CompleteCommandTests, RolesResetCommandTests, TerminalLauncherTests, ConstraintEvaluationTests, QueueCommandTests, IntegrationTestBase). All 3511 tests pass across two consecutive runs with zero flakes. Coverage gap check: 135/135 modules passing.

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-08 16:05
- Result: PASSED
- Notes: LGTM. Semaphore serialization correctly fixes the process-global Console race. TextWriter.Synchronized removal justified (redundant under semaphore). Net -78 lines, 14 duplicates consolidated. All 3511 tests pass, 135/135 coverage modules green. No orphaned code, no dead methods.

Awaiting human approval.