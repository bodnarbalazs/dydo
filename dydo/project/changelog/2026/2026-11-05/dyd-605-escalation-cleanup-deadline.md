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

Budgets are unchanged: `CLEANUP_SECONDS`, `DESCENDANT_EXIT_SECONDS`, the cleanup deadline and the
grace fraction. Success still needs a listing after the last kill that shows no live descendant.
Neither pinned kills nor a new pass start after the deadline. A first listing that cannot read
`/proc` pins nothing, so the grace still ends before any kill. The Windows job path is untouched.

## Proof

The new `CoverageCancellationTests` regression drives the native escalation entry with the
adapter's first process-table listing delayed 2.1 seconds. At base `88d9dcd4` it exits 1 with the
filed chain while the descendants have ended and the foreign process lives. On the fix it exits
130. Unit tests pin the pin-under-lock invariant, pidfd kills that skip a collected process, the
grace order, and expiry without a confirming listing.

## Files Changed

`DynaDocs.Tests/coverage/run_tests.py`, `DynaDocs.Tests/coverage/gate_adapter.py`, and the tests
`test_gate_adapter.py`, `test_reaping.py` and `test_runner.py` (proof commit a87ae611, then the
DYD-605 implement commit), and this entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
