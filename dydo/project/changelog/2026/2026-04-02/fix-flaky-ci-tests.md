---
area: general
type: changelog
date: 2026-04-02
---

# Task: fix-flaky-ci-tests

Fixed 3 flaky CI tests. (1) EnsureRunning_ConcurrentCalls: replaced Thread.Sleep timing with CountdownEvent/ManualResetEventSlim synchronization and removed stale PID — race is now purely on FileMode.CreateNew. (2) ShellProcess_SkipsKill: added GetProcessNameOverride to ProcessUtils, refactored WatchdogService.PollAndCleanup to use it, replaced real shell PID with dummy process + mocked name. (3) FindStaleActiveEntries_IgnoresRunningPid: replaced static override with Environment.ProcessId (always alive). Added [Collection(ProcessUtils)] to both test classes to prevent cross-class override races. All 3390 tests pass, gap_check clean.

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\ProcessUtils.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\ProcessUtilsCollection.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\QueueServiceTests.cs — Modified


## Review Summary

Fixed 3 flaky CI tests. (1) EnsureRunning_ConcurrentCalls: replaced Thread.Sleep timing with CountdownEvent/ManualResetEventSlim synchronization and removed stale PID — race is now purely on FileMode.CreateNew. (2) ShellProcess_SkipsKill: added GetProcessNameOverride to ProcessUtils, refactored WatchdogService.PollAndCleanup to use it, replaced real shell PID with dummy process + mocked name. (3) FindStaleActiveEntries_IgnoresRunningPid: replaced static override with Environment.ProcessId (always alive). Added [Collection(ProcessUtils)] to both test classes to prevent cross-class override races. All 3390 tests pass, gap_check clean.

## Code Review

- Reviewed by: Unknown
- Date: 2026-04-02 18:21
- Result: PASSED
- Notes: LGTM. All 3 flaky test fixes are correct and minimal: (1) ConcurrentCalls uses CountdownEvent/ManualResetEventSlim instead of Thread.Sleep — deterministic. (2) ShellProcess_SkipsKill uses GetProcessNameOverride following the existing IsProcessRunningOverride pattern — no real shell PID dependency. (3) IgnoresRunningPid uses Environment.ProcessId — simpler, always correct. Collection attribute correctly prevents cross-class override races. 3390 tests pass, gap_check clean (132/132 modules).

Awaiting human approval.

## Approval

- Approved: 2026-04-02 18:55
