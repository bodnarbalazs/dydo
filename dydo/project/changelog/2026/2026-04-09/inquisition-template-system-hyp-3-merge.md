---
area: general
type: changelog
date: 2026-04-09
---

# Task: inquisition-template-system-hyp-3-merge

Merged worktree/inquisition-template-system into master (fast-forward). Committed hypothesis test files from the template system inquisition (TemplateCommandTests, IncludeReanchorTests). Fixed a bug in IncludeReanchor.Reanchor where FindLineIndex matched the first occurrence of ambiguous anchors (e.g. '---' as frontmatter vs HR) — added FindLineIndexBefore to search backwards from the lower anchor, resolving hyp-3. All 3488 tests pass, gap_check green.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\GuardLiftServiceTests.cs — Created
C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-final5-a.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\CompletionProviderTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WorktreeCreationLockTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\PathUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\WorktreeCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Utils\PathUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\GuardLiftService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\CompletionProvider.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\run_tests.py — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Utils\FileReadRetryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\TerminalLauncherTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MacTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\TerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\IncludeReanchor.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WindowsTerminalLauncher.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardWorktreeAllowTests.cs — Modified


## Review Summary

Merged worktree/inquisition-template-system into master (fast-forward). Committed hypothesis test files from the template system inquisition (TemplateCommandTests, IncludeReanchorTests). Fixed a bug in IncludeReanchor.Reanchor where FindLineIndex matched the first occurrence of ambiguous anchors (e.g. '---' as frontmatter vs HR) — added FindLineIndexBefore to search backwards from the lower anchor, resolving hyp-3. All 3488 tests pass, gap_check green.

## Code Review (2026-04-08 14:42)

- Reviewed by: Jack
- Result: FAILED
- Issues: Services/IncludeReanchor.cs bug fix (FindLineIndexBefore + reordered anchor resolution) is NOT committed. Commit fee6405 has the tests but not the production code change. Test Reanchor_AnchorIsDashes_MatchesFrontmatterInsteadOfHorizontalRule will fail on clean checkout. Code quality is excellent — just needs the commit.

Requires rework.

## Code Review

- Reviewed by: Jack
- Date: 2026-04-08 15:27
- Result: PASSED
- Notes: Code is clean, tests pass (3497/3497). gap_check failed due to unrelated uncommitted IAgentRegistry changes — user approved release.

Awaiting human approval.

## Approval

- Approved: 2026-04-09 22:50
