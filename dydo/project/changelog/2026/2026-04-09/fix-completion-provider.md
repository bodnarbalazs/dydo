---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-completion-provider

Fixed CompletionProvider staleness (issue #49). Added 11 missing top-level commands (message, msg, wait, issue, inquisition, complete, template, roles, validate, watchdog, worktree, queue), 3 missing roles (orchestrator, inquisitor, judge), 7 new subcommand entries (issue, inquisition, roles, template, worktree, queue, watchdog), fixed 2 incomplete entries (agent tree, task compact), and added --subject option handler. Wrote 48 tests covering all arrays. No plan deviations. Note: 2 pre-existing failures in CommandDocConsistencyTests (AllCommands_AppearInHelpText, AboutQuickReference_IncludesAllCommands) — unrelated to this change, all commands listed as missing including original ones like check/fix.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\GuardLiftServiceTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\CompletionProviderTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorktreeCreationLockTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\PathUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Utils\PathUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\GuardLiftService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\CompletionProvider.cs — Modified


## Review Summary

Fixed CompletionProvider staleness (issue #49). Added 11 missing top-level commands (message, msg, wait, issue, inquisition, complete, template, roles, validate, watchdog, worktree, queue), 3 missing roles (orchestrator, inquisitor, judge), 7 new subcommand entries (issue, inquisition, roles, template, worktree, queue, watchdog), fixed 2 incomplete entries (agent tree, task compact), and added --subject option handler. Wrote 48 tests covering all arrays. No plan deviations. Note: 2 pre-existing failures in CommandDocConsistencyTests (AllCommands_AppearInHelpText, AboutQuickReference_IncludesAllCommands) — unrelated to this change, all commands listed as missing including original ones like check/fix.

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-09 12:05
- Result: PASSED
- Notes: LGTM. CompletionProvider correctly updated with all missing commands, roles, subcommands, and --subject handler. HelpCommand cleanly extracted from Program.cs — eliminates duplication that was already out of sync in tests. 48 new tests in CompletionProviderTests, HelpCommandTests fixed to delegate to source of truth, smoke tests tightened to strict equality. InitCommand quoting fix is a good shell safety improvement. All 3599 tests pass, gap_check green (136/136). Clean, well-executed change.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
