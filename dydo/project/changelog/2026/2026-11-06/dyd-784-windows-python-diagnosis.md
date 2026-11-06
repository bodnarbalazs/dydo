---
area: project
type: changelog
date: 2026-11-06
---

# DYD-784 - Windows nested Node skip diagnosis and fix

The Windows `python-coverage` failure in run `42162831171`, job `127712322688`,
is caused by DYD-784's new test infrastructure. It is not inherited DYD-788 debt.
`test_javascript_metrics.JavaScriptMetricsTests.test_official_metrics_and_failure_contracts`
correctly rejects ten skipped nested Node tests at line 109. The hosted suite
ran 1074 Python tests with one failure; its nested Node suite reported 127 tests,
117 passed, ten skipped, zero failed. No skip allowance or assertion is changed.

## All ten skips

All are registered by `DynaDocs.Tests/coverage/tests/statechart-config-oracle.test.cjs`.
The two loops used `{ skip: process.platform !== 'linux' }`, so Windows skipped
their bodies before probing any prerequisite:

| TAP number | Exact test name |
|---|---|
| 81 | the CI commands accept a passed oracle |
| 82 | the CI commands reject a skipped oracle |
| 83 | the CI commands reject a empty oracle |
| 84 | the CI commands reject a failed oracle |
| 87 | installed runner JDK publishes verified paths without downloading Java |
| 88 | installed runner JDK rejects version |
| 89 | installed runner JDK rejects compiler |
| 90 | installed runner JDK rejects missing |
| 91 | installed runner JDK rejects java-exit |
| 92 | installed runner JDK rejects tlc |

`node_tests.cjs` recursively discovers `*.test.cjs`, `*.test.mjs` and `*.test.js`
under coverage/tests and npm/test. It therefore correctly discovers this new
file. Its discovery needs no scope expansion or patch. The actual oracle's own
JDK guard is not one of these ten skips.

The concrete alternatives were a DYD-784 registration defect, inherited npm
cleanup behavior, or a missing Windows prerequisite. Exact skip names and the
registration predicates identify the first; inherited cleanup cannot explain
zero failed Node tests or these names, and prerequisite checks never ran.

## Tested source and attribution

Checkout and the job's preflight both identify tested merge
`2816d179cc8db21a118084e35e6b7ea9f3f347ab`, tree
`2d61cf63b83a5108c3067d9b9c0cf14e2fda6ae3`. GitHub's commit API confirms parents,
in order:

1. Feature tip `cbfe6f772835c574da9e0304cc237b19e2368d7b` (DYD-788 landed).
2. PR head `b1ec72e005b326088d885efe6c16b6cd4c9ba3d2`.

Read-only `git ls-remote` matched those feature and PR branch tips during diagnosis.
`attribution.json` records SHA-256 comparisons fetched at these exact commits:

- The oracle regression file is absent at `cbfe6f77`; PR head and tested merge
  both hash to `849ed04edbc92bfaa937e9dca3207c5316f8d73d6e0c252f2c43f3a6685242b3`.
  Its two introduction hops are `83356f35` and `4c024529`.
- `node_tests.cjs` and `test_javascript_metrics.py` are identical in all three trees.
- `npm/test/launcher.test.cjs` in the tested merge exactly matches `cbfe6f77`
  (`f83326beeb3f46e8c67b127ad1c1a43b68e87a8bdb0900539ac8d3e4dbd3de15`),
  while the PR head has the older `f8ca6adb...` bytes. DYD-788 was already tested.

Before handoff, the live feature tip advanced to
`5580564577e10e42dedbe429d2f0197894742777`, the DYD-658 merge. GitHub's comparison
confirms it is ahead of `cbfe6f77`, with no commits behind; its changed paths are
`DynaDocs.Tests/coverage/python_coverage.py`, its focused Python test, and
`dydo/reference/coverage-tools.md`. None overlaps this fix's workflow or Node
regression file, but the Python campaign's producer has changed. The next hosted
Windows campaign must therefore test the fresh merge ref. `current-feature-tip.json`
retains that comparison. The writer branch remains a descendant of `b1ec72e`;
this fix does not merge the newer feature parent.

