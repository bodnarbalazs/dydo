---
area: general
type: changelog
date: 2026-04-05
---

# Task: fix-read-permissions-definitive

Worktree Read allow decision implemented. When the guard approves a Read in a worktree context (CWD contains dydo/_system/.local/worktrees/), it outputs Claude Code's explicit allow JSON to stdout, bypassing the unreliable permission pattern matching. Non-worktree and non-Read operations are completely untouched. Files changed: Commands/GuardCommand.cs (3 small additions), DynaDocs.Tests/Integration/GuardWorktreeAllowTests.cs (new, T2, 14 tests). All 14 new + 156 existing guard tests pass. Plan at dydo/agents/Emma/plan-fix-read-permissions-definitive.md.

## Progress

- [ ] (Not started)

## Files Changed

C:/Users/User/Desktop/Projects/DynaDocs/Commands/smoke-final4-b.txt — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\GuardWorktreeAllowTests.cs — Created
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\tier_registry.json — Created
C:\Users\User\Desktop\Projects\DynaDocs\Templates\about-dynadocs.template.md — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\DispatchService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Commands\GuardCommand.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\MessageFinder.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\QueueService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Utils\FileLockTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Utils\FileReadRetryTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Utils\FrontmatterParserTests.cs — Modified


## Review Summary

Worktree Read allow decision implemented. When the guard approves a Read in a worktree context (CWD contains dydo/_system/.local/worktrees/), it outputs Claude Code's explicit allow JSON to stdout, bypassing the unreliable permission pattern matching. Non-worktree and non-Read operations are completely untouched. Files changed: Commands/GuardCommand.cs (3 small additions), DynaDocs.Tests/Integration/GuardWorktreeAllowTests.cs (new, T2, 14 tests). All 14 new + 156 existing guard tests pass. Plan at dydo/agents/Emma/plan-fix-read-permissions-definitive.md.

## Code Review

- Reviewed by: Jack
- Date: 2026-04-03 15:28
- Result: PASSED
- Notes: LGTM. Code is clean, tests pass. All 14 new tests pass. Security verified: allow JSON only on approved read success path. gap_check: 134/135 pass — the 1 failure (DispatchService CRAP 30.2) is pre-existing from ca09bfd, not a regression.

Awaiting human approval.

## Approval

- Approved: 2026-04-05 11:31
