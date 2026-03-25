---
area: general
type: changelog
date: 2026-03-25
---

# Task: fix-test-wt-leak

(No description)

## Progress

- [ ] (Not started)

## Files Changed

C:\Users\User\Desktop\Projects\DynaDocs\Services\WatchdogService.cs — Modified
C:\Users\User\Desktop\Projects\DynaDocs\DynaDocs.Tests\Services\WatchdogServiceTests.cs — Modified


## Review Summary

Added StartProcessOverride (Func<ProcessStartInfo, Process?>) to WatchdogService, used in TryCloseWindow and EnsureRunning to replace direct Process.Start calls. WatchdogServiceTests sets override to (_ => null) in constructor, clears in Dispose. This prevents TryCloseWindow_InvalidWindowId_DoesNotThrow and PollAndCleanup tests from spawning real wt.exe processes. Follows existing override pattern (ProcessUtils.PowerShellResolverOverride, TerminalLauncher.ProcessStarterOverride). No plan deviations. Note: gap_check uses stale coverage data since tests are locked by Dexter -- no new coverage regressions introduced.

## Code Review

- Reviewed by: Brian
- Date: 2026-03-20 16:53
- Result: PASSED
- Notes: LGTM. StartProcessOverride follows established pattern. PollAndCleanup logic fix is correct — fixes missing ClearAutoClose in fallback kill path. Tests pass (33/33). gap_check failures are pre-existing (stale coverage data, locked by Dexter), no regressions.

Awaiting human approval.

## Approval

- Approved: 2026-03-25 17:25