## Fix and proof boundary

The oracle command explicitly uses PowerShell, already present in both hosted
platforms and already required by its guard and provisioner. Its TAP destination
is quoted. The regression executes that actual workflow command with GitHub's
native-exit propagation and real passing, skipped, empty and failing Node files.
Temporary paths deliberately contain spaces.

Provisioning tests replace the POSIX executable fixture with a PowerShell function
for only the fixture's absolute Java command. The real provisioning script still
checks the file, version, compiler, native exit value and TLC hash, and publishes
paths only on success. The fake download still refuses anything except fixture
TLC. File URLs use `pathToFileURL`; output parsing accepts native CRLF. Neither
test loop has a platform skip. No JDK/tool installation, dependency lock,
provenance, payload, oracle runtime, shared test runner or Python assertion changes.

The bounded Windows-registration replay evaluates only this test file with
`process.platform` set to `win32`; its imported Node modules and PowerShell
still run on Linux. It is a precise counterexample to the skip predicates,
**not native Windows execution proof**. Existing tests themselves are the
regression cases; no source-text check substitutes for their behavior.

Before the fix, replay produced 13 tests, three passes, ten skips, Node exit 0;
the existing zero-skip TAP guard exited 1. Reverting the fix reproduced the same
result. Restoring it produced 13 passes, zero skips, Node and guard exits 0.
The native Linux focused run also passed 13/13. Workflow checks passed 8/8.
The unchanged Python metrics test passed, exit 0, running all 127 Node tests:
126 passed, zero failed, one existing Linux-only skip for
`preserves Node 22 synchronous spawn failure for an invalid Windows executable`.

Hosted job `127712322877` in the original run already passed the real seed-1,
4000-config oracle: one test, one pass, zero skips. Its TAP is retained here.
That is prior-merge evidence, not proof of this changed workflow. Native Windows
zero-skip execution and a fresh hosted real-oracle pass remain required after
review and the captain's next push. This writer did not push, change PR or Linear
state, or merge.

## Gates and evidence

Both mandatory changed-candidate statechart gates passed, child and aggregate
exit 0, with complete measurement and no findings. Coverage ran its suite once:
455 tests, 447 passed, zero failed, eight skips (seven platform-specific cases
and the local JRE-only real oracle). Static passed without an input-change error.
The wrapper verified every frozen tracked hash after both collectors finished.
Payload validation passed; asset discovery was nonempty and all ten tests passed
without skips. Mutation remains unavailable under the existing manifest.

Both adapters identify dirty parent `b1ec72e005b326088d885efe6c16b6cd4c9ba3d2`,
source fingerprint `8da58e5f8eb05b297ddcbef5344700900cdae83cf3923d235e57cd28910b1111`,
inventory SHA-256 `139d23cc191c91e5001c8793f98b9a4f3821576c15fea3bd1a5dd0ced267b620`.
Measured implementation hashes are:

- `.github/workflows/ci.yml`: `79ef54125b4f8c6d3a5d7aeade89cfd477b0180537015008cf6861bba5317abd`.
- `DynaDocs.Tests/coverage/tests/statechart-config-oracle.test.cjs`:
  `da9fe1ee5d71d5584a34c141c6ee2518d97b9c08ddfd4e5b63718758db9bb186`.

