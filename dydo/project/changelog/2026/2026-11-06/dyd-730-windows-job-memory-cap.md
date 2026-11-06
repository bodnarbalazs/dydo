---
area: general
type: changelog
date: 2026-11-06
---

# dydo 3.6.0 - Windows facade Job-object memory caps (unreleased)

## Summary

Windows child-running facade operations now assign themselves to a private nested Job object
with a default 8G committed-memory limit. `DYDO_MEMORY_MAX` may lower it. The facade stays in
place and children inherit the job without changes to their lifetimes
([DYD-730](https://linear.app/bodnar-balazs/issue/DYD-730)).

## Changed

- Windows records `enforced` and the lower of the requested and outer job-memory limits. Setup
  or query errors record `unavailable` with one warning and let the operation proceed.
- Delivered job-memory-limit messages fail their row with `memoryCapHit` and the same finding
  as Linux. Operation and row records declare `memoryCapHitReporting`: `best-effort` on Windows,
  `exact` on Linux. `peakJobMemory` is the cumulative job peak sampled per row, diagnostic only.
- Mocked kernel32 tests run on every OS. The live Windows fixture requires a 400M allocation to
  fail under a 256M cap; it does not require notification delivery.
- The portable example is generated with `sync_testing_example.py`. Both testing guides and
  Coverage Tools describe the mechanism and the limits of hit reporting.

No glossary term is renamed, so this entry has no merge normalisations.

## Residuals

Windows completion-port delivery is not guaranteed. A missing message proves nothing, and
neither peak memory nor a child's exit establishes a hit. Hosted Windows CI is the proof of
record for native enforcement. macOS remains unavailable; Linux gains only the reporting field.

## Files Changed

`DynaDocs.Tests/coverage/gap_check.py`, `test-associations.json`,
`tests/test_windows_memory_cap.py`, `tests/test_memory_cap.py`, `tests/test_testing_facade.py`,
`DynaDocs.Tests/Steps/AssuranceAdoptionSteps.cs` (result-key assertions),
`dydo/reference/gap-check.example.py`, both `testing-strategy.md` copies,
`dydo/reference/coverage-tools.md`, and this entry.

## Related

- [Coverage Tools](../../../../reference/coverage-tools.md#memory-cap) - Memory cap behavior
- [Testing Strategy](../../../../guides/testing-strategy.md#runner-prerequisites-and-shared-hosts) - Shared host commands
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
