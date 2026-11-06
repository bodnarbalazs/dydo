---
area: general
type: changelog
date: 2026-11-05
---

# dydo 3.6.0 - The testing facade caps its own memory on a shared host (unreleased)

## Summary

A runaway probe on a host many agents share grew to about 40 GB within a minute, and the OOM
killer ended the user's desktop session ([DYD-722](https://linear.app/bodnar-balazs/issue/DYD-722), found during DYD-640). The
testing facade now caps the memory of every operation that runs children, so no brief has to
remember the cap. A run that hits it fails with a finding. The rule for ad-hoc commands is written
down in the [Testing Strategy](../../../../guides/testing-strategy.md#runner-prerequisites-and-shared-hosts)
and carried by the crew skills ([Coverage Tools](../../../../reference/coverage-tools.md#memory-cap)).

## Changed

- `gap_check.py` caps `all`, `test`, `gate` and `--force-run` once, before the first child, at 8G
  with swap off. `--help` and `capabilities` run no children and stay uncapped. `DYDO_MEMORY_MAX`
  may lower the cap; a higher value exits 2: "a cap hit is a finding; raising the cap is not
  allowed".
- On Linux the facade inherits a cap from an outer scope that already holds it as tightly, with
  swap off and `OOMPolicy=continue`. Otherwise it reruns itself once under
  `systemd-run --user --scope -p MemoryMax=<cap> -p MemorySwapMax=0 -p OOMPolicy=continue
  --expand-environment=no`, at the lower of the cap and any tighter outer limit. The rerun keeps
  the process, stdio, exit codes, signals and argv bytes, `$(` included.
- Without systemd-run, without a user manager, and on Windows and macOS, the cap is `unavailable`.
  The run goes ahead uncapped, prints one stderr warning and records the reason. Row deadlines still
  apply.
- Every `result.json` records a `memoryCap` block: `state` (`enforced`, `inherited` or
  `unavailable`), `limit` in bytes, `mechanism` and `reason`.
- A row during which the capping cgroup records an OOM kill is `failed`, exit 1,
  `memoryCapHit: true`, with the reason `memory cap hit (MemoryMax=<cap>): a finding; do not raise
  the cap`, whatever the child returned. A test row derived from such a coverage row fails the same
  way.
- `run_tests.py` imports the same cap from `gap_check.py` and applies it when run directly. The
  derived `dydo/reference/gap-check.example.py` carries the cap, so adopters get it too.
- The ad-hoc rule is `systemd-run --user --scope -p MemoryMax=8G -p MemorySwapMax=0
  -p OOMPolicy=continue timeout 3600 <cmd>`, probes at 4G with a short timeout, and any gate lock
  outside the cap. code-writer, reviewer, inquisitor, diagnosing-bugs, verify-dydo and
  ticket-captain each carry it in one line; the ticket-captain's crew briefs name it.

No change in this entry renames a term mechanically, so it has no merge normalisations.

## Residuals

Guards stop accidents, not rogue agents.

- Windows has no memory cap. A Job-object memory limit is a separate follow-up.
- In the inherited path, an OOM kill anywhere in the outer scope counts as the row's cap hit.
- When the kernel kills the facade itself, no result is written; the caller sees 137.

## Files Changed

`DynaDocs.Tests/coverage/gap_check.py`, `DynaDocs.Tests/coverage/run_tests.py`,
`dydo/reference/gap-check.example.py` (written by `sync_testing_example.py`),
`DynaDocs.Tests/coverage/tests/test_memory_cap.py`, `DynaDocs.Tests/coverage/tests/test_testing_facade.py`,
`DynaDocs.Tests/coverage/test-associations.json`, `dydo/guides/testing-strategy.md`,
`Scaffold/dydo/guides/testing-strategy.md`, `dydo/reference/coverage-tools.md`, six crew and officer
`SKILL.md` files, and this entry.

## Related

- [Changelog](../../_changelog.md) - Releases and the Upgrade steps rule