The final fix commit packages those exact implementation bytes with this
post-run record and new evidence. Receipts identify the measured pre-commit
tree truthfully; they do not claim that these later evidence files were measured.
The original gate invocation was
`/var/tmp/dydo-gate-lock.sh systemd-run --user --scope --slice=app-t3code-agents.slice -p MemoryMax=8G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 1800 bash dydo/project/changelog/2026/2026-11-06/dyd-784-merge-evidence/gates.sh.txt /tmp/dyd784-windows-diagnosis-b1ec72`.
Its systemd invocation ID is `4abedb0bbeca449e9e354010d4a68e22`; both receipts
confirm the inherited 8 GiB cgroup, swap off and `OOMPolicy=continue`.
Focused/replay probes used 4 GiB caps. No cap was raised.

Unchanged oracle Java/TypeScript, managed runtime, sealed inputs and guard mean
the earlier hosted TAP and source attribution remain useful exact-source evidence.
They do not replace the gates above or the next hosted run. No separate uninstrumented
full TLC suite was rerun. Cache verification checked all 111 extracted JRE files,
418 Pele files, locked archives and 228 installed JavaScript packages; no tools
were installed.

Raw evidence is retained separately in `dyd-784-windows-evidence/`. Compressed
logs are lossless (`gzip -dc FILE.gz`); no historical logs were overwritten.
Original scratch is `/tmp/dyd784-windows-diagnosis-b1ec72`.

## Repeat recipe

Run from this checkout with the already provisioned Node 26.13.0, PowerShell,
Python, .NET and verified JRE/TLC/Pele cache. Do not install replacements.
Use a fresh output directory. The replay temporarily restores two files from
`b1ec72e`, expects the guard's exit 1, and restores their saved fixed bytes in
`finally`. Keep this checkout exclusively owned and frozen until all gates exit.

```bash
out=$(mktemp -d /tmp/dyd784-windows-repeat.XXXXXX)
evidence=dydo/project/changelog/2026/2026-11-06/dyd-784-windows-evidence
cap=(systemd-run --user --scope --slice=app-t3code-agents.slice -p MemoryMax=4G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 120)
/var/tmp/dydo-gate-lock.sh "${cap[@]}" env PWSH=/tmp/dyd622-pwsh/pwsh python3 "$evidence/repeat-revert.py.txt" "$out"
/var/tmp/dydo-gate-lock.sh systemd-run --user --scope --slice=app-t3code-agents.slice -p MemoryMax=4G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 600 bash "$evidence/repeat-focused.sh.txt" "$out"
/var/tmp/dydo-gate-lock.sh systemd-run --user --scope --slice=app-t3code-agents.slice -p MemoryMax=8G -p MemorySwapMax=0 -p OOMPolicy=continue timeout 1800 bash dydo/project/changelog/2026/2026-11-06/dyd-784-merge-evidence/gates.sh.txt "$out"
```

`repeat-focused.sh.txt` verifies existing cache/tool identities, runs focused,
workflow and Python-to-Node checks, and freezes all tracked hashes. The gate
script runs payload validation, nonempty asset discovery and its ten tests,
then the exact mandatory `gap_check.py gate coverage --stack statechart` and
`gate static --stack statechart` commands using the existing cache recipe.
It disables .NET node reuse, saves native logs and adapter/aggregate receipts,
and checks all frozen source hashes afterward. No standalone full TLC suite is
added on top of the mandatory coverage producer's suite.

To repeat hosted attribution, use read-only `gh api` for job `127712322688/logs`
and commit `2816d179cc8db21a118084e35e6b7ea9f3f347ab`. The traceback's stdout is
a Python string literal: decode only the part after `not found in ` with
`ast.literal_eval`, then print TAP lines containing `# SKIP` and the counters.
Do not print the original giant escaped assertion line. `attribution.json`
names each file and source pin for an exact `contents/<path>?ref=<SHA>` replay.

## Related

- [DYD-784](https://linear.app/bodnar-balazs/issue/DYD-784) - Owning ticket
- [Original implementation and integration](dyd-784-config-fuzz-oracle.md) - Earlier proof
- [Testing strategy](../../../../guides/testing-strategy.md) - Gate and evidence policy
