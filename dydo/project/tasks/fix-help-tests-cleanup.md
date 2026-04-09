---
area: general
name: fix-help-tests-cleanup
status: human-reviewed
created: 2026-04-09T11:35:29.0287472Z
assigned: Charlie
updated: 2026-04-09T12:05:57.8433376Z
---

# Task: fix-help-tests-cleanup

Implemented all 5 items from the brief: (1) Extracted help text from inline Program.cs lambda into HelpCommand.cs with public PrintHelp() method, rewired HelpCommandTests to use the real implementation instead of a stale copy, added assertions for all new sections (Role, Validation, Issue, Inquisition, Queue, Worktree Commands, agent tree, task compact). (2) Added WorktreeCommand.Create() to RootCommand_CanBeBuilt, added HelpCommand.Create() to both smoke tests, removed CleanCommand (not in Program.cs), changed assertion to exact count (28). (3) Deleted all 12 smoke-*.txt artifacts from Commands/. (4) Moved validate command to its own Validation Commands section in dydo-commands template, added dydo/project/inquisitions/** to judge permissions table. (5) Quoted humanName in both init and join shell examples in InitCommand.cs. Also fixed CommandDocConsistencyTests to read help text from HelpCommand.cs and excluded help from quick-reference check (it was never there before, just became discoverable). All tests pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Implemented all 5 items from the brief: (1) Extracted help text from inline Program.cs lambda into HelpCommand.cs with public PrintHelp() method, rewired HelpCommandTests to use the real implementation instead of a stale copy, added assertions for all new sections (Role, Validation, Issue, Inquisition, Queue, Worktree Commands, agent tree, task compact). (2) Added WorktreeCommand.Create() to RootCommand_CanBeBuilt, added HelpCommand.Create() to both smoke tests, removed CleanCommand (not in Program.cs), changed assertion to exact count (28). (3) Deleted all 12 smoke-*.txt artifacts from Commands/. (4) Moved validate command to its own Validation Commands section in dydo-commands template, added dydo/project/inquisitions/** to judge permissions table. (5) Quoted humanName in both init and join shell examples in InitCommand.cs. Also fixed CommandDocConsistencyTests to read help text from HelpCommand.cs and excluded help from quick-reference check (it was never there before, just became discoverable). All tests pass, gap_check green.

## Code Review

- Reviewed by: Brian
- Date: 2026-04-09 12:12
- Result: PASSED
- Notes: LGTM. Clean extraction of help text to HelpCommand.cs, tests rewired to real implementation, smoke tests correctly updated (exact count, WorktreeCommand added, CleanCommand correctly scoped). Template fixes match role JSON. InitCommand quoting is a good defensive fix. All 3599 tests pass, gap_check green (136/136).

Awaiting human approval.