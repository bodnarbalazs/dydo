---
area: project
type: changelog
date: 2026-11-06
---

# DYD-784 - Require the config-fuzz oracle in CI

The new `statechart-config-oracle` job runs the existing 4000-config, seed-1
comparison with TLC on `ubuntu-24.04`. It selects the runner's installed Temurin
JDK 21.0.13+9 at `/usr/lib/jvm/temurin-21-jdk-amd64`, checks the exact vendor/build
reported by `java --version` and requires `jdk.compiler@21.0.13`. Missing Java,
version drift or a failed probe stops the job; there is no JDK download or installation.
TLC 1.7.4 is downloaded using the unchanged dependency lock and verified against
SHA-256 `936a262061c914694dfd669a543be24573c45d5aa0ff20a8b96b23d01e050e88`.

The job executes the test file directly and passes its TAP report to the existing
`assert-node-tests-ran.ps1` guard with a minimum of one test. A skip, missing report,
empty file, or test failure fails the job. Its TAP report is retained as a CI artifact.
The test's seed, case count, comparisons and allowances are unchanged. Managed
runtime selection still chooses the JRE; release publishing remains tag-only.

## Fix hop - runner pin and sealed inputs

This hop addresses both blockers against
`83356f358ec39bd873d96c8a9147d734c5f227e7`. The helper now selects existing runner
software. `Resources/Statechart/dependency-lock.json` and the skill's `PROVENANCE.md`
are restored byte-for-byte from base `107a0394f1257228527c984511ea51bb723262ae`;
their sealed hashes remain `3ad231072bddc04588d0f2cf1b61a3e62fc38b4ff400ccea5c3b5d41409575ab`
and `94bd4882414f1b3f116b5c04089f823147d45995d9895915a29662bb90d525f8` respectively.
No payload manifest or assertion changes are needed.

