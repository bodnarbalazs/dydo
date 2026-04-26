---
id: 116
area: backend
type: issue
severity: low
status: resolved
found-by: review
date: 2026-04-26
resolved-date: 2026-04-26
---

# WatchdogServiceTests.PollAndCleanup_SkipsWorkingAgents flakes when sibling PathUtilsTests Dispose deletes the captured CWD

## Description

`WatchdogServiceTests.PollAndCleanup_SkipsWorkingAgents` (and possibly other tests in the same file) captures `Environment.CurrentDirectory` at construction and uses it later for path resolution. A sibling test class — `PathUtilsTests` — `Dispose`s by deleting a temporary directory it set as CWD during its own execution. When the test runner schedules the two test classes such that `PathUtilsTests.Dispose()` fires after `WatchdogServiceTests` captured CWD but before the `PollAndCleanup_SkipsWorkingAgents` test body runs (or while it's resolving paths), the captured CWD is now a deleted directory. The watchdog service path resolution then fails (or returns paths that don't exist), and the test reports a flake.

Surfaced 2026-04-26 by reviewer Frank during review of `7811ad4` (`#0003`/`#0114` `--body` flag work). Frank's note: *"One transient WatchdogServiceTests flake observed (CWD captured-then-deleted by sibling PathUtilsTests Dispose) — unrelated to this task, passes on rerun; worth filing separately."*

The flake is rare in normal local runs (test ordering tends to be stable) but reproducible under parallel test execution and on CI runners where ordering can vary across runs.

## Reproduction

1. Ensure `PathUtilsTests` includes a `Dispose` (or `IDisposable`-style cleanup) that deletes a directory it `Environment.CurrentDirectory = ...`'d into.
2. Run the test suite with parallel test discovery (`dotnet test` default behavior in many configurations).
3. Observe occasional `WatchdogServiceTests.PollAndCleanup_SkipsWorkingAgents` failures with errors related to non-existent paths or unresolvable working directories.
4. Re-run the suite — the flake clears because test ordering shifts and `Dispose` no longer interleaves with the captured-CWD window.

The exact frequency depends on the runner's parallelism and the timing of `PathUtilsTests.Dispose()`. In Frank's observation it was a single transient failure followed by a clean rerun.

## Likely root cause

Two interacting test-isolation gaps:

1. **`WatchdogServiceTests` captures `Environment.CurrentDirectory` once and reuses it later** rather than passing the relevant directory in via test setup. Test-level CWD is global mutable state shared across the runner's parallel partition; capturing it implicitly is a hazard.
2. **`PathUtilsTests.Dispose()` deletes the directory it set as CWD without restoring CWD first.** That leaves CWD pointing at a deleted path for any test that runs after `Dispose` but before the runner moves on.

Either gap independently is fixable. Together they produce the flake.

## Suggested fix

Pick one:

1. **In `WatchdogServiceTests`:** stop capturing `Environment.CurrentDirectory`. Use the test-context's working directory explicitly (e.g., a `TestContext` property, an injected path, or a dedicated `Path.GetTempPath()`-rooted directory created in the test's setup and torn down in its own `Dispose`). This is the durable fix.
2. **In `PathUtilsTests.Dispose()`:** restore CWD to its pre-test value before deleting the temp directory. Cheaper but leaves the capture-CWD pattern in `WatchdogServiceTests` as a foot-gun for future tests.

Do both for belt-and-braces. Add a regression test (or just a comment in `WatchdogServiceTests`) explaining why CWD is not captured.

## Impact

- Low: a transient flake on local + CI. Reruns clear it. Has not blocked any merge in observed history.
- Annoying: agents reviewing changes can't trust a single test run; "rerun on flake" becomes part of the review playbook, which dilutes "tests must be green" as a signal.
- Long-tail risk: as the test suite grows, more tests may hit similar CWD-capture / CWD-deletion patterns. Worth fixing the underlying anti-pattern now while there's only one observed instance.

## Related context

- `DynaDocs.Tests/Services/WatchdogServiceTests.cs` — the `PollAndCleanup_SkipsWorkingAgents` test (and possibly siblings using captured CWD).
- `DynaDocs.Tests/Utils/PathUtilsTests.cs` — the `Dispose` that deletes a CWD-set directory.
- Frank's review at `dydo/agents/Frank/review-issue-create-body-and-test-name.md` (or the associated review notes) — original flake observation.
- gap_check / `dotnet test` runs from the 2026-04-26 session — produced the flake-then-clean-rerun pattern Frank described.

## Resolution

Fixed in commit 3654ec6 (Frank). WatchdogServiceTests no longer captures Environment.CurrentDirectory; uses test-context working directory explicitly. PathUtilsTests.Dispose parks on Path.GetTempPath() with justified try/catch for cleanup. Rationale comments cite the issue. 3803/3803 tests clean on forced rerun; gap_check 136/136. Reviewed by Charlie.
