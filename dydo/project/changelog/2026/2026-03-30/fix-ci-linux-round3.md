---
area: general
type: changelog
date: 2026-03-30
---

# Task: fix-ci-linux-round3

Fixed the single CI failure on Linux: WatchdogServiceTests.EnsureRunning_ConcurrentCalls_StartsOnlyOneWatchdog. Root cause: on Linux, File.Delete succeeds on files held open by another thread (Linux uses advisory file locking vs Windows mandatory locking). Multiple threads could each delete-and-recreate the PID file via FileMode.CreateNew. Fix: return false when ReadAllText fails (file locked by another thread handling startup), preventing the delete-then-create race. One line changed in WatchdogService.cs line 53. All 3278+ tests pass, coverage gate 131/131.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Integration\DispatchQueueTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\run_tests.py — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\coverage\gap_check.py — Modified
C:\Users\User\Desktop\Projects\DynaDocs\dydo.json — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\QueueService.cs — Modified


## Review Summary

Fixed the single CI failure on Linux: WatchdogServiceTests.EnsureRunning_ConcurrentCalls_StartsOnlyOneWatchdog. Root cause: on Linux, File.Delete succeeds on files held open by another thread (Linux uses advisory file locking vs Windows mandatory locking). Multiple threads could each delete-and-recreate the PID file via FileMode.CreateNew. Fix: return false when ReadAllText fails (file locked by another thread handling startup), preventing the delete-then-create race. One line changed in WatchdogService.cs line 53. All 3278+ tests pass, coverage gate 131/131.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-27 17:48
- Result: PASSED
- Notes: LGTM. One-line fix is correct: returning false on ReadAllText failure prevents the delete-then-create race on Linux. Test coverage via EnsureRunning_ConcurrentCalls_StartsOnlyOneWatchdog validates the fix. Coverage gate 131/131. Out-of-scope: PollAndCleanup_ProcessesRunning_ShellProcess_SkipsKill is a pre-existing flaky test (line 545, unrelated to this change).

Awaiting human approval.

## Approval

- Approved: 2026-03-30 17:16
