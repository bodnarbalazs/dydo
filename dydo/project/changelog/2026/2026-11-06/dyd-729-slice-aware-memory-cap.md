---
area: general
type: changelog
date: 2026-11-06
---

# dydo 3.6.0 - Facade memory caps stay in the caller's slice (unreleased)

## Summary

The facade's enforced scope now joins the caller's nearest slice within the user manager, so
concurrent runs share that slice's memory budget. Previously each rerun moved to `app.slice`,
escaping the aggregate budget above its caller ([DYD-729](https://linear.app/bodnar-balazs/issue/DYD-729)).

## Changed

- Both the probe and the enforced rerun select the caller's slice. Plain `app.slice` and callers
  without a user-manager slice keep the default placement. Inheritance stays unchanged, and
  `memoryCap.reason` names the enforced slice.
- The shared cap defaults unset `MSBUILDDISABLENODEREUSE`, `DOTNET_CLI_USE_MSBUILD_SERVER` and
  `UseSharedCompilation` to `1`, `0` and `false`. Caller values stay intact. Direct `run_tests.py`
  receives these defaults through its existing import, without changing that file.
- Both testing guides, Coverage Tools and the six skills show the slice-aware ad-hoc command
  and the .NET environment prefix. Each run has its own cap; only the shared slice budget bounds
  concurrent runs together.
- New regression tests cover nested and default placement, user-manager boundaries, the recorded
  slice, environment defaults and caller overrides, for both runner copies. Existing tests are
  unchanged. The example is generated only by `sync_testing_example.py`.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Residuals

Windows and macOS still report the memory cap unavailable. A scope limit bounds each enforced
rerun individually; an aggregate bound requires a capped ancestor slice. A kernel kill of the
facade itself can still prevent it from writing a result.

## Files Changed

`DynaDocs.Tests/coverage/gap_check.py`, `DynaDocs.Tests/coverage/tests/test_memory_cap.py`,
`dydo/reference/gap-check.example.py`, both `testing-strategy.md` copies,
`dydo/reference/coverage-tools.md`, the diagnosing-bugs, verify-dydo, code-writer, inquisitor,
reviewer and ticket-captain skills, and this entry.

## Related

- [Coverage Tools](../../../../reference/coverage-tools.md#memory-cap) - Memory cap behavior
- [Testing Strategy](../../../../guides/testing-strategy.md#runner-prerequisites-and-shared-hosts) - Shared host commands
- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
