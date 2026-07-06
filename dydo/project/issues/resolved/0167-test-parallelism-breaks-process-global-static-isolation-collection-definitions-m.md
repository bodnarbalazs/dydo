---
title: Test parallelism breaks process-global static isolation: collection definitions misconfigured
id: 167
area: general
type: issue
severity: high
status: resolved
found-by: inquisition
date: 2026-05-05
---

# Test parallelism breaks process-global static isolation: collection definitions misconfigured

Three of the four xunit collection definitions in `DynaDocs.Tests` are misconfigured (`DisableParallelization = true` missing on `ProcessUtils` and `EndToEnd`; no `ConsoleOutput` collection defined at all), so multiple test classes that mutate the same process-global statics — `Console.Out/Error`, `ProcessUtils.IsProcessRunningOverride`, `ProcessUtils.GetProcessNameOverride`, `ProcessUtils.PowerShellResolverOverride` — can run in parallel and stomp on each other. This is the underlying mechanism behind #0165's Console-capture cross-over under coverage; one realisation was reproduced live during this inquisition (`QueueServiceTests.FindStaleActiveEntries_DetectsDeadPid` failing because `WorktreeCommandTests` had nulled `IsProcessRunningOverride` mid-run).

## Description

Three of the four xunit collection definitions in DynaDocs.Tests are misconfigured, so multiple test classes that mutate the same process-global statics (`Console.Out/Error`, `ProcessUtils.IsProcessRunningOverride`, `ProcessUtils.GetProcessNameOverride`, `ProcessUtils.PowerShellResolverOverride`) can run in parallel and stomp on each other.

This is the underlying mechanism behind #0165 (Console-capture cross-over under coverage). One realisation of the same race was reproduced live during this inquisition: `QueueServiceTests.FindStaleActiveEntries_DetectsDeadPid` failed at xunit time 00:00:06.76 because `WorktreeCommandTests` (a different parallel collection) had nulled `IsProcessRunningOverride` while the test was reading it.

## Evidence

### Collection definitions

- `DynaDocs.Tests/Integration/IntegrationTestCollection.cs:8` — `[CollectionDefinition("Integration", DisableParallelization = true)]`. The only collection that opts out of cross-collection parallelism.
- `DynaDocs.Tests/Services/ProcessUtilsCollection.cs:8` — `[CollectionDefinition("ProcessUtils")]` **without** `DisableParallelization = true`. The class doc-comment claims it "Disables xUnit parallel execution between test classes that share static mutable ProcessUtils overrides" — false. The flag is missing.
- `DynaDocs.Tests/EndToEnd/CliEndToEndTests.cs:385` — `[CollectionDefinition("EndToEnd")]` similarly without the flag.
- **No `[CollectionDefinition("ConsoleOutput")]` exists at all.** Nine test classes use `[Collection("ConsoleOutput")]` but rely on xunit's implicit-definition fallback, which has default (parallelise-with-other-collections) settings.

### Static mutations across parallelisable collections

`ProcessUtils.IsProcessRunningOverride` is mutated from three different collections:

- `[Collection("ProcessUtils")]`: `WatchdogServiceTests`, `QueueServiceTests`, `FileLockTests`.
- `[Collection("Integration")]`: `WaitCommandTests:202-223,336-355`, `DispatchQueueTests`. Safe because Integration has `DisableParallelization = true`.
- `[Collection("ConsoleOutput")]`: `WorktreeCommandTests:2907,2921,2940,2953`. **Not safe** — ConsoleOutput parallelises with ProcessUtils.

### Console.Out/Error gate-bypass sites

`DynaDocs.Tests/ConsoleCapture.cs` provides a single static `SemaphoreSlim Gate` that serialises every redirect-execute-restore. Three sites bypass it:

- `DynaDocs.Tests/Services/AuditCompactionTests.cs:843-854` — direct `Console.SetError(stderr)` and restore. Class has no `[Collection]` attribute.
- `DynaDocs.Tests/Services/AuditEdgeCaseTests.cs:110-129` — same pattern, no `[Collection]`.
- `DynaDocs.Tests/Commands/WorktreeMergeSafetyIntegrationTests.cs:278-296` (`CaptureAll`) — direct `Console.SetOut/SetError`. Class is `[Collection("ConsoleOutput")]`.

#0165's smoking-gun cross-over (`WorktreeCommandTests.Merge_BranchNotAdvanced_Blocks_WithoutRunningGitMerge` seeing `AuditCompactionTests`'s `[dydo] WARNING` and vice versa) is exactly this pattern: even when one side acquires the gate, a gate-bypassing side can capture the gate-holder's writer as its "original" and the restore order produces the cross-over.

## Reproduction

```
python DynaDocs.Tests/coverage/run_tests.py
```

The race is intermittent. On the inquisition's reproduction run `QueueServiceTests.FindStaleActiveEntries_DetectsDeadPid` failed; on the immediate follow-up `gap_check.py --force-run` it did not. #0165's reporter saw the Console cross-over on a different branch; it did not fire on this run.

## Fix path

Two-step:

1. Migrate the three gate-bypass `Console.SetOut/SetError` sites onto `ConsoleCapture.Stdout/Stderr/All` (matches existing disciplined sites).
2. Add `DisableParallelization = true` to `ProcessUtilsCollection` and `EndToEndCollection`; add a new `ConsoleOutputCollection` definition with the same flag; route every no-collection class that touches any process-global static into one of those collections.

These should land together — landing 1b without 1a leaves the gate-bypass sites as residual hazard inside their newly-sequential collection; landing 1a without 1b leaves the cross-collection race for `IsProcessRunningOverride` etc.

A cleaner long-term alternative (per #0165 path 1): migrate Console-asserting tests to xunit's `ITestOutputHelper`, and have production code accept a redirectable writer. Larger blast radius — call out as a separate decision rather than bundling.

## Related

- #0148 — test runtime regression; the prior `Git()`-helper fix (`aeee461`) addressed one cliff but did not address this isolation problem.
- #0165 — Console-capture cross-over under coverage; the most visible face of this same misconfiguration.
- Inquisition: `dydo/project/inquisitions/test-runtime-regression.md` Finding #1.

## Resolution

Resolved by `405a220` (PR2 of the runtime-regression batch). Rather than fix the four collection definitions individually, the assembly itself was made fully sequential via `[assembly: CollectionBehavior(DisableTestParallelization = true)]` in `DynaDocs.Tests/AssemblyInfo.cs` — collapsing the cross-collection race surface to a single global ordering. The same commit migrated the three gate-bypass `Console.SetOut/SetError` sites (`AuditCompactionTests`, `AuditEdgeCaseTests`, `WorktreeMergeSafetyIntegrationTests.CaptureAll`) onto `ConsoleCapture.Stderr` / `.All` so the capture gate is no longer bypassable, and added `ParallelisationDisabledTests` to pin the assembly-level invariant via reflection. Follow-up `e3e6c47` replaced a dead `AuditCompaction` test with `ConsoleCaptureTests` to pin the contract.