---
area: project
type: changelog
date: 2026-11-05
---

# DYD-678 POSIX group drain reads a task being released as no member

## Summary

The independent POSIX statechart owner no longer aborts its group drain when the host's process
listing catches a task that the kernel is releasing
([DYD-678](https://linear.app/bodnar-balazs/issue/DYD-678)). After it stops the checking group,
`PosixStatechartProcess.WaitForEmptyAsync` lists every process on the host with
`/bin/ps -axo pgid=,stat=` and reads each line as a pgid and a state. If `release_task` is tearing a
task down while ps reads its `/proc/<pid>/stat`, the kernel can no longer lock the task's signal
handlers and reports its pgid as -1. procps prints a pgid unsigned, so the line reads `4294967295 X+`.
The owner parsed the pgid as `int` and rejected that line with "Cannot interpret statechart group
inspection." It retained its scratch as uncertain and exited 2 before it answered `drained:`. Any
process on the host can be the one being released, so the failure is rare. In the full .NET coverage
campaign it hit one of about 55 drains, as `StatechartPosixLifecycleTests.AnUnprovenCompletedRequestFrameRecordsNoVerdict`
reading null at its drain. Production statechart runs drain through the same listing.

`IsLiveMember` now reads the pgid unsigned, as procps writes it. 4294967295 never names an owned
group, so a task being released counts as no member. Every other line must still be exactly an
unsigned pgid and a state, and any other line still stops the drain. The interpretation is the new
internal `ListsLiveMember(listing, group)`, which `HasLiveMembersAsync` calls with the ps output.

## Proof

- The failing campaign's AltCover report visits the `IsLiveMember` throw once, along with the
  owner's uncertain-retention path. A passing campaign from the same worktree visits neither.
- On this host's procps-ng 4.0.4, re-reading `/proc/<pid>/stat` while its parent reaped the zombie
  read pgid -1 in 223 of 2000 trials. The real `/bin/ps` printed `4294967295 X+` (143 times) and
  `4294967295 Z+` (twice) for zombies reaped during its scan, in 22 of 200 trials, and no other
  rejected shape.
- Red: the test's `anonymous` case under AltCover instrumentation, with that captured line replayed
  into every `/bin/ps` listing by a ps-only `LD_PRELOAD` shim, failed at its drain assertion
  (`drained:<nonce>` expected, null received) in 20 of 20 runs at base `8a53ee6a`. A well-formed
  replayed line passed 10 of 10. With the fix, 20 of 20 passed; reverting the fix failed 5 of 5.
- The new `StatechartPosixTests.AGroupInspectionReadsATaskBeingReleasedAsNoMember` passes the
  captured lines through `ListsLiveMember`. It fails before the fix with the owner's exception and
  passes after it.

## Files Changed

`Services/Statechart/PosixStatechartProcess.cs` and `DynaDocs.Tests/Statechart/StatechartPosixTests.cs`
(the DYD-678 implement commit), and this entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
