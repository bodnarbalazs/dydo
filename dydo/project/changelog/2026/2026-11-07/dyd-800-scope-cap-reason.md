---
area: general
type: changelog
date: 2026-11-07
---

# dydo 3.6.0 - Memory-cap findings distinguish the scope limit from the exhausted budget

## Diagnostic contract

[DYD-800](https://linear.app/bodnar-balazs/issue/DYD-800) corrects the Linux cap-hit
reason. An increasing cgroup `oom_kill` counter proves a hit, but the configured
`MemoryMax` does not identify which budget triggered it. An enclosing slice may
exhaust its budget while the scope records zero `max` and `oom` events.

The reason labels the number as the configured scope limit and says an enclosing
slice budget may have triggered the kill. It makes no claim about a particular
ancestor. The row remains `failed`, exit 1, with `memoryCapHit: true`; stderr,
coverage-derived test rows and the direct .NET test adapter carry the same finding.
Windows Job-object findings keep their existing wording. Limits, slice selection
and hit detection are unchanged.

## Regression and reach

The existing `test_a_row_that_raises_the_oom_kill_count_fails_as_a_cap_hit` uses
scripted cgroup events with zero `max` and `oom` and an increasing `oom_kill`.
It checks the row and stderr for test exits 0 and 137 and static exits 0 and 130.
The same test class runs against both the canonical facade and its generated example.
Existing derived-suite and direct-adapter assertions check propagation; the Windows
message tests retain their exact original reason assertion.

Reach inspection also covered the facade tests, example producer and its tests,
both testing-strategy guides, the assurance-adoption steps and historical memory-cap
changelog entries. The guides' exact reason describes Windows and remains correct.
Historical entries describe their original changes. Current Linux guidance lives in
[Coverage Tools](../../../../reference/coverage-tools.md#memory-cap).

## Rerun recipe

Writer proof on base `f330ff8302003169df08b49e5983dadd93c9553f`: the updated
memory-cap regression ran 71 tests and failed 11 assertions before the fix, passed
after it, and failed the same 11 assertions when the production fix was temporarily
reverted. Restoring the fix passed 83 memory-cap/Windows-message tests with one native
Windows enforcement skip. The facade selection passed 216 tests with two platform
skips. The example-producer test and canonical sync check both exited 0.

The decisive red line was that `memory cap hit (configured scope limit
MemoryMax=1048580K; an enclosing slice budget may have triggered the kill): a finding;
do not raise the cap` was absent from `test verdict derived from the coverage row:
memory cap hit (MemoryMax=1048580K): a finding; do not raise the cap`.
Raw logs, tested patches and source hashes are retained in `/var/tmp/dyd800/`
(`red`, `green`, `reverted-red`, `restored-green`, `facade-light`, `example-tests`,
`sync-check`). The directory also holds the exact `dyd800-run.sh` capped wrapper,
locked restoration recipe, discovery counts, dependency-hash proof and initial
working-tree preconditions. No tests ran before locked restoration completed.

From the ticket worktree, restore the committed prerequisites in the captain's common
orders first. Set `TMPDIR=/var/tmp/dyd800-writer` and the .NET variables
`MSBUILDDISABLENODEREUSE=1`, `DOTNET_CLI_USE_MSBUILD_SERVER=0`,
`UseSharedCompilation=false`. Preserve output under `/var/tmp/dyd800/`.

Use this wrapper for the focused commands below:

```bash
systemd-run --user --scope --slice=app-t3code-agents.slice \
  -p MemoryMax=4G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 180 \
  .static-gates/python/bin/python -m unittest discover \
  -s DynaDocs.Tests/coverage/tests -p test_memory_cap.py -v
```

Repeat with `test_windows_memory_cap.py`, `test_testing_facade.py` and
`test_sync_testing_example.py`. For the canonical example check, use the same
wrapper with `.static-gates/python/bin/python
DynaDocs.Tests/coverage/sync_testing_example.py --check`.

The final Python coverage producer runs the whole Python suite because the facade
does not support feature-scoped coverage; its consumer applies the unchanged policy.
Run each final gate through `/var/tmp/dydo-gate-lock.sh`, then the same systemd wrapper
with `MemoryMax=8G` and `timeout 3600`:

```bash
.static-gates/python/bin/python DynaDocs.Tests/coverage/gap_check.py gate static --stack python
.static-gates/python/bin/python DynaDocs.Tests/coverage/gap_check.py gate coverage --stack python
```

Honor `/var/tmp/dydo-heavy-hold`. The coverage suite verdict also supplies the final
Python Light result. Native Windows enforcement is not exercised on Linux; the
portable Windows message doubles check that its diagnostic is unchanged. No live
OOM run is needed for this wording correction. Mutation is unavailable under DYD-774.

## Related

- [Testing Strategy](../../../../guides/testing-strategy.md) - proof placement and platform limits
- [Changelog](../../_changelog.md) - release history
