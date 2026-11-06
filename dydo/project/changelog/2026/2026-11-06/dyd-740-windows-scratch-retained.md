---
area: project
type: changelog
date: 2026-11-06
---

# DYD-740 Windows refuses a registered scratch replacement, and its test now says so

## Summary

`StatechartLifecycleTests.AReplacedScratchPathIsRetainedEvenAfterTheDrain` failed on windows-latest
([DYD-740](https://linear.app/bodnar-balazs/issue/DYD-740)). The product was right; the test was
not. The cleanup helper holds each registered scratch through `WindowsStatechartFiles.Open`, whose
share mode omits `FILE_SHARE_DELETE`. So the test's first step, moving the registered stage away to
replace it, raised a sharing violation while the helper was still alive. The test's `using`
teardown then deleted its directory with the helper still holding the stage, and that IOException
replaced the real one. No product code changed.

The test now asserts each platform's own guarantee. POSIX keeps its scenario and assertions: a
replaced path is retained after the drain, and the foreign replacement survives. On Windows the
replacement cannot happen. Moving the registered stage away or deleting it is refused with a sharing
violation, and renaming its parent is refused with access denied. The stage stays until the
authoritative drain, which deletes the stage and temp scratch, journals
`deleted-after-authoritative-drain` and leaves a foreign directory beside them untouched.

`StatechartTestDirectory` now owns the helpers the lifecycle tests start. Before it deletes its
directory, it closes each helper's input and waits for it to exit, so a failing test cannot leave a
helper holding its scratch or hide the failure behind a teardown error. The helper answers EOF by
retaining its scratch and closing the handles before it exits. The fixture does not kill the helper,
because a killed helper's handle was seen still open right after its exit.

## Proof

- Red, release validation run 42133806406 attempt 2 on the DYD-740 proof commit `48a55f08`, job
  127642463384. With the fixture ending the helper first, the test's first exception surfaced:
  `IOException` from `Directory.Move` at the stage move, not the teardown error.
- In the same run, tagged probes, since removed, recorded these results:
  - Moving or deleting the registered stage or temp failed with 0x80070020 (sharing violation).
  - Renaming the stage's parent failed with 0x80070005 (access denied).
  - A drain after those refusals cleaned both scratches and exited 0.
  - After an EOF exit with code 2, the stage moved freely, so no handle outlives the helper.
  - After a kill, a move made straight after the helper's exit still hit the sharing violation.

## Files Changed

`DynaDocs.Tests/Statechart/StatechartLifecycleTests.cs` and
`DynaDocs.Tests/Statechart/StatechartTestDirectory.cs` (the DYD-740 implement commit), and this
entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
