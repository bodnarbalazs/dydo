---
area: general
name: verify-process-leak-fix
status: human-reviewed
created: 2026-03-26T20:50:43.4952300Z
assigned: Dexter
updated: 2026-03-26T23:19:52.2766986Z
---

# Task: verify-process-leak-fix

Fixed 3 process leak bugs (TDD — tests first, then fixes, all 3275 tests pass, coverage gate clean). Bug 1: WaitCommand now checks Claude ancestor PID via FindAncestorProcess in addition to immediate parent — fixes circular dependency where background bash survives Claude exit on Windows (WaitCommand.cs, ProcessUtils.Ancestry.cs). Bug 2: New PollOrphanedWaits in WatchdogService scans wait markers for free agents and kills live orphaned wait PIDs — the watchdog previously only searched for agent session processes, missing dydo wait processes entirely (WatchdogService.cs). Bug 3: EnsureRunning rewritten with FileMode.CreateNew for atomic PID file creation — prevents concurrent dispatches from spawning duplicate watchdog instances (WatchdogService.cs). 7 new tests across WaitCommandTests.cs and WatchdogServiceTests.cs. No plan deviations.

## Progress

- [ ] (Not started)

## Files Changed

(None yet)

## Review Summary

Fixed 3 process leak bugs (TDD — tests first, then fixes, all 3275 tests pass, coverage gate clean). Bug 1: WaitCommand now checks Claude ancestor PID via FindAncestorProcess in addition to immediate parent — fixes circular dependency where background bash survives Claude exit on Windows (WaitCommand.cs, ProcessUtils.Ancestry.cs). Bug 2: New PollOrphanedWaits in WatchdogService scans wait markers for free agents and kills live orphaned wait PIDs — the watchdog previously only searched for agent session processes, missing dydo wait processes entirely (WatchdogService.cs). Bug 3: EnsureRunning rewritten with FileMode.CreateNew for atomic PID file creation — prevents concurrent dispatches from spawning duplicate watchdog instances (WatchdogService.cs). 7 new tests across WaitCommandTests.cs and WatchdogServiceTests.cs. No plan deviations.

## Code Review

- Reviewed by: Charlie
- Date: 2026-03-26 23:30
- Result: PASSED
- Notes: LGTM. All 3 fixes are correct: (1) Claude ancestor PID check mirrors existing parent PID pattern, (2) PollOrphanedWaits logic is clean with proper edge case handling, (3) EnsureRunning atomicity via FileMode.CreateNew is sound. 7 new tests meaningful and well-structured. 3275 tests pass, coverage gate clean (131/131 modules). Code is minimal and follows standards.

Awaiting human approval.