The pin comes from GitHub's [Ubuntu 24.04 inventory](https://github.com/actions/runner-images/blob/42d27e4a2c1232e5640a88ed42a974c8918ecd28/images/ubuntu/Ubuntu2404-Readme.md#java),
image `20261026.354.1`, which lists Java `21.0.13+9`. Its
[image build script](https://github.com/actions/runner-images/blob/42d27e4a2c1232e5640a88ed42a974c8918ecd28/images/ubuntu/scripts/build/install-java-tools.sh)
installs Temurin under `/usr/lib/jvm/temurin-${java_version}-jdk-${java_arch}` with
`amd64` for x64. Read-only network inspection confirmed both at that immutable
source commit. Copies are retained in `dyd-784-fix-evidence/runner-image.txt.gz` and
`runner-java-install.sh.txt.gz`; the image build script was not executed locally.

The installed JDK is trusted as part of GitHub's hosted image, like the runner's
PowerShell. Its exact vendor/build is enforced; this is not a claim of a JDK
archive hash check. The repository's download-integrity pattern still applies to
TLC, the only asset this helper downloads. The JDK is not a shipped or downloaded
managed dependency and needs no new entry in the sealed lock. When GitHub updates
the installed build, CI fails until this pin is reviewed against updated runner
metadata. No fallback installation or floating version selection is permitted.

Fix-hop proof is retained separately in `dyd-784-fix-evidence/`. Historical evidence
below belongs to the rejected first candidate and is not proof of this changed tree.

The focused regression first failed on both sealed hashes and on the forbidden
JDK download (`Only the locked TLC download is authorized.`). With the fix reverted,
it failed again, exit 1; `ValidateStatechartPayload` also reproduced the exact
`Statechart payload source drift: skills/engineering/statechart/PROVENANCE.md`
error, exit 1. After restoration, all 13 focused cases passed without skips and
the payload target passed. `StatechartAssetsTests` discovered and passed all 10
cases, zero skips, proving the embedded lock constructor and offline payload
extraction as well as the build-time seal. The real local JRE oracle skipped once;
the TAP guard rejected it with exit 1. That negative check is not hosted execution
proof.

Whole-statechart coverage passed, child and aggregate exit 0: 455 tests, 447 passed,
zero failures, eight skips (seven platform-specific and the JRE-only oracle).
The unchanged module/method coverage policy passed. The suite, LCOV, adapter report
and aggregate receipt are retained as `coverage-suite.tap.gz`,
`coverage-lcov.info.gz`, `coverage-adapter.json.gz` and `coverage-result.json`.
The first static run was **invalid**, child/aggregate exit 2: `Repository inputs
changed during collection`. The writer edited this changelog during that run.
Its complete failure remains in `static-invalid.log.gz`,
`static-invalid-adapter.json.gz` and `static-invalid-result.json`; it is not a pass.
The repeat freezes repository inputs until the static collector exits.
That repeat passed, child and aggregate exit 0; `static.log.gz`,
`static-adapter.json.gz` and `static-result.json` carry its result. Both successful
gate receipts confirm the inherited 8 GiB cap, swap disabled and `OOMPolicy=continue`.
The first combined proof wrapper's exit 1 remains recorded alongside this successful
static repeat; the passing coverage and focused checks were not rerun unnecessarily.
The static repeat used the same shared lock, cap and timeout:
`/var/tmp/dydo-gate-lock.sh systemd-run --user --scope --slice=app-t3code-agents.slice -p MemoryMax=8G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 1800 bash dydo/project/changelog/2026/2026-11-06/dyd-784-fix-evidence/repeat-static.sh.txt`.

Integration reach: the final diff from the accepted base adds the CI job, its
CI-only helper and focused workflow tests. The dependency lock, provenance,
payload manifests, oracle Java/TypeScript source, package configuration and
release workflow have no final diff. The removed JDK archive-pin/extraction tests
belonged to the disallowed download route; installed-runtime rejection tests
replace them. Existing TAP fail/skip/empty checks and TLC integrity rejection
remain. `source-sha256.txt` binds the changed implementation, test and sealed
inputs; aggregate receipts and adapter fingerprints identify the measured dirty
tree on `83356f3`. Raw logs and large reports are compressed losslessly; use
`gzip -dc FILE.gz` to read them. Recipes write fresh uncompressed logs on repeat.

### Repeat the fix-hop proof

Run from the worktree root. The existing Python, PowerShell, JRE/TLC/Pele cache and
restored .NET packages are prerequisites, not permission to install replacements.
All executions use the user systemd bus; gates acquire the shared lock outside the
memory cap. No cap was raised and .NET node reuse is disabled. The revert recipe
temporarily restores the three implicated files from the rejected candidate,
expects both the focused tests and payload target to exit 1, then restores the fix
on exit. Keep the entire worktree frozen while the proof session runs, including
changelog edits: the assurance inventory fingerprints documentation too.

```bash
evidence=dydo/project/changelog/2026/2026-11-06/dyd-784-fix-evidence
prior=dydo/project/changelog/2026/2026-11-06/dyd-784-evidence
cache=/home/balazs/.local/share/dydo/statechart/deps/3ad231072bddc04588d0f2cf1b61a3e62fc38b4ff400ccea5c3b5d41409575ab/linux-x64
cap=(systemd-run --user --scope --slice=app-t3code-agents.slice -p MemoryMax=4G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 120)
"${cap[@]}" /tmp/dyd507-524-venv/bin/python "$prior/verify-cache.py.txt" "$cache"
"${cap[@]}" /tmp/dyd507-524-venv/bin/python "$evidence/sealed-inputs.py.txt"
"${cap[@]}" bash "$evidence/local-skip.sh.txt"
/var/tmp/dydo-gate-lock.sh bash "$evidence/proof-session.sh.txt"
```

`proof-session.sh.txt` holds one shared gate slot while it runs the revert proof,
the focused suite and sealed-byte check under separate 4 GiB / 120 s caps, then
`final-gates.sh.txt` under 8 GiB / 1800 s. The latter validates the payload, builds and discovers the focused
`StatechartAssetsTests`, runs that nonempty selection, then executes the unchanged
whole-stack `gap_check.py gate coverage --stack statechart` and
`gap_check.py gate static --stack statechart` through the prior cache recipe. It
retains separate native logs, the .NET TRX, and both adapter and aggregate receipts.
Whole-stack selection is necessary because the facade has no feature selector.
Mutation remains **unavailable**, as declared in `gap_check.json`; no mutation pass
is claimed. The hosted real-oracle run is still mandatory before merge.

The provider stream failed while the first revert request was queued. On resume,
its session handle was gone, a capped process-argument search found no owned proof
process, and no revert result had been retained. The restored fix and the earlier
13/13 result, sealed-byte proof and successful payload target were intact. Only
the unproved remainder was requeued; its scripts now write their results to disk.

## Historical writer proof - candidate 83356f3

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

## Historical first-hop proof recipe

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
