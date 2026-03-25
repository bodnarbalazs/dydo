---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-worktree-remaining

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\NudgeConfig.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\Services\ConfigFactory.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigFactoryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\TemplateCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\GuardCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\LinuxTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Models\DydoConfig.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Serialization\DydoJsonContext.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ValidationService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\AgentRegistry.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCompatTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ValidationServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\BashCommandAnalyzer.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\BashCommandAnalyzerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified


## Review Summary

Fixed three worktree issues: (1) Added init-settings to inherited worktree launch path in all three platform launchers (Windows/Linux/Mac), (2) Fixed FinalizeMerge cleanup order - now uses git -C for worktree remove, adds worktree prune, and makes cleanup best-effort with warnings, (3) Added CatHeredocRegex to BashCommandAnalyzer to strip heredoc blocks before analysis, preventing false command-substitution warnings and write-on-EOF blocks. All 3013+ tests pass. No plan deviations.

## Code Review (2026-03-23 00:09)

- Reviewed by: Charlie
- Result: FAILED
- Issues: FAIL: LinuxTerminalLauncher.cs lines 32 and 68 build init-settings command inline without single-quote escaping, unlike Mac which uses the shared WorktreeInitSettingsScript() helper. Fix: replace inline construction with TerminalLauncher.WorktreeInitSettingsScript(mainProjectRoot) in both locations. Everything else is clean — FinalizeMerge git -C fix, worktree prune sequencing, CatHeredocRegex, and all tests are correct.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-23 00:19
- Result: PASSED
- Notes: LGTM. Both inline init-settings constructions in LinuxTerminalLauncher.cs (lines 32 and 67) correctly replaced with TerminalLauncher.WorktreeInitSettingsScript(mainProjectRoot), matching MacTerminalLauncher. Single-quote escaping is now handled by the shared helper. 3020/3021 tests pass (1 flaky dispose in unrelated PathUtilsDiscoveryTests). gap_check has 13 pre-existing failures in unrelated modules — no regressions from this change.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
