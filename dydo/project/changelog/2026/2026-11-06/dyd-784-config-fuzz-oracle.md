---
area: project
type: changelog
date: 2026-11-06
---

# DYD-784 - Require the config-fuzz oracle in CI

The new `statechart-config-oracle` job runs the existing 4000-config, seed-1
comparison with TLC on `ubuntu-24.04`. It downloads Temurin JDK 21.0.8+9 and TLC
1.7.4 using the dependency lock and verifies SHA-256 before unpacking or execution.
The JDK hash reuses the repository's reviewed Java build-toolchain pin.

The job executes the test file directly and passes its TAP report to the existing
`assert-node-tests-ran.ps1` guard with a minimum of one test. A skip, missing report,
empty file, or test failure fails the job. Its TAP report is retained as a CI artifact.
The test's seed, case count, comparisons and allowances are unchanged. Managed
runtime selection still chooses the JRE; release publishing remains tag-only.

## Writer proof

Source base: `107a0394f1257228527c984511ea51bb723262ae`, branch
`DYD-784-config-fuzz-oracle`, isolated worktree
`/home/balazs/.t3/worktrees/DynaDocs/t3-92dba75c`. Branch, root, base ancestry and clean
status were verified before edits. All changes are inside the captain's owned paths.

The declared `/tmp/dyd745-statechart` cache disappeared during inspection. The
existing managed cache below supplied the same pinned JRE, TLC and Pele, read-only.
All archive hashes and extracted regular-file bytes were verified before reuse.
Python 3.14.4 and its locked assurance packages came from the existing
`/tmp/dyd507-524-venv`; PowerShell 7.6.9 came from `/tmp/dyd622-pwsh/pwsh`.
Node was 26.13.0. No tool or JDK was installed on Nightfury.

- Red: the focused regression failed with `CI must run the config oracle in a
  required job` before the workflow change. Reverting the workflow after the fix
  reproduced that assertion, exit 1; the restoration trap restored the change.
- Green: nine focused cases passed, zero skips, exit 0. Tests parse the YAML and
  execute its actual commands against real passing, skipped, empty and failing
  Node test files. Provisioning tests replace only the download boundary and verify
  archive extraction, output paths and refusal of changed JDK/TLC bytes.
- Real local oracle: one skipped test under the verified JRE. The TAP guard rejected
  it with exit 1. This is fail-on-skip proof, **not successful oracle execution**.
- Whole-stack coverage: exit 0; 455 tests, 447 passed, zero failed, eight skipped
  (seven platform-specific plus the JRE-only oracle skip). The unchanged coverage
  policy passed. Raw suite output, adapter metrics and the capped run receipt are
  retained as `coverage.log.gz`, `coverage-adapter.json.gz` and `coverage-result.json`.
- Whole-stack static: passed, child and aggregate exit 0; retained as `static.log`,
  `static-adapter.json.gz` (losslessly compressed) and `static-result.json`. Both gate receipts confirm the
  inherited 8 GiB cgroup cap, swap off and `OOMPolicy=continue`.
- Mutation remains unavailable: the facade declares no adopted TypeScript mutation
  mechanism. It is not a passing mutation result.

Raw output and the probe recipes are retained in the adjacent `dyd-784-evidence/` directory.
The `.txt` recipes are captured probe inputs, not new maintained tools.
Bulky raw reports are compressed losslessly with `gzip -n`; `gzip -dc FILE` reads
their original bytes, including the runner's trailing whitespace.
`source-sha256.txt` binds the implementation and focused tests to the measured bytes;
the gate receipts identify the dirty base and their source fingerprint. The exact
changed-path inventory is `changed-paths.txt`. The changelog's scoped `dydo check`
passed with zero errors and warnings (`docs-check.log`).

## Repeat the proof

Run from the repository root. These commands use the same existing cache and
interpreters; if they have been removed, return the missing prerequisite to the
captain rather than installing anything. Every execution is capped; the gate lock
is outside the cap. The gate recipe explicitly disables .NET node reuse.

```bash
evidence=dydo/project/changelog/2026/2026-11-06/dyd-784-evidence
cache=/home/balazs/.local/share/dydo/statechart/deps/3ad231072bddc04588d0f2cf1b61a3e62fc38b4ff400ccea5c3b5d41409575ab/linux-x64
cap=(systemd-run --user --scope --slice=app-t3code-agents.slice -p MemoryMax=4G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 120)
"${cap[@]}" /tmp/dyd507-524-venv/bin/python "$evidence/verify-cache.py.txt" "$cache"
"${cap[@]}" env PWSH=/tmp/dyd622-pwsh/pwsh node --test DynaDocs.Tests/coverage/tests/statechart-config-oracle.test.cjs
"${cap[@]}" bash "$evidence/revert-proof.sh.txt"
"${cap[@]}" bash "$evidence/local-skip.sh.txt"
/var/tmp/dydo-gate-lock.sh systemd-run --user --scope --slice=app-t3code-agents.slice -p MemoryMax=8G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 1800 bash "$evidence/run-gate.sh.txt" coverage
/var/tmp/dydo-gate-lock.sh systemd-run --user --scope --slice=app-t3code-agents.slice -p MemoryMax=8G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 1800 bash "$evidence/run-gate.sh.txt" static
```

The revert proof temporarily restores only `ci.yml` from the pinned base, asserts
the test's exit 1, then restores the current workflow on exit. The local-skip probe
asserts the guard's exit 1; both probe wrappers therefore exit 0 on the expected red.

## Outstanding before merge

The captain must publish the candidate through its authorized integration route and
observe the hosted `statechart-config-oracle` job pass: one test, one pass, zero skips,
with the named 4000-config, seed-1 comparison in the retained TAP artifact. The local
fixture tests cannot prove that the real JDK executes the oracle successfully.
The writer did not push, open a PR, merge, or change Linear status.

The captain repeats the proof recipe. The final merged candidate repeats applicable
capped coverage/static gates and receives the contract's fresh merge-review pair.

## Related

- [DYD-784](https://linear.app/bodnar-balazs/issue/DYD-784) - Owning acceptance contract
- [Testing strategy](../../../../guides/testing-strategy.md) - Proof and shared-host rules
- [Changelog](../../_changelog.md) - Release history
