---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-merge-marker-race

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\NudgeConfig.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v11.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v10.txt — Created
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
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified


## Review Summary

Fixed the merge marker race condition. Two changes: (1) Split RemoveMarkers into RemoveWorktreeMarkers (excludes .merge-source, used by ExecuteCleanup) and RemoveAllMarkers (includes .merge-source, used by FinalizeMerge). This prevents worktree cleanup from deleting .merge-source written for the next dispatch. (2) Swapped order in FinalizeMerge: worktree removal now happens before branch deletion so git branch -D no longer fails. Added two new tests: RemoveWorktreeMarkers_PreservesMergeSource and RemoveAllMarkers_DeletesMergeSource. No plan deviations — implemented Option A as specified.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-22 21:00
- Result: PASSED
- Notes: LGTM. Clean split of RemoveMarkers into RemoveWorktreeMarkers/RemoveAllMarkers correctly prevents the race. FinalizeMerge ordering fix (worktree removal before branch deletion) is correct. Two new tests cover both sides of the split. No coverage regressions. No slop.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
