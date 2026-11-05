---
area: project
type: changelog
date: 2026-11-05
---

# DYD-605 escalation cleanup lists descendants during the owner's grace

## Summary

An interrupted Python coverage adapter no longer fails its cleanup when one `/proc` listing is slow
([DYD-605](https://linear.app/bodnar-balazs/issue/DYD-605)). On escalation, the adapter gives the
coverage owner a SIGINT grace of a third of its cleanup window. Only after that grace did it list
its descendants to kill them, and then list them again to confirm they had ended. A listing that
took 2.1 of the 3 cleanup seconds left no time for the confirming one. The adapter then exited 1
with `TimeoutError` -> `MeasurementTimeout` instead of 130, although every descendant had already
ended. Loaded hosts showed the same symptom without any forcing (DYD-597).

`gate_adapter._stop_coverage_campaign` now makes its first descendant listing during the owner's
grace, through `run_tests.pin_descendants`. That listing opens a pidfd for each live descendant
under `_REAP_LOCK`. A pidfd names its process and never a later one given the same pid, so the
kill can wait until the grace is over. After the grace, `end_descendants` kills the pinned
descendants first, then lists. When the pins were all that lived, that first listing confirms the
cleanup. A descendant left unpinned is listed and killed as before.

`_REAP_LOCK` stops only this process's reaping, and the owner is still alive during the grace, so a
listed descendant's own parent can collect it, and its pid can pass to another process, before its
pidfd opens. The listing therefore records each descendant's start time, field 22 of its
`/proc/<pid>/stat`. Once the pidfd is open, `pin_descendants` reads that field again. A pid passes
only to later processes, so the same start time proves the pidfd names the listed descendant. A
pidfd whose process shows another start time, or none, is closed at once and never signalled. An
interrupted pinning closes every pidfd it opened before the interrupt propagates.

Budgets are unchanged: `CLEANUP_SECONDS`, `DESCENDANT_EXIT_SECONDS`, the cleanup deadline and the
grace fraction. Success still needs a listing after the last kill that shows no live descendant.
The deadline is checked before each pinned kill and before each pass, so neither starts after it;
pins left unsignalled at the deadline are closed and the cleanup raises. A first listing that
cannot read `/proc` pins nothing, so the grace still ends before any kill. The Windows job path is
untouched.

## Proof

The new `CoverageCancellationTests` regression drives the native escalation entry with the
adapter's first process-table listing delayed 2.1 seconds. At base `88d9dcd4` it exits 1 with the
filed chain while the descendants have ended and the foreign process lives. On the fix it exits
130. Unit tests pin the pin-under-lock invariant, pidfd kills that skip a collected process, the
grace order, and expiry without a confirming listing. They also cover a deadline that falls between
two pinned kills, a listed pid that names another process by the time its pidfd opens, and an
interrupt during pinning.

## Files Changed

`DynaDocs.Tests/coverage/run_tests.py`, `DynaDocs.Tests/coverage/gate_adapter.py`, and the tests
`test_gate_adapter.py`, `test_reaping.py` and `test_runner.py` (proof commit a87ae611, then the
DYD-605 implement and fix commits), and this entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
