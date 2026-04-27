---
id: 117
area: backend
type: issue
severity: low
status: resolved
found-by: review
date: 2026-04-27
resolved-date: 2026-04-27
---

# WatchdogServiceTests.Stop_ReturnsTrue_WhenProcessIsRunning flakes on CI Linux because ping process exits inside the test window

## Description

`DynaDocs.Tests/Services/WatchdogServiceTests.Stop_ReturnsTrue_WhenProcessIsRunning` consistently fails on the GitHub Actions Linux CI runner with `Assert.True() Failure`. The test passes locally and passes when the file is run in isolation; it only fails as part of the full suite on CI Linux.

The test calls `WatchdogService.Stop` and asserts the return value is `true`. `Stop` returns `false` when `IsProcessRunning(pid)` reports the process is not alive. On the Linux CI runner, the dummy process used by the test (`ping`, run with no count limit so it should be long-running) is observed exiting fast enough that the ~4ms gap between spawn and `Stop`'s liveness check sees a dead PID, causing the assertion to fail.

The sibling test `WatchdogServiceTests.Stop_DeletesPidFile_WhenProcessIsRunning` passes in the same CI run because it does not assert on `Stop`'s return value — the pidfile is deleted regardless of whether `Stop` reported success or no-op.

This is **not** the same pattern as `#0116` (CWD captured-then-deleted by sibling `PathUtilsTests.Dispose()`). No CWD capture is involved in this test. Surfaced 2026-04-27 by Henry during `fix-ci-after-audit-recovery`; investigation notes:

- CI run reference: `24996949358` (one of several red runs that surfaced the failure).
- Root cause inferred from the assertion shape, the timing window, and the contrast with the sibling test that doesn't check the return value.
- Pattern: `ping` on Linux without `-c` should run indefinitely, but on the GitHub-hosted runner the process appears to exit within milliseconds of spawn — possibly because the runner's network stack rejects the ICMP socket creation and ping exits with an error very quickly.

## Reproduction

1. Push any commit to a branch that triggers GitHub Actions CI on Linux.
2. Watch the test run. `WatchdogServiceTests.Stop_ReturnsTrue_WhenProcessIsRunning` will fail with:
   ```
   Assert.True() Failure
   ```
3. Run the same suite locally on Windows or macOS — the test passes.
4. Run the test in isolation on CI:
   ```
   dotnet test --filter "FullyQualifiedName~WatchdogServiceTests.Stop_ReturnsTrue_WhenProcessIsRunning"
   ```
   The behaviour depends on whether the `ping` process exits before the assertion runs — may pass intermittently when run alone (less CPU contention).

## Likely root cause

`WatchdogServiceTests` spawns `ping` as a stand-in for "any long-running process" and assumes it will live for at least the duration of the assertion. On the GitHub Actions Linux runner this assumption breaks: the runner sandbox or network stack causes `ping` to exit nearly immediately. By the time `Stop` queries `IsProcessRunning(pid)`, the PID is gone and `Stop` returns `false`, even though logically the test was running.

This is a test-isolation gap, not a `WatchdogService` bug. The production code is doing the right thing (correctly reporting "the process is no longer running, so Stop is a no-op").

## Suggested fix

Pick the most reliable available, in order of preference:

1. **Mock `IsProcessRunning`** for the duration of the test. Inject a stub that returns `true` until `Stop` has run its course, then verify `Stop` reported `true`. Decouples the test from real OS process behaviour entirely.

2. **Use a more reliable long-running dummy** than `ping`:
   - `sleep 30` (Linux/macOS) — guaranteed to live for 30 seconds.
   - `timeout 30 cat` (read from stdin until killed).
   - A custom test helper that spawns a thread doing `Thread.Sleep(TimeSpan.FromMinutes(1))` in a child process.
   - Pick a primitive that doesn't depend on network or OS-specific behaviour.

3. **Increase the assertion's tolerance** — if `IsProcessRunning` reports false but the test recently spawned the process, treat it as a "process exited too fast for this CI environment" skip (with a clear note). Worst option — masks the underlying brittleness.

Recommendation: option 1 (mock the dependency) is the durable fix. Option 2 (replace `ping` with `sleep`) is a quick win that probably also resolves it without the mock infrastructure. Combine both: use `sleep` as the spawn primitive AND mock `IsProcessRunning` for the assertion paths.

Add a regression that runs the test in tight succession 10x — if it ever fails, the underlying flakiness is back.

## Impact

- CI red on every push that touches the test file (or that runs the full suite). Forces "rerun on flake" as part of the PR review playbook.
- Has not blocked any merge in observed history because reviewers learned to spot the pattern, but it dilutes "tests must be green" as a signal.
- Will eventually hide a real regression — when the next test starts failing with `Assert.True()`, it'll get classified as "the same flake" and ignored.

## Related context

- `DynaDocs.Tests/Services/WatchdogServiceTests.cs` — the failing test.
- `Services/WatchdogService.cs` — `Stop` and its `IsProcessRunning` dependency.
- Issue `#0116` — different CWD-pollution pattern in the same test file. Frank's fix landed earlier and does not address this case.
- CI run `24996949358` — first observation in the audit-recovery context; Henry's investigation notes are in `dydo/agents/Henry/inbox/archive/` (around 2026-04-27).
- Henry's intermediate status message at `dydo/agents/Brian/inbox/archive/` (subject `fix-ci-after-audit-recovery`) — the original investigation summary.

## Resolution

WatchdogService Stop_ReturnsTrue_WhenProcessIsRunning flake fixed in commit 980104a (Frank). Platform-split: sleep 30 on POSIX, kept ping on Windows where it's reliable. Added Stop_ReturnsTrue_WhenProcessIsRunning_TightSuccession regression test running the assertion 10 times in a row. Verified locally with 3 consecutive WatchdogServiceTests runs (54/54 each). Reviewed by Charlie.
