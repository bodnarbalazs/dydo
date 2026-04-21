---
id: 97
area: backend
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-04-18
resolved-date: 2026-04-21
---

# Watchdog Run() has no cancellation, parent-PID, or signal handling — outlives its purpose

## Description

`WatchdogService.Run` (`Services/WatchdogService.cs:142-162`) is a bare `while (true) { Thread.Sleep(10_000); … }` loop. It has:

- no `CancellationToken`
- no `AppDomain.CurrentDomain.ProcessExit` handler
- no `Console.CancelKeyPress` handler
- no parent-PID liveness check
- no "work is done" condition

The only ways to terminate it are: (a) `dydo watchdog stop` (explicit, rare), or (b) external process kill (what users actually reach for — `taskkill /im dydo.exe /f` appears repeatedly in the LC audit). There is also no cleanup of `watchdog.pid` on exit, because there is no exit path in the first place.

Compare to `WaitCommand` (`Commands/WaitCommand.cs:76-79, 122-125`), which uses `ProcessUtils.GetParentPid`, `FindAncestorProcess("claude")`, and `Console.CancelKeyPress` to self-terminate — exactly the pattern the watchdog is missing.

Tied to inquisition `dydo/project/inquisitions/stale-dydo-processes.md` finding F3. Directly explains why the watchdog outlives its spawning dispatch and why `taskkill` is the only observed remediation path.

## Reproduction

Covered by `DynaDocs.Tests/Services/WatchdogParentLivenessAbsenceTests.cs` (three PASSING structural tests):

1. `WatchdogService_Run_Body_Contains_NoParentLivenessTokens` — extracts `Run`'s body and asserts none of `GetParentPid`, `FindAncestorProcess`, `CancellationToken`, `ProcessExit`, `CancelKeyPress` appear.
2. `WaitCommand_Source_Contains_LivenessTokens_SanityCheck` — positive control proving the token detector works (WaitCommand does contain them).
3. `WatchdogService_Run_Body_IsBareWhileTrueSleepLoop` — asserts the body matches `while (true)` with `Thread.Sleep` and no `break`/`return` after the loop start.

The brief's original behavioural variant (spawn a real watchdog, orphan it, verify it keeps running) was intentionally not written — running it would itself leak the bug being investigated.

## Resolution

Add a termination path. Minimum viable:

1. Record the spawning dydo PID in `watchdog.pid` (two lines: watchdog PID, anchor PID).
2. In `Run`, check the anchor PID's liveness each tick. If the anchor is gone for N consecutive polls (e.g. 3 × 10 s = 30 s), break the loop.
3. Register `AppDomain.CurrentDomain.ProcessExit` and `Console.CancelKeyPress` to set a `CancellationTokenSource` and break the loop.
4. In `finally`, delete the pid file. (This is a prerequisite for issue #98 — it only has to handle *crashed* orphans if graceful exits already clean themselves up.)

A heartbeat-file approach (watchdog writes a timestamp every N seconds; dispatch/guard scrubs it) is an alternative if parent-PID is judged unreliable on Windows due to PID recycling.