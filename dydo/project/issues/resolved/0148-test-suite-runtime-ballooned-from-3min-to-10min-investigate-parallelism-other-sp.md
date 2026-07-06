---
title: Test suite runtime ballooned from 3min to 10min — investigate parallelism + other speedups
area: backend
fix-release: 
needs-human: false
resolution: 
severity: medium
status: resolved
work-type: 
id: 148
type: issue
found-by: manual
date: 2026-05-01
---

# Test suite runtime ballooned from 3min to 10min — investigate parallelism + other speedups
Diagnosed: the 10-min observation is **not** a 3× growth across the suite. It is a **30-second `WaitForExit` cliff** in `WorktreeMergeSafetyIntegrationTests.Git()` (`DynaDocs.Tests/Commands/WorktreeMergeSafetyIntegrationTests.cs:218-233`) that fires when git's stdout/stderr overflow the OS pipe buffer. Eight tests in the class can each lose 30s = ~4 min on top of the ~3:20 baseline. Fix is a localized test-helper refactor; suite returns to ~3:20.
## Description
The test suite was reported to take ~10 min vs a previous ~3 min baseline. Investigation showed solo `gap_check.py --force-run` runs at 3:19; two parallel runs at 3:29 / 3:30; coverage-enabled runs at 3:34. The 10-min number was reproduced exactly once, in a PowerShell-tool invocation, where 3 consecutive `WorktreeMergeSafetyIntegrationTests.*` tests failed at xUnit timestamps 0:34, 1:04, 1:34 — every test hitting the 30s `WaitForExit` exactly. With 8 tests in the class, the worst case adds ~4 minutes of dead time on top of the baseline.
## Root cause
`WorktreeMergeSafetyIntegrationTests.Git()` redirects stdout/stderr but never reads them while git is running:
```c#
RedirectStandardOutput = true,
RedirectStandardError = true,
...
var p = Process.Start(psi) ?? throw new InvalidOperationException("git failed to start");
p.WaitForExit(30_000);                  // never drains stdout/stderr
if (p.ExitCode != 0)                    // throws "Process must exit..." if still running
    throw new InvalidOperationException($"...: {p.StandardError.ReadToEnd()}");
```
When git produces enough output (Updating files: X% progress, autocrlf warnings, init template hints) to fill the OS pipe buffer (~64 KB on Windows), git blocks waiting for the buffer to drain. `WaitForExit(30_000)` returns false at 30s. Line 231 calls `p.ExitCode` on the still-running process and throws `Process must exit before requested information can be determined`. Test fails after a hard 30s wait. With 8 [Fact]s in the class each calling `Git()` multiple times, this can cascade.
Trigger conditions are environment-dependent — locale, autocrlf config, git version, AV scanning, concurrency-induced slow git output — which is why the slowdown is intermittent and was hard to reproduce in early measurement.
## Reproduction
1. Run `python DynaDocs.Tests/coverage/gap_check.py --force-run` in conditions that increase git's output volume on Windows (e.g. via the Claude PowerShell tool, or with multiple agents pressing the disk).
2. Observe `WorktreeMergeSafetyIntegrationTests.*` failing at exactly 30s each, exception type `System.InvalidOperationException : Process must exit before requested information can be determined.` originating at `WorktreeMergeSafetyIntegrationTests.Git`.
3. Wall time grows by ~30s per affected test, up to ~4 min if all 8 in the class trigger.
Counter-reproduction: a clean solo bash run completes in 3:19 with no failures — confirms the suite itself isn't 10 min.
## Resolution
Addressed by the runtime-regression PR batch. PR1 (#0169 commit 12e30e9) made `gap_check.py` propagate `dotnet test`'s exit code, ending the silent coverage gate that let parallelism-induced failures slip through. PR2 (#0167 commit 405a220) disabled assembly-wide xUnit parallelism in `DynaDocs.Tests` and migrated three gate-bypass `Console`-capture sites whose per-class isolation no longer held. PR3 (#0168) fixes the redirect-without-drain pattern diagnosed above in the `Git()` helper and its siblings, and PR4 (#0170) tightens the same pattern in production `Process.Start` callers. Together these close the contributing factors behind the observed 3min→10min runtime regression.
## Related
- `DynaDocs.Tests/Commands/WorktreeMergeSafetyIntegrationTests.cs:218-233` — the `Git()` helper with the deadlock
- Other lower-priority findings from this investigation are tracked separately:
  - AuditCompactionTests fixture trim (~6s save)
  - `dydo check --quick` (no-coverage iterative mode) for inner-loop speed
  - Integration collection serialization + CWD-mutation refactor (long-term ~30-90s parallelism win, separate decision)
  - Cross-class CWD-contamination flake family (#0116 resolved; #0136 still open)
- Investigation notes: `dydo/agents/Tara/notes-investigate-test-runtime.md`