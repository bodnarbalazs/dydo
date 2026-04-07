---
area: general
type: changelog
date: 2026-04-05
---

# Task: fix-init-settings-timing

Fixed init-settings timing race: (1) ExecuteInitSettings now accepts optional worktreePath param, called from DispatchService after CreateGitWorktree so settings.local.json exists before terminal opens. (2) Added 1s sleep in terminal scripts between init-settings and claude as belt-and-suspenders. (3) Replaced error swallowing (2>/dev/null || true, empty catch) with visible warnings in both bash and PowerShell scripts. (4) Added warn-severity default nudge for inquisitor dispatches missing --new-window (nudge approach per user preference, not hardcoded in DispatchService). Updated 3 existing test assertions that relied on old error-swallowing patterns. All 3426 tests pass, all 132 coverage modules pass tier requirements.

## Progress

- [ ] (Not started)

## Files Changed

C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-final4-a.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\tier_registry.json — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\commit-msg.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\brief.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\gap_check.py — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\InboxServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\AgentListHandlerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CompleteCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\CompletionsCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\HelpCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\GraphDisplayHandlerTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\RolesResetCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\QueueCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WatchdogCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\ValidateCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\IntegrationTestBase.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConstraintEvaluationTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ConfigFactoryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\ConfigFactory.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\GuardCommandTests.cs — Modified


## Review Summary

Fixed init-settings timing race: (1) ExecuteInitSettings now accepts optional worktreePath param, called from DispatchService after CreateGitWorktree so settings.local.json exists before terminal opens. (2) Added 1s sleep in terminal scripts between init-settings and claude as belt-and-suspenders. (3) Replaced error swallowing (2>/dev/null || true, empty catch) with visible warnings in both bash and PowerShell scripts. (4) Added warn-severity default nudge for inquisitor dispatches missing --new-window (nudge approach per user preference, not hardcoded in DispatchService). Updated 3 existing test assertions that relied on old error-swallowing patterns. All 3426 tests pass, all 132 coverage modules pass tier requirements.

## Code Review (2026-04-02 21:48)

- Reviewed by: Dexter
- Result: FAILED
- Issues: Two issues in new test code: (1) Dead variable 'matchingNudge' in DefaultNudges_DoesNotMatchInquisitorDispatchWithNewWindow - computed but never asserted on. (2) xUnit2008 warnings at ConfigFactoryTests.cs lines 241 and 255 - use Assert.DoesNotMatch instead of Assert.False(regex.IsMatch()).

Requires rework.

## Code Review

- Reviewed by: Dexter
- Date: 2026-04-02 22:17
- Result: PASSED
- Notes: LGTM. Both review issues resolved cleanly: dead matchingNudge variable removed, Assert.DoesNotMatch used correctly to silence xUnit2008. 3426 tests pass, 132/132 coverage modules pass.

Awaiting human approval.

## Approval

- Approved: 2026-04-05 11:31
