---
area: general
name: fix-completion-provider
status: human-reviewed
created: 2026-04-09T11:35:31.2208853Z
assigned: Brian
updated: 2026-04-09T11:57:39.4593074Z
---

# Task: fix-completion-provider

Fixed CompletionProvider staleness (issue #49). Added 11 missing top-level commands (message, msg, wait, issue, inquisition, complete, template, roles, validate, watchdog, worktree, queue), 3 missing roles (orchestrator, inquisitor, judge), 7 new subcommand entries (issue, inquisition, roles, template, worktree, queue, watchdog), fixed 2 incomplete entries (agent tree, task compact), and added --subject option handler. Wrote 48 tests covering all arrays. No plan deviations. Note: 2 pre-existing failures in CommandDocConsistencyTests (AllCommands_AppearInHelpText, AboutQuickReference_IncludesAllCommands) — unrelated to this change, all commands listed as missing including original ones like check/fix.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed CompletionProvider staleness (issue #49). Added 11 missing top-level commands (message, msg, wait, issue, inquisition, complete, template, roles, validate, watchdog, worktree, queue), 3 missing roles (orchestrator, inquisitor, judge), 7 new subcommand entries (issue, inquisition, roles, template, worktree, queue, watchdog), fixed 2 incomplete entries (agent tree, task compact), and added --subject option handler. Wrote 48 tests covering all arrays. No plan deviations. Note: 2 pre-existing failures in CommandDocConsistencyTests (AllCommands_AppearInHelpText, AboutQuickReference_IncludesAllCommands) — unrelated to this change, all commands listed as missing including original ones like check/fix.

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-09 12:05
- Result: PASSED
- Notes: LGTM. CompletionProvider correctly updated with all missing commands, roles, subcommands, and --subject handler. HelpCommand cleanly extracted from Program.cs — eliminates duplication that was already out of sync in tests. 48 new tests in CompletionProviderTests, HelpCommandTests fixed to delegate to source of truth, smoke tests tightened to strict equality. InitCommand quoting fix is a good shell safety improvement. All 3599 tests pass, gap_check green (136/136). Clean, well-executed change.

Awaiting human approval.