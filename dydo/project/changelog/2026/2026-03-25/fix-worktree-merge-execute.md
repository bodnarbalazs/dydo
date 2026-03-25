---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-worktree-merge-execute

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Models\NudgeConfig.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v11.txt — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-test-v9.txt — Created
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

Fixed all 3 bugs in WorktreeCommand.ExecuteMerge: (1) Replaced git checkout + merge with git -C <mainRoot> merge, reading .worktree-root marker to get the main repo path. This avoids the fatal 'already used by worktree' error when running from inside a worktree. (2) Eliminated the unchecked git checkout call entirely — git -C makes checkout unnecessary. (3) Markers were already preserved on conflict (early return before FinalizeMerge), confirmed correct. Also removed leftover merge-debug Console.WriteLine output. Updated SetupMergeAgent test helper to write .worktree-root so tests have a valid mainRoot. No plan deviations.

## Code Review (2026-03-22 19:29)

- Reviewed by: Charlie
- Result: FAILED
- Issues: FAIL: Missing baseBranch verification before merge. The old code ensured the main repo was on baseBranch before merging (git checkout baseBranch). The new code at line 407 assumes the main repo is already on baseBranch. If the main repo branch was changed (by another agent or manual intervention), the merge silently targets the wrong branch. Fix: Add RunProcess("git", $"-C \"{mainRoot}\" checkout {baseBranch}") before line 407. This is safe with git -C and is a no-op in the normal case. Everything else is correct: git -C approach solves the worktree checkout error, debug output removed, .worktree-root fallback with null check, markers cleanup includes .worktree-root, test helper updated. Tests pass (3011/3011). Coverage gap_check has 13 pre-existing failures — none in WorktreeCommand.cs, no regressions from this change.

Requires rework.

## Code Review (2026-03-22 19:48)

- Reviewed by: Charlie
- Result: FAILED
- Issues: FAIL: (1) Line 408: RunProcess for checkout does not check the exit code. If checkout fails, merge proceeds against the wrong branch — defeats the purpose of this fix. Must use RunProcessWithExitCode and return ExitCodes.ToolError on non-zero with a clear error message. (2) Lines 389-391: Comment says 'Using git -C avoids checkout entirely' but a checkout was added back. Comment is stale and misleading.

Requires rework.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-22 20:03
- Result: PASSED
- Notes: LGTM. Brian is correct — the checkout was unnecessary and re-introduced the original worktree bug. The git -C merge targets the main repo which is always on baseBranch by design. Comment is accurate, test regression guard is meaningful. 3011/3011 tests pass. 13 pre-existing coverage failures, none in WorktreeCommand.cs.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
