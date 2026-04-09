---
area: general
type: changelog
date: 2026-04-09
---

# Task: fix-mac-iterm-detection

Fixed macOS iTerm2 detection for agent dispatch. Three changes: (1) Added GetRunningTerminal() to ITerminalDetector — uses TERM_PROGRAM env var to detect the actual running terminal instead of guessing filesystem paths. (2) Updated MacTerminalLauncher.Launch() to use iTerm2 for both tab and window modes when running in iTerm2. (3) Added GetITermWindowScript() for new-window dispatches in iTerm2. Filesystem fallback (IsAvailable) preserved for backward compat when TERM_PROGRAM is not set. Five new tests cover the new behavior.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Commands\HelpCommand.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardSecurityTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardWorktreeAllowBashWriteTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Program.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\HelpCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CommandSmokeTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\dydo-commands.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\InitCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CommandDocConsistencyTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\OffLimitsService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\IBashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorktreeCreationLockTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\IncludeReanchorTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\TemplateCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TemplateUpdateTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FolderScaffolder.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\IncludeReanchor.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ITerminalDetector.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-judge.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Templates\mode-orchestrator.template.md — Modified


## Review Summary

Fixed macOS iTerm2 detection for agent dispatch. Three changes: (1) Added GetRunningTerminal() to ITerminalDetector — uses TERM_PROGRAM env var to detect the actual running terminal instead of guessing filesystem paths. (2) Updated MacTerminalLauncher.Launch() to use iTerm2 for both tab and window modes when running in iTerm2. (3) Added GetITermWindowScript() for new-window dispatches in iTerm2. Filesystem fallback (IsAvailable) preserved for backward compat when TERM_PROGRAM is not set. Five new tests cover the new behavior.

## Code Review

- Reviewed by: Emma
- Date: 2026-04-07 12:06
- Result: PASSED
- Notes: LGTM. Logic correctly prioritizes TERM_PROGRAM env var over filesystem detection, with backward-compat fallback. GetITermWindowScript follows the same pattern as GetITermTabScript. 4 meaningful tests cover the key scenarios. All 3464 tests pass, gap_check green (135/135).

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:49
