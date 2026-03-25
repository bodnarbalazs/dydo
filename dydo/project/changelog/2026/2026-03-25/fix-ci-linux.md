---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-ci-linux

Fixed 9 Linux CI test failures across 5 files. 4 ProcessUtilsTests needed Windows-only guards (methods named OnWindows but ran on all platforms). 2 WorktreeCommandTests needed platform-conditional assertions (cmd+rmdir vs File.Delete). 1 WatchdogServiceTests had hardcoded cmd.exe. 1 WorktreeDispatchTests used absolute path /main/project causing permission errors on Linux. 1 FileCoverageService production bug: NormalizeEventPath didnt normalize backslashes before GetRelativePath, breaking cross-platform audit path matching. All 3187 tests pass, coverage gate clear.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ProcessUtilsTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Commands\WorktreeCommandTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\WorktreeDispatchTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\FileCoverageService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified


## Review Summary

Fixed 9 Linux CI test failures across 5 files. 4 ProcessUtilsTests needed Windows-only guards (methods named OnWindows but ran on all platforms). 2 WorktreeCommandTests needed platform-conditional assertions (cmd+rmdir vs File.Delete). 1 WatchdogServiceTests had hardcoded cmd.exe. 1 WorktreeDispatchTests used absolute path /main/project causing permission errors on Linux. 1 FileCoverageService production bug: NormalizeEventPath didnt normalize backslashes before GetRelativePath, breaking cross-platform audit path matching. All 3187 tests pass, coverage gate clear.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-25 17:22
- Result: PASSED
- Notes: LGTM. All 9 Linux CI fixes are correct and minimal. Production bug in NormalizeEventPath is a real fix with an existing test covering it. Test-only changes use correct platform guards and conditionals. 3187 tests pass, coverage gate clear (129/129 modules).

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:24
