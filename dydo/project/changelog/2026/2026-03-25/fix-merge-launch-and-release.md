---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-merge-launch-and-release

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
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified


## Review Summary

Fixed Bug 1: merge dispatch path in DispatchService.WriteAndLaunch now sets mainProjectRoot = GetWorktreeRoot(registry, senderName) ?? PathUtils.FindProjectRoot(), and LaunchTerminalIfNeeded uses mainProjectRoot as fallback for working directory. This ensures the merge code-writer launches in the main repo, not the worktree. Bug 2 (release constraint bypass): verified the escape hatch in RoleConstraintEvaluator.CanRelease lines 121-123 is correctly implemented and dispatchedByRole is properly populated via inbox from_role field. No code change needed. One pre-existing test failure: Cleanup_RemovesMergeSourceMarker expects ExecuteCleanup to delete .merge-source but commit d3ea848 intentionally changed that behavior.

## Code Review (2026-03-22 22:37)

- Reviewed by: Dexter
- Result: FAILED
- Issues: Code fix is correct and surgical (2 lines), follows existing patterns, no security issues. FAIL for one issue: Stale test Cleanup_RemovesMergeSourceMarker (WorktreeDispatchTests.cs:405) asserts .merge-source is deleted by ExecuteCleanup, but commit d3ea848 intentionally changed ExecuteCleanup to use RemoveWorktreeMarkers (preserving .merge-source). The assertion should be Assert.True, not Assert.False. The correct unit test already exists (RemoveWorktreeMarkers_PreservesMergeSource in WorktreeCommandTests.cs), but this integration test was missed in d3ea848. Also note: leftover debug output on DispatchService.cs:160 ([dispatch-debug]) from commit 35383f9 should be removed.

Requires rework.

## Code Review

- Reviewed by: Dexter
- Date: 2026-03-22 23:04
- Result: PASSED
- Notes: LGTM. Both review issues resolved cleanly: (1) Test renamed to Cleanup_PreservesMergeSourceMarker with assertion flipped to Assert.True, matching d3ea848 behavior. (2) All 7 [dispatch-debug] Console.WriteLine lines removed. Merge dispatch path correctly sets mainProjectRoot. 3012/3013 tests pass (1 pre-existing flaky test: Merge_OutputsProgressMessage passes when run individually). gap_check: 13 pre-existing failures, zero regressions.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
