---
area: general
name: fix-ci-linux-round3
status: human-reviewed
created: 2026-03-27T13:22:33.7208528Z
assigned: Brian
updated: 2026-03-27T14:15:25.7764857Z
---

# Task: fix-ci-linux-round3

Fixed the single CI failure on Linux: WatchdogServiceTests.EnsureRunning_ConcurrentCalls_StartsOnlyOneWatchdog. Root cause: on Linux, File.Delete succeeds on files held open by another thread (Linux uses advisory file locking vs Windows mandatory locking). Multiple threads could each delete-and-recreate the PID file via FileMode.CreateNew. Fix: return false when ReadAllText fails (file locked by another thread handling startup), preventing the delete-then-create race. One line changed in WatchdogService.cs line 53. All 3278+ tests pass, coverage gate 131/131.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed the single CI failure on Linux: WatchdogServiceTests.EnsureRunning_ConcurrentCalls_StartsOnlyOneWatchdog. Root cause: on Linux, File.Delete succeeds on files held open by another thread (Linux uses advisory file locking vs Windows mandatory locking). Multiple threads could each delete-and-recreate the PID file via FileMode.CreateNew. Fix: return false when ReadAllText fails (file locked by another thread handling startup), preventing the delete-then-create race. One line changed in WatchdogService.cs line 53. All 3278+ tests pass, coverage gate 131/131.

## Code Review

- Reviewed by: Emma
- Date: 2026-03-27 17:48
- Result: PASSED
- Notes: LGTM. One-line fix is correct: returning false on ReadAllText failure prevents the delete-then-create race on Linux. Test coverage via EnsureRunning_ConcurrentCalls_StartsOnlyOneWatchdog validates the fix. Coverage gate 131/131. Out-of-scope: PollAndCleanup_ProcessesRunning_ShellProcess_SkipsKill is a pre-existing flaky test (line 545, unrelated to this change).

Awaiting human approval.