---
area: project
type: changelog
date: 2026-11-04
---

# DYD-504 toolchain modernization

## Hosted Windows cancellation fixture fix — 2026-11-04

This fix starts at clean, pushed `64f1f4bcd8c4287ec3961d5897faadb52152cd3c`
on `DYD-504-modernize-toolchain`. The ticket branch, isolated worktree root,
base ancestry, clean tree and owned-path checks passed before editing. Only
`DynaDocs.Tests/coverage/tests/test_windows_cancellation.py` and this proof change.

Hosted run `41851428407`, Windows job `126648463540`, failed all four native
cancellation cases on `95d591dac64a33d62f22032c530592b7d3ab4ee6`. Each saved
facade receipt (`facade`, `repeat`, `adapter-killed`, `escalation`) reports
`state: invalid`, `aggregateExit: 2`, empty `argv`, null `childExit`, and
`configured gates require a required artifact`. All four stdout files agree;
stderr is empty and no adapter/owner PID files exist. The fixture declared
`artifacts: []`, so the real facade correctly rejected it before launching the
adapter. The listener then obscured that rejection with a 20-second timeout.
These receipts establish a fixture defect, not a native cancellation verdict.

The shared fixture builder now declares required `published/identities.json`,
the actual campaign receipt written by `run_tests._publish_assurance` on normal
completion and cooperative interruption. Forced termination can omit it; the
existing facade interruption path and native escalation assertion retain that
distinction. No dummy artifact or production policy change was introduced.
All four native cases retain their sentinel-survival, held-handle and applicable
receipt assertions. Readiness now checks for facade exit between bounded socket
waits and reports the exit plus stdout/stderr; a live facade still has the same
20-second readiness deadline.

### Focused proof and captain recipe

The portable regression builds exactly the native fixture for all four modes
and invokes the copied real facade's `capabilities` CLI. It expects exit 0 and
`coverage: configured`, without launching the adapter/owner. Before the fix,
all four subcases failed with exit 2 and the hosted diagnostic (unittest exit 1).
After the fix, all ten protocol tests passed (exit 0). Reverting only the
artifact declaration reproduced the same four failures (exit 1), then the
green-tested bytes were restored. Separate regressions exercise real facade
rejection diagnostics, socket readiness and a deterministic live-process deadline.

Run from the repository root with the existing locked CPython 3.14.4 environment:

```bash
PYTHONPATH=DynaDocs.Tests/coverage/tests dydo/_system/.local/static-gates/python/bin/python -m unittest -v test_windows_cancellation test_windows_job test_gate_adapter.CoverageCancellationTests
dydo/_system/.local/static-gates/python/bin/python DynaDocs.Tests/coverage/gap_check.py gate static --stack python
```

The focused selection discovered 69 tests: 21 passed and 48 Windows-native tests
were skipped, exit 0. Those skips include all four native cancellation cases;
they are missing proof, not passes. Python static passed with exit 0, as did
targeted Ruff and the repository's Python cognitive/parameter/nested-ternary
checks on the edited test module. Fixture builder, readiness helper and native
probe cognitive scores are 1, 7 and 8 respectively, below the unchanged limit 20.
The initial socket tests were denied by the sandbox; the authorized loopback
rerun passed. No runtime or dependency changes were needed.

Evidence is retained under
`DynaDocs.Tests/coverage/results/dyd-504-windows-fixture-fix/`: `pre-edit.json`,
`hosted-red.json`, and per-command logs, argv/exits, input SHA-256 manifests and
candidate patches for `red`, `green-loopback`, `reverted-red`, `focused`,
`python-static`, `ruff` and `test-metrics`. The unsuccessful sandbox attempt is
retained as `green`. The original hosted evidence remains under
`results/dyd-504-hosted-windows-failure/`; the static facade receipt is
`results/run-1793804764671-83bcb321/result.json`, relative to the coverage folder.

Only fixture/test inputs and this proof changed. Prior build/viewer/full-stack
evidence and the captain's 24/24 runner-fix replay remain historical evidence
for their unchanged inputs; this hop does not claim a new whole-candidate pass
or rerun those costly campaigns. The captain must push, replay the focused
recipe, obtain fresh independent review covering both successor hops, and retain
green hosted Windows evidence for all four cases. Windows B1 remains unestablished.
The 18 DYD-518 findings and three DYD-507 tests remain known failed gates under
their exact dispositions, with no new allowance. Other deferred findings remain
unchanged. Integration into feature tip `14c43837` remains owed; it differs from
the original `42882830` base. This writer commits only; delivery stays with the captain.

code-writer/model: gpt-6-astra (high).

## Hosted Python runner compatibility fix — 2026-11-04

This narrow successor starts at clean `95d591dac64a33d62f22032c530592b7d3ab4ee6`
on `DYD-504-modernize-toolchain`. All five pre-edit checks passed: ticket branch,
contracted isolated root, supplied base ancestry, clean tree and owned paths.
Only `ci.yml`, `ReleaseWorkflowTests.cs` and this proof change in this hop.

Hosted CI run `41851428407`, Linux cancellation job `126648462928`, failed before
native tests: `actions/setup-python@v5` could not find Python 3.14.4 x64 for
Ubuntu 26.04, the image selected by `ubuntu-latest`. The captain checked the
official Python manifest and confirmed Ubuntu 24.04 support. The cancellation
matrix now uses `[ubuntu-24.04, windows-latest]`. Node 26.13.0, SDK 10.0.303,
Python 3.14.4 and shared runtime 10.0.14 remain unchanged.

Inspection of the reached release workflow found its sole Python setup in
`validation` on `windows-latest`. Its Linux AOT build matrix does not set up
Python; no release runner change or release execution was needed. A new workflow
regression asserts that Python remains confined to the supported validation job.

Round-three nit 7 is addressed in code: the cancellation workflow regression
checks the supported Linux image, retained Windows matrix leg, matrix dispatch,
`.python-version`, exact native unittest invocation and unconditional execution.
Ten negative fixtures reject floating/unsupported Linux images, a dropped Windows
leg, bypassed matrix dispatch, missing/wrong version-file configuration, a
commented command and each omitted native test module/class. Each fixture first
proves the unchanged job valid and its replacement applicable, then requires
the altered job to fail the same assertions.

### Focused proof and captain recipe

Using the existing CPython 3.14.4 interpreter, SDK 10.0.303 and shared runtime
10.0.14, run from the repository root:

```bash
dydo/_system/.local/static-gates/python/bin/python DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~ReleaseWorkflowTests
```

The same command with `--list-tests` discovered 24 cases, exit 0. Before the
runner fix, 11 failed and 13 passed, exit 1; the deciding assertion expected
`[ubuntu-24.04, windows-latest]` but observed `[ubuntu-latest, windows-latest]`.
With the fix, all 24 passed without skips, exit 0. Reverting only the runner
change reproduced the same 11 failures, exit 1; the fix was then restored to
the exact green-tested workflow bytes. No unaffected full suite or coverage
gate was rerun. `git diff --check` passed.

Evidence is retained in `DynaDocs.Tests/coverage/results/dyd-504-hosted-runner-fix/`:
full logs, commands, exits, source hashes and candidate patches for discovery,
red, green and reverted-red, plus the hosted setup-failure log. The initial
sandbox-blocked discovery attempt is retained separately; the authorized retry
passed. Each adapter run owned its own temporary directory and cleaned its test
worktree. Existing evidence archives were preserved.

The one-time hosted status read found viewer successful, the Linux setup failure,
and Windows cancellation/build/full-stack still running. This hop does **not**
claim a hosted pass. The captain owns focused replay, fresh review and hosted
Linux/Windows native cancellation and runtime-provisioning proof, plus delivery.
Exactly the 18 DYD-518 coverage findings and three DYD-507 tests remain known
failed gates under their existing dispositions. All other round-three deferrals
are unchanged; no production defect was repaired or reclassified here.

code-writer/model: gpt-6-astra (high).

## Round-two Windows cancellation successor — 2026-11-04

This section supersedes the round-one B1/N3 closure language below. Windows B1
is implemented with native PR-CI regression coverage; native Windows execution
and fresh review remain owed. Linux cancellation remains proved with real .NET
processes. The original base is still `42882830292f44133f796ee3d152cb446ea4091f`;
this fix starts at clean, pushed `b08b57705d9331ca6a2c6afd9ba6ce08a7bc4354` on
`DYD-504-modernize-toolchain` in the contracted isolated worktree. All five
pre-edit checks passed; `pre-edit.json` retains branch/root/base/status/ownership.

Balazs's 2026-11-04 ruling closes the DYD-517 question and assigns the existing
18 native-Windows Python coverage findings to
[DYD-518](https://linear.app/bodnar-balazs/issue/DYD-518). They remain a **known
failed gate**, permitted for this feature merge only; DYD-518 blocks 3.6.0 landing.
No threshold, exclusion, pass claim, inheritance attribution, aggregation, or
`owned_process.py` ownership expansion is introduced. DYD-518 owns the separate
native Windows coverage job and Balazs-reviewed DR 048 amendment. This hop's CI
job tests cancellation only. DYD-507 retains its distinct three .NET failures.

### Behavior and red proof

Adapter waits now return to Python every 200 ms, so a Windows BREAK handler can
run while the separately grouped owner is busy. The adapter joins an outer
kill-on-close job before launching that owner; killing the adapter cannot abandon
its owner and nested subject job. Cooperative cancellation, forced termination
and descendant cleanup share the remaining cleanup deadline. A cleanup timeout
or unconfirmed owner produces a measurement error, including when a repeated
interrupt lands during cleanup; confirmed cancellation retains exit 130.

The native owner records signals through teardown and receipt writing. It checks
cancellation during supervision, terminates the job, and publishes actual job,
handle and stream observations. An interruption without those observations
retains `cleanup_confirmed: false`. Publication of an interrupted build now lists
unbuilt artifacts instead of losing the receipt while hashing missing binaries;
completed campaigns still require their full artifact set.

Before editing, the protocol tests failed on a 1,770-second adapter wait and
missing owner receipts. The final tests against exact parent production bytes
also fail on the 1,000-ms native wait and repeated interruption. The disposable
overlay retains all output; it simulates OS boundaries and is not Win32 proof.
The real Linux early-build probe exposed the missing-binary publication failure;
`red-interrupted-build` reproduces it before the fix and the captain recipe
passes afterward.

Implementation checks also caught and repaired a no-deadline regression: a
200-ms wait expiration must continue waiting when no row deadline was supplied.
All affected adapter callers are included in the final Light recipe. The existing
Linux forced-escalation fixture now allows three seconds for total cleanup;
its old 0.1-second override had shortened only cooperative waiting while leaving
separate force/descendant allowances. It retains every real-process assertion.

### Native proof and CI

The new `test_windows_cancellation` regression drives the real facade, adapter,
assurance owner and native job with a real subject and separately grouped child.
Socket readiness puts signals after process creation; held process handles prove
exit, and an unrelated sentinel must survive. Four Windows cases cover facade
cancellation, repeated BREAK during native teardown, unresponsive-owner escalation,
and adapter death. Cooperative cases require the published incomplete-but-cleaned
receipt, empty job, completed root/retained handles and drained streams. Forced
escalation never invents an owner receipt.

`.github/workflows/ci.yml` job `assurance-cancellation` runs the native tests on
`windows-latest` and preserves the Linux cancellation regression on `ubuntu-latest`.
It uses `.python-version` (3.14.4), Node 26.13.0 and existing locked dependencies;
it uploads cancellation evidence even on failure. It consumes no .NET executable;
the existing .NET CI runtime provisioning is unchanged and its workflow tests pass.
Native command from the repository root:

```text
PYTHONPATH=DynaDocs.Tests/coverage/tests
python -m unittest -v test_windows_cancellation test_windows_job test_gate_adapter.CoverageCancellationTests
```

Set `PYTHONPATH` as an environment variable on Windows, as the CI job does.
`DYDO_CANCELLATION_EVIDENCE` optionally names the retained output directory.
Windows results are **awaiting hosted PR CI**, not inferred from Linux skips.

The real Linux adapter → run_tests → csharp_coverage → dotnet probes preserve
exit 130 and `cleanup_confirmed: true`: repeated early-build interruption exits
in 0.36 s; interruption during actual tests exits in 0.26 s. Both leave no marked
live process and remove their owner worktree. The later probe still reproduces
the nested `dydo-interrupt-spelling` registration residue, which remains an open
nit. Its residue was archived; cleanup's dry run refuses a detached test tree
under T3's root. The retained tree was moved to `/tmp/dyd-504-round2-retained-nested`
without discarding files; subsequent normal runner pruning removed its stale
registration. A second probe's registered nested residue remains under
`/tmp/dyd-504-round2-native/tmp/`; the underlying cleanup nit is not repaired.

### Evidence and finding dispositions

Persistent evidence is `DynaDocs.Tests/coverage/results/dyd-504-fix-round2/`.
Command directories carry argv, exit, full output, input hashes and candidate
patches. `sources.tar.gz`, `commands.tar.gz`, `gate-results.tar.gz` and
`SHA256SUMS` preserve source and evidence; `commit.json` binds the successor SHA.
Original round-one and reviewer archives remain intact. Initial sandbox-restricted
runs and attempts invalidated by nested probe artifacts are retained as failed or
interrupted attempts, never credited as final gates. Final discovery has 135
unique evaluated C# test Compile identities after scratch was removed from the
checkout. Final executable-input compatibility is checked against the commit;
the only later difference is this proof index.

| Final proof | Exit | Observation |
|---|---|---|
| Affected Light / captain recipe | 0 | 171 tests: 123 passes, 48 platform skips |
| Workflow/runtime provisioning regression | 0 | 12 .NET tests, no skips |
| Python static | 0 | Zero findings and gaps |
| Shared clone/static row | 0 | Node static, including C#/Python/JavaScript clones: zero findings and gaps |
| Python coverage suite | 0 | 643 tests: 587 passes, 56 platform skips, zero failures |
| Python coverage policy | 1 | Exactly 18 DYD-518 findings, zero gaps |
| Native Windows cancellation | Not run locally | Four new native cases await the PR-CI job |

The final Python static and coverage source fingerprint is
`de315877f12cf2b84914eb7f172902da2aeeb6290e7d8d42d331289b5416a718`.
The coverage facade receipt is `results/run-1793802214043-44ed31ff/result.json`;
Python static is `results/run-1793802199157-94b0278c/result.json`.
`coverage-findings.json` compares the named findings (ignoring shifted line
numbers) against the retained round-one report: the same 18 identities, no new
finding. This is not comparison with original base coverage and is not inheritance
attribution. Final measurements are:

- `run_tests.py`: `_end_job_members` HCRAP 44 (one finding).
- `windows_job.py`: line 35.924933%, branch 24.193548%; `members` HCRAP 33,
  `retain_members` 32, `poll_pipe` 47, `close_pipe` 43, `make_pipe` 22, `pipes` 29,
  `launch` 40, `finish` 236, `wait_campaign` 113, `supervise` 55, `native_run` 31
  (13 findings). The changed native methods remain within the explicitly named
  DYD-518 failed-gate disposition; their numbers are not hidden or relabeled.
- Unchanged `owned_process.py`: line 17.948718%, branch 0%; `supervise` HCRAP 29
  and `run_owned` 55 (four findings).

No new whole-.NET, Node or viewer coverage verdict is claimed by this hop.
Earlier full-gate receipts remain historical; their unchanged runtime/product
inputs are not a substitute for the fresh affected Python gates above. The
12-test workflow run reaches the new cancellation job's pinned declarations
and the existing patched-runtime provisioning assertions. Native tests exercise
processes; portable protocol tests alone do not establish Win32 delivery.

Focused captain recipe (local locked assurance interpreter; run as a separate
unittest process, because the existing DYD-516 fixture mutates its own parent):

```bash
PYTHONPATH=DynaDocs.Tests/coverage/tests \
  dydo/_system/.local/static-gates/python/bin/python -m unittest -v \
  test_windows_cancellation test_gate_adapter test_windows_job test_runner
```

The real-chain recipe is the retained `real_chain_probe.py`, using a fresh absolute
scratch path under `/tmp`, delay `5` plus repeat `0.1` for build interruption, or
delay `35` for mid-tests; inspect the recorded process samples, receipt and absence
of marked live processes rather than treating its observer exit alone as success.
Only the repository's normal isolated test runner creates worktrees.

DYD-514 overlap is limited to adapter forced-wait/descendant-cleanup error
classification and remaining budgets. Its separate runner-final-cleanup
classification is not claimed fixed. DYD-516/N3 remains open: the other
missing-producer fixture still changes its parent subreaper state. New probes
are isolated in disposable subprocesses; this hop does not repair that fixture.
[DYD-516](../2026-11-05/dyd-516-missing-producer-campaign-isolation.md) closes it:
the fixture now runs its campaign in a child interpreter.
N4 budget/category mismatch, N5 README/npm prerequisites and N6 AOT-only assessment
remain retained/deferred at their existing severities. The nested-interruption
residue nit remains open. Fresh independent review, hosted CI and captain proof
rerun remain owed. No children, installs, global defaults, push, PR, merge, release,
tag or publication were performed by this writer.

code-writer/model: gpt-6-astra (high).


## Round-one fix successor — 2026-11-04

This section supersedes the Python inheritance claims in the retained hops.
The combined Claude/Codex FAIL against `dbd16297d1feac0512bf28b4be5448642fc3dc36`
is binding. B1 is repaired and proved locally; B2 remains an acceptance blocker.
No threshold, exclusion, tier or native-Windows claim changed. No comparable
coverage measurement of original base `42882830292f44133f796ee3d152cb446ea4091f`
exists in this evidence. Replaying our own `43a016ba` hop and comparing source
bodies cannot establish inheritance.

All five pre-edit checks passed: the specified ticket branch and isolated root,
the exact parent HEAD, original base ancestry, an empty status, and owned paths.
`pre-edit.json` records those observations. This hop changes only the two coverage
runner modules, their three reached Python test files, and this proof index.
`owned_process.py`, guard/product code, DYD-507 tests, runtime pins and dependencies
are unchanged. No installs, global defaults, children, branch switch, merge, push,
PR, tag, release or publication were performed.

### Cancellation repair and reproduction

The adapter forwards interruption to its separately grouped campaign, defers
repeat interrupts during bounded cleanup, waits for the owner, and escalates
within the facade's cleanup allowance. On Linux the adapter is a subreaper so
detached descendants of a forcibly terminated owner remain reachable. Cleanup
targets its own descendants. Windows keeps CTRL_BREAK forwarding and its native
owner; the adapter now installs the corresponding Python interrupt handler.
An owner cancellation with confirmed cleanup retains its published evidence and
returns 130. A campaign's own exit 130 survives the adapter unchanged.

The original reviewer probe, with only source/evidence paths adapted, reproduced
adapter exit -2, live owner and subject, and no receipt on the parent. Its strict
successor replay stops with `ProcessLookupError` because its observation assumes
the owner still exists; that replay is retained and is not labeled a passing test.
The permanent equivalent regression observes absent processes correctly and also
tests a detached descendant, the real facade, direct owner interruption, a direct
campaign interruption, forced escalation, and an unrelated sibling process.
All five entries return 130, leave no live owned process, and preserve the sibling.
Cooperative entries retain `cleanup_confirmed: true` receipts. Forced escalation
has no fabricated owner receipt. A parent-source overlay makes the regression red
(including owner exit 2); restoring the successor makes it green. Full original,
overlay and successor output and observer recipes are retained.

### Portable validation and remaining scope

Eight existing platform-neutral owner tests now execute on Linux, plus a new
valid-request/deadline case. Only the reported OS identity is replaced at the OS
boundary for these validation tests; no Win32 API is simulated and no native
containment proof is claimed. Real Win32 execution stays Windows-specific.
Only the reviewed in-process failure fixture was fixed here: it now stubs
containment so it cannot turn the shared unittest process into a subreaper. The
missing-producer fixture still ran the real Linux campaign in the unittest
process; DYD-516 closes that N3 residue.

| Final evidence | Exit | Observation |
|---|---|---|
| `captain-recipe` | 0 | 16 focused tests, no skips; five native cancellation entries |
| `final-python-static` | 0 | Zero findings and gaps; final executable inputs |
| Python coverage suite | 0 | 632 cases: 580 passes, 52 skips, zero failures |
| Python coverage policy | 1 | 18 threshold findings, zero gaps |
| .NET coverage | 1 | 3,596 passes, exactly three DYD-507 failures, no skips; one functional finding, zero gaps |
| Node coverage | 0 | 91 passes, one Windows skip; thresholds pass |
| Viewer coverage | 0 | 389 passes, no skips; thresholds pass |
| Four-stack coverage aggregate | 1 | Above failed rows remain failed |

The .NET owner records complete execution and confirmed Linux subreaper cleanup;
its snapshot is removed and deregistered. Functional failures prevent a passing
.NET threshold claim. The source-pinned final facade receipt is
`results/run-1793797383164-ab4aaf9d/result.json`; final Python static is
`results/run-1793797654422-51a8372c/result.json`.

Remaining B2 findings, at unchanged 80% line / 60% branch / HCRAP 20:

| Module | Remaining measurements |
|---|---|
| `run_tests.py` | `_end_job_members` HCRAP 44 (one finding) |
| `windows_job.py` | Line 31.0541%, branch 18.9655%; 11 HCRAP findings below (13 total) |
| `skills/engineering/verify-dydo/scripts/owned_process.py` | Line 17.9487%, branch 0%; `supervise` HCRAP 29 and `run_owned` 55 (four total) |

Windows-owner HCRAP findings are `members` 33, `retain_members` 32, `poll_pipe` 47,
`close_pipe` 43, `make_pipe` 22, `pipes` 29, `launch` 40, `finish` 236,
`wait_campaign` 92, `supervise` 41 and `native_run` 31. The validation finding
(previously 239.792) is gone. Every changed production method meets HCRAP and
cognitive limits; no new threshold finding was introduced. Exact floating-point
measurements and member identities are in `summaries/python-coverage.json`.

B2 needs native Windows execution and an explicit admiral/DR disposition for the
unchanged per-module policy on a Linux run. Production changes to the verification
driver's `owned_process.py` still need ownership expansion. These failures are
not attributed to the base and are not covered by DYD-507's narrow disposition.

Severity dispositions: B1 repaired locally, with native Windows/hosted CI still
owed; B2 open. Should DYD-514 remains separate: `run_tests()` final cleanup and the
owner's unconfirmed-cleanup classification are unchanged. This hop does not claim
to fix it. N3 was addressed only for the reviewed failure fixture; the
missing-producer fixture's mutation stayed open until DYD-516. N4's requested-budget
and category mismatch, N5's README/npm prerequisites, and N6's AOT-only alternative
assessment remain deferred to their owners; no scope or severity was silently changed.

### Evidence identity and captain recipe

New persistent evidence is under
`DynaDocs.Tests/coverage/results/dyd-504-fix-round1/`. The local hook refused direct
access to `dydo/_system/**` despite the brief's authorization, so original archives
remain untouched there; the reviewer's existing `/tmp` copy supplied the original
probe. `commands.tar.gz`, `gate-results.tar.gz` and checked `SHA256SUMS` preserve
the new full logs, source patches/hashes, commands, actual exits, environments,
raw receipts and original reviewer files. `commit.json` binds the final candidate.

The initial sandbox runs are diagnostic only: shared Git metadata was read-only
and a sandbox-only empty `/tmp/.git` redirected facade fixtures. They were stopped
with raw exits 130 and -2. The host preflight passed before the corrected runs.
The only executable-input delta after the .NET campaign snapshot was the final
Python owner-test file. `input-compatibility.json` records both hashes and changed
function identities. The .NET snapshot contains final runner/adapter bytes and
unchanged C# inputs; final Python coverage/static inventories contain the final
test bytes. The ordinary 631-case Python run predates those last additions; the
final coverage producer supplies the final suite verdict. Only this proof index
changes after the final executable-input freeze.

Parent build and .NET/Node/viewer static receipts remain credited only for their
unchanged measured sources, configuration, dependency and runtime inputs, with
the comparison recorded. They are not newly executed results. All four coverage
rows were rerun because the shared cancellation adapter changed. Mutation remains
unavailable under the existing manifest; hosted CI, native Windows, captain rerun
and fresh independent review remain captain obligations.

Run from this worktree with the installed dependencies and host filesystem access
(the native facade fixtures need isolated Git worktree creation):

```bash
PYTHONPATH=DynaDocs.Tests/coverage/tests dydo/_system/.local/static-gates/python/bin/python -m unittest \
  test_gate_adapter.CoverageCancellationTests \
  test_gate_adapter.GateAdapterTests.test_coverage_subprocess_gets_one_remaining_timeout_and_cooperative_cleanup \
  test_gate_adapter.GateAdapterTests.test_coverage_subprocess_force_terminates_after_cleanup_budget \
  test_runner.LinuxCampaignTests \
  test_windows_job.WindowsJobContractTests \
  test_windows_job.WindowsJobPlatformTests
```

This recipe exercises native cancellation and detached cleanup, deadline cleanup,
Linux owner completion/failure and portable validation. Full gate commands and
per-row raw receipts are in the named command directories. No self-review PASS
is claimed.

code-writer/model: gpt-6-astra (high).


## Distribution successor — retained disposition, 2026-11-04

This section supersedes the runtime-install blocker and final-.NET-work statements
in the retained hops below. The captain completed DYD-513: Microsoft.NETCore.App
10.0.14 is installed alongside 10.0.11; SDK 10.0.303 and ASP.NET runtime 10.0.11
remain unchanged. The install receipt is `captain-04d05fb7/runtime-install.log`.
This writer installed no SDK, runtime or package and changed no global defaults.

### Contract and implementation

Before editing, `provisioning/pre-edit.json` records clean branch
`DYD-504-modernize-toolchain`, the contracted isolated worktree, HEAD
`04d05fb7d67b6a66635c9a49d48085db6c36657b`, ancestry through retained
`43a016ba9ada3ae8b8615f0a55646fd3299d4c90` to original base
`42882830292f44133f796ee3d152cb446ea4091f`, and the explicitly owned paths.
The live DYD-504 contract matched the supplied 12:16:41.953Z version. All four
retained archive checksums were verified; no original evidence was overwritten.

All five .NET setup sites now use an isolated SDK directory and immediately run
`.github/workflows/provision-dotnet-runtime.ps1`: CI build/test and full-stack;
release matrix build, Windows validation, and NuGet packing. Microsoft's Windows
or Unix installer adds only runtime 10.0.14 for the host architecture, preserving
non-versioned host files and PATH. The script checks exact SDK 10.0.303 before and
after installation and requires the selected host to list runtime 10.0.14.
Release cross-compilation still selects its target RID independently. Release
workflows, tags and publication were not executed.

The named workflow tests were declared before implementation. Twelve focused
workflow/runtime cases pass. With both workflows reverted to parent bytes, the
two new provisioning cases fail with `Missing release step 'Provision patched
.NET runtime'`; the workflow bytes were restored. Negative fixtures reject a
missing or commented invocation, wrong shell, unpinned SDK, missing isolated
install directory, and provisioning before SDK setup or after its consumers.

### Distribution evidence

The locally packed `dydo.3.5.1.nupkg` retains the repository's current version;
it was inspected without installation or publication. Its
`tools/net10.0/any/dydo.runtimeconfig.json` requests Microsoft.NETCore.App 10.0.14
with `LatestMinor`. This deliberately raises the .NET tool minimum, as do ordinary
framework-dependent build/run outputs and the CLI copied to test output. The
3.6.0 Upgrade steps now tell users to upgrade their shared runtime first.

`provisioning/distribution-probes` records six direct launch expectations. Using
an isolated host with links to already-installed runtime files, built dydo,
the test-output child and the unpacked tool each exit **150** when only 10.0.11 is
visible, and each exit **0** after 10.0.14 is visible. The isolated host was removed;
no runtime installation or global tool installation was performed. The package
hash, all runtimeconfigs and launch logs are in `provisioning/distribution/`.

Fresh unsuppressed RID restore passes. Its assets resolve both
`Microsoft.DotNet.ILCompiler` and `runtime.linux-x64.Microsoft.DotNet.ILCompiler`
at 10.0.14, with Host, Runtime and Runtime.NativeAOT download dependencies at
10.0.14. Package SHA-512 identities are recorded in `distribution/result.json`.
The security pin remains unchanged and no audit or warning suppression was added.

Inspection of `release.yml`, `npm/lib/platform.js`, `npm/lib/download.js`,
`npm/bin/dydo.cjs` and `npm/install.js` establishes the other routes: the five
win-x64, linux-x64, linux-arm64, osx-x64 and osx-arm64 release archives are
self-contained NativeAOT outputs; npm downloads and executes those same assets.
They embed the patched runtime and need no separately installed shared runtime.
npm's unsupported-platform fallback points to the .NET tool and inherits its
minimum. The viewer is an embedded bundle, not another .NET distribution.
No native archive was built or republished by this hop; this route evidence is
workflow/downloader inspection plus resolved AOT package evidence.

### Final gates and receipt reuse

All command receipts below live under `dydo/_system/.local/dyd-504-proof/provisioning/`
and retain exact argv/cwd, timestamps, exit, environment, input SHA-256 manifest,
source patch and full output. Preflight discovered 3,441 .NET identities, expanding
to 3,599 executed cases. Installed dependency/version collectors pass on CPython
3.14.4, Node 26.13.0 and SDK 10.0.303. Ordinary and coverage runners own their
isolated worktrees/processes; their outer deadlines are 25 minutes.

| Receipt | Exit | Observation |
|---|---|---|
| `focused-green` | 0 | 12 workflow/runtime cases passed |
| `workflow-revert-red` | 1 | Both provisioning cases reject parent workflows |
| `fresh-audit-restore` | 0 | Patched AOT dependencies; audit enabled |
| `final-build` | 0 | Release solution build, zero warnings/errors |
| `local-tool-pack` | 0 | Local-only .NET tool package |
| `distribution-probes` | 0 | All six expected launches: three 150, then three 0 |
| `final-dotnet-suite` | 1 | 3,596 passed, three DYD-507 failures, zero skips |
| `final-dotnet-static` | 0 | Zero findings and zero measurement gaps |
| `final-dotnet-coverage` | 1 | 3,596 passed, same three DYD-507 failures, zero skips; one functional finding, zero gaps |
| `documentation-check-final` | 0 | Final documentation check |

The ordinary-suite failures remain exactly
`GapCheckCommandTests.LauncherCancelsTheRunnerProcessTreeAndWaitsForItsDirectChild`,
`CliScenarioProcessTests.OrphanedPipe_ReportsUnconfirmedCleanupAndRetainsScratch`,
and `CliScenarioProcessTests.Timeout_TerminatesAndReapsParentAndDescendant`.
Their comparable base evidence and unchanged-source attribution are retained
below. DYD-507 owns them; its admiral disposition permits individual feature
merges with these failures documented, not a passing-suite claim. The ordinary
worktree was removed and deregistered and its recorded lingering child was reaped.
The coverage campaign also completed with those same three failures, 3,596 passes
and zero skips. Its owner records `complete: true`, `cleanup_confirmed: true`,
`containment: linux-subreaper`, CPython `[3,14,4]` and subject status 1 in
`results/assurance/run-48794e86e65b4ec09a388490f6b56dce/`
`raw-1b40b37f916344628b8951a224413e61/identities.json`. Its single functional
finding prevents a passing .NET coverage/HCRAP threshold claim. The .NET static
facade receipt is `results/run-1793794906070-03c14cf4/result.json`; coverage is
`results/run-1793795066370-d2f8c310/result.json`.

The brief explicitly authorizes reuse of unaffected Python/Node/viewer receipts.
`provisioning/input-reuse.json` lists every full-manifest difference and the exact
row scope credited. No measured Python/Node/viewer source, test, dependency lock
or runtime pin changed in this hop. Reused results are: Python 630 tests with
zero failures and 60 platform skips (suite exit 0), Node 91 passed and one Windows
skip, viewer 389 passed, and all three static rows with zero findings/gaps.
Node and viewer coverage thresholds pass. Only the successful Python/Node rows
of the old mixed facade runs are reused, never their failed aggregate. Current
.NET static plus those compatible rows account for the static obligation; the
full four-stack facade was not rerun redundantly.

**That hop failed Python coverage with 19 threshold findings and zero gaps,
exit 1. Inheritance was unproven and is withdrawn.** The checksum-verified replay on 43a016ba and byte-identical
flagged-method comparison to 42882830 remain in `recovery/`. No threshold,
exclusion, waiver or policy changed. The captain/admiral owns this unresolved
blocker; DYD-507's exception does not cover it. Coverage overall remains failed.

### Handoff and focused recipe

The successor commit binding, per-file hashes and post-freeze documentation-only
delta are in `provisioning/commit.json`. New `provisioning-commands.tar.gz` and
`provisioning-coverage-results.tar.gz`, checked by `PROVISIONING-SHA256SUMS`, retain
this hop's evidence alongside all original archives. These ignored artifacts
must travel with the handoff before removing the worktree. The proof index is
the only tracked file changed after the executable-input freeze.

Captain recipe from the worktree root, with the existing local dependencies:

```bash
py=dydo/_system/.local/static-gates/python/bin/python
dotnet --list-sdks
dotnet --list-runtimes
dotnet build DynaDocs.sln -c Release --warnaserror
"$py" DynaDocs.Tests/coverage/run_tests.py -- -c Release --filter FullyQualifiedName~ReleaseWorkflowTests
dotnet bin/Release/net10.0/dydo.dll --version
dotnet DynaDocs.Tests/bin/Release/net10.0/dydo.dll --version
```

For distribution reinspection, run `dotnet pack DynaDocs.csproj -c Release
--no-build --no-restore -o /tmp/dyd-504-tool-inspection`, then read
`tools/net10.0/any/dydo.runtimeconfig.json` from that local nupkg. The exact six-case
launch recipe is retained as `provisioning/distribution_probe.py`; it expects a
fresh evidence destination. The raw gate receipts provide exact rerunnable argv.

Remaining: the Python 19-finding disposition/repair; hosted PR CI and native
Windows/platform proof; the captain's focused rerun, fresh independent review,
and later target update/integration. PowerShell is absent on this Linux host, so
the provisioning script itself has not been executed here; workflow assertions
and local runtime/SDK launches do not substitute for hosted installer execution.
The 60 Python and one Node platform skips remain missing Windows proof. No PR,
branch switch, merge, push, release, tag, publication or child agent was used.

code-writer/model: gpt-6-astra (high). No self-review PASS is claimed.

## Recovery successor — retained disposition, 2026-11-04

This section supersedes the unfinished-scope and routing statements in the retained
hop below. The original archives and their checksum file remain unchanged. Before
editing, the successor writer verified clean branch `DYD-504-modernize-toolchain`,
root `/home/balazs/.t3/worktrees/DynaDocs/t3-7a947183`, retained HEAD
`43a016ba9ada3ae8b8615f0a55646fd3299d4c90`, original base ancestry
`42882830292f44133f796ee3d152cb446ea4091f`, and owned paths. Both archive hashes,
every hash in `commit.json`, and the original tested input manifest were checked;
only its two declared documentation deltas differed. The captain explicitly added
the npm ZIP fixture, viewer storage setup and directly reached regression tests.
The subsequent advisory addendum added `DynaDocs.csproj` and reached pin assertions/docs.

### Implemented behavior and red/green proof

- The npm extraction test writes a deterministic valid ZIP containing `dydo.exe`
  with `binary fixture` bytes. It still invokes real platform extraction and checks
  the extracted content. It needs no ZIP writer package or GNU-tar ZIP support.
  `zip-red-native` fails with `End-of-central-directory signature not found`;
  `zip-green-native` passes all seven tests, exit 0.
- Browser tests use the actual jsdom local/session storage areas, replacing Node
  26's host globals only in jsdom environments. Existing Node-environment fixtures
  still run. Five new tests cover browser Storage identity, window/global access,
  key/remove behavior, restoration after global stubbing, and separate storage areas.
  `storage-revert-red` fails four of five new tests against retained setup;
  `storage-final-focused` passes all 85 selected tests, including the 19 Node-only
  fixture cases. `viewer-stable-coverage` passes all 389 tests.
- `RuntimeFrameworkVersion` is now 10.0.14, the first patch fixing
  GHSA-p3jp-9v83-76w9 / CVE-2026-111497. LatestMinor roll-forward remains in place.
  Fresh unsuppressed restore of the old declaration fails NU1903 (`advisory-red`);
  patched restore and Release build both pass. The runtime-pin assertion fails
  against the old declaration (`runtime-pin-revert-red`) and all nine workflow/pin
  tests pass (`patched-pin-test`). No audit/NoWarn suppression was added or used
  for the fresh vulnerability restore/build proof.

The RID restore resolves `Microsoft.DotNet.ILCompiler` and
`runtime.linux-x64.Microsoft.DotNet.ILCompiler` at 10.0.14. Its download dependencies
resolve Microsoft.NETCore.App Host, Runtime, Runtime.NativeAOT and
Microsoft.AspNetCore.App Runtime for linux-x64 at 10.0.14. Saved assets and package
SHA-512 receipts are in `recovery/patched-resolution/`. These are package-cache
restores, not shared-runtime installation. The eight Python packages and all JS
locks remain unchanged; the resolved-version collector passes on CPython 3.14.4,
Node 26.13.0 and SDK 10.0.303. No new package, runtime or global default was installed.

### Runtime blocker and captain handoff

**Approval/install prerequisite: Microsoft.NETCore.App runtime 10.0.14, linux-x64,
into the existing `/home/balazs/.dotnet` installation.** Both built dydo and the
copy in the test output request 10.0.14 in their runtimeconfig. Direct framework-
dependent launches both exit 150 and identify only 10.0.11 as installed. The test
host itself targets 10.0.0 and runs on 10.0.11, but its dydo children cannot launch.
No newer SDK or ASP.NET shared runtime is required by this measured launch failure.
No install was attempted. The captain must obtain Balazs's authorization before
provisioning, then complete the dependent final gates. CI setup currently pins SDK
10.0.303; it does not establish availability of shared runtime 10.0.14. The approved
CI provisioning choice and hosted checks remain pending with the captain.

The advisory invalidates the retained build/static receipts as fresh-restore
security proof; `patched-restore` and `patched-build` replace that claim. The
retained ordinary .NET and C# coverage failures remain historical evidence,
**not final-candidate results after the runtime-pin change**. Full .NET suite,
.NET static/coverage, hosted CI, native Windows proof and fresh independent review
remain outstanding. The runtime-dependent gates were stopped, not bypassed.

A second blocker is Linux Python coverage: 19 threshold findings remain despite
a passing full suite. `run_tests.py::_end_job_members` has HCRAP 44;
`windows_job.py` has 23.3618% line / 4.31034% branch coverage and 12 HCRAP
findings (22–239.792); `owned_process.py` has 17.9487% line / 0% branch
coverage and HCRAP 29/55. Thresholds remain 80% line, 60% branch and HCRAP 20.
The complete findings are in the Python adapter report. These are not measurement
gaps and are not covered by DYD-507. Owner for routing/disposition: the DYD-504
captain; no broader verification-driver redesign was authorized to this writer.

Historical diagnostic, not base attribution: `retained-python-coverage/threshold-diagnostic.json` replays
the unchanged join and policy against the checksum-verified retained raw coverage
for commit 43a016ba, validating every measured Python source/counter hash. It
produces the identical 19 findings; the old functional failure had prevented
the normal gate from reaching threshold evaluation. This diagnostic is not a gate
pass. `coverage-finding-source-comparison.json` additionally proves every flagged
method byte-identical to original base 42882830 and the entire `owned_process.py`
file unchanged. Neither establishes comparable base coverage or inheritance.
No thresholds, exclusions or native-Windows assertions were weakened.

DYD-507 owns exactly the three lifecycle failures recorded below. Its live issue
was read during this hop: the admiral permits individual feature merges with those
exact inherited failures documented; release landing still requires green. That
disposition does not waive the new runtime prerequisite or any unrelated failure,
threshold or measurement gap. No DYD-507 source was incorporated.

### Successor receipts and reuse

All successor commands have full output, exact argv/cwd, source manifests and patch,
environment, timestamps and actual exit under `dydo/_system/.local/dyd-504-proof/recovery/`.
The final commit binding and per-receipt input differences are in
`recovery/commit.json` and `recovery/input-reuse.json`. Runtimeconfigs and both
exit-150 launch logs are retained. New archives `recovery-commands.tar.gz` and
`recovery-coverage-results.tar.gz` plus `RECOVERY-SHA256SUMS` travel with this
worktree; they do not overwrite either old archive.

| Successor receipt | Exit | Result |
|---|---|---|
| `patched-restore` | 0 | Fresh RID restore, audit enabled, patched packages |
| `patched-build` | 0 | Release solution build, warnings as errors, zero warnings/errors |
| `patched-pin-test` | 0 | Nine workflow/runtime-pin assertions |
| `independent-static` | 1 | Python and Node pass; initial viewer lint fails, superseded by viewer rerun |
| `viewer-stable-static` | 0 | Viewer static, zero findings/gaps |
| `independent-coverage` | 1 | Node passes 91, one Windows skip; initial viewer environment failure superseded |
| `viewer-stable-coverage` | 0 | Viewer 389 pass; coverage thresholds pass, zero findings/gaps |
| `python-stable-coverage` | 1 | 630 tests, zero failures, 60 platform skips; 19 thresholds (inheritance unproven), zero gaps |
| `patched-dydo-launch`, `patched-test-child-launch` | 150 | Missing Microsoft.NETCore.App 10.0.14 x64 |

Only the successful Python/Node rows of the mixed facade runs are reused, never
their failed aggregate. Their measured sources/tests/dependencies did not change
with the subsequent viewer-only correction or documentation updates. The same
scope check binds the build evidence; these changes cannot alter its executable
inputs. Python coverage was repeated on the frozen tree because the first run
overlapped the temporary runtime-pin red overlay; that first run remains diagnostic
evidence, not the final Python result. Python's coverage adapter records the exact full
unittest-discovery argv and suite exit, so that producer's verdict serves as its
ordinary-suite verdict without a duplicate run. The old red Python/viewer/Node
coverage outcomes are superseded, not renamed as passes. `viewer-final-static`
correctly rejected inputs changed by the overlapping pin-red overlay (exit 2);
only `viewer-stable-static` is the final viewer static result. Initial sandbox
attempts lacked native execution/store access and are not used as behavior proof.

### Focused captain recipe

From the worktree root, using the existing locked dependencies:

```bash
node --test --test-reporter=tap npm/test/download.test.cjs
pnpm --dir viewer test src/testSetup.test.ts src/theme/useTheme.test.tsx src/app/App.test.tsx fixtures/fixtures.test.ts
pnpm --dir viewer lint
dotnet restore DynaDocs.csproj --force --no-http-cache -r linux-x64 -warnaserror
dotnet build DynaDocs.sln -c Release --warnaserror
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~ReleaseWorkflowTests
```

The focused .NET assertions use only the existing test host; they do not prove
that dydo children can launch. After authorized runtime provisioning, rerun the
contract's ordinary .NET suite and required full static/coverage gates with
`dydo/_system/.local/static-gates/python/bin/python`, and complete hosted CI.
Until then the launch recipe `dotnet bin/Release/net10.0/dydo.dll --version`
reproduces the missing-runtime blocker, not a product test failure.

code-writer/model: gpt-6-astra (high). No self-review PASS is claimed.

## Retained hop — historical record

## Pre-edit contract

All five checks passed before source edits: branch `DYD-504-modernize-toolchain`;
root `/home/balazs/.t3/worktrees/DynaDocs/t3-7a947183` is the existing isolated
worktree; HEAD equals and descends from `42882830292f44133f796ee3d152cb446ea4091f`;
`git status --porcelain=v1` was empty; planned edits are restricted to coverage
tooling/tests, reached release-workflow assertions, workflow/runtime declarations,
coverage/testing documentation and this proof. No production guard edits are owned.
The owning ticket's execution contract is the governing plan; DR 048 governs assurance.

## Acceptance cases, declared before implementation

- `test_python_lock_pins_are_measured_against_the_installed_distribution`:
  CPython 3.14.4 and the existing locked dependency closure are usable.
- `test_python_runtime_rejects_non_cpython_and_wrong_series`: fail closed on a
  different interpreter or series; native Node/SDK mismatches remain failures.
- `test_tools_name_both_interpreters_and_every_resolved_tool_pin`: Linux and
  Windows local venv paths retain dependency-bearing interpreter identity.
- PythonRuntimeTests: callable, branch and module witnesses retain exact joins on 3.14.
- ReleaseWorkflowTests: CI uses the new exact runtime pins and local assurance environment;
  malformed pins and missing prerequisites remain rejected.
- Existing isolated-runner containment tests: descendants are ended before snapshot removal.
- Final gates: Release build, isolated full .NET suite, all Python tests, every
  configured coverage/static stack. Hosted CI and fresh review belong to the captain.

Raw command logs, source patches and exit receipts are initially under
`/tmp/dyd-504-proof`; final evidence and limitations are recorded below.

## Recovery and final implementation

The replacement code-writer rechecked the supplied branch/root/base on recovery,
inspected all 24 retained tracked modifications and this untracked proof before
editing, and saved `retained.patch`. Recovery explicitly authorized adopting the
dirty tree. No earlier completed receipts survived: `/tmp/dyd-504-proof` and the
coverage results were absent. No old runtime or global default was installed.

Node 26.13.0, .NET SDK 10.0.303 and CPython 3.14.4 were measured locally. Workflow
pins, the viewer engine, assurance declarations and interpreter checks now agree.
The local interpreter uses `bin/python` on Linux and `Scripts/python.exe` on
Windows. The Linux C# campaign uses the existing subreaper mechanism; failed
execution, detached live descendants and unconfirmed cleanup cannot become a
successful campaign. An unconfirmed cleanup retains its snapshot. Windows keeps
its native owner. Reached fixtures now use native absolute paths, model POSIX
signal delivery and file-descriptor scanning, and isolate their repository roots
from shared temporary ancestors.

`uv pip compile` resolved `requirements.txt` against the local CPython 3.14.4
interpreter. Its eight-package output is byte-identical to `requirements.lock`:
colorama 0.4.6, complexipy 8.0.0, coverage 7.16.0, mando 0.7.1, radon 6.0.1,
ruff 0.16.6, six 1.17.0, vulture 2.16. `uv pip check`, the resolved Python/Node/.NET
version collectors, and locked metrics/tool restore all passed. No package was
added or bumped. The initial sandbox cache/network failures are retained beside
the successful task-local-cache re-lock receipt; they are not dependency
incompatibilities.

## Evidence identity

Final executable/configuration/documentation inputs were frozen before the final
runs. Every final runtime-gate receipt names HEAD, argv, cwd, environment, timestamps, exit,
its source patch and every tracked/untracked input's SHA-256. The shared input
manifest digest is
`70d652ba94cf0ba86c8154a36faa91290ec818f73afecc6fa5b0c3c7d91943f9`.
After the runs, only this proof and the `verify-dydo` skill runtime prerequisite
were edited. The prerequisite now reads the existing `.python-version` pin
instead of recommending 3.12. The final integrity receipt explicitly lists those
two documentation deltas; all executable, test, configuration and lock bytes
match the gate inputs. Documentation and canonical-skill checks cover the final
prose changes. No failed or interrupted receipt
is reused as a pass. The earlier coverage run was explicitly canceled with exit
130 before the final fixture corrections; the final coverage run is separate.

Preflight discovered 3,436 .NET test identities, 629 initial Python tests (630
with the added non-Windows rejection test), 384 viewer tests and 13 maintained
Node test files. Native .NET execution expands theory rows to 3,595 cases.

## Focused recipe

From the repository root, with the local dependencies restored:

```bash
py=dydo/_system/.local/static-gates/python/bin/python
PYTHONPATH=DynaDocs.Tests/coverage/tests "$py" -m unittest \
  test_python_runtime.PythonRuntimeTests \
  test_gate_versions.GateVersionTests.test_python_lock_pins_are_measured_against_the_installed_distribution \
  test_runner.LinuxCampaignTests \
  test_runner.WorktreeCleanupTests.test_unconfirmed_cleanup_retains_the_snapshot \
  test_windows_job.WindowsJobPlatformTests \
  test_csharp_coverage.CSharpCoverageTests.test_template_module_selection_rejects_every_unproven_binding \
  test_testing_facade.PortableTestingFacadeTests.test_copied_single_file_preserves_child_streams_exit_deadline_and_cleanup \
  test_testing_facade.PortableTestingFacadeTests.test_default_all \
  test_testing_facade.PortableTestingFacadeTests.test_deferred_test_row_is_interrupted_when_coverage_never_runs \
  test_testing_facade.PortableTestingFacadeTests.test_derivation_only_when_both_rows_are_selected
"$py" DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~ReleaseWorkflowTests
node --test --test-reporter=tap DynaDocs.Tests/coverage/tests/run-host-canaries-args.test.mjs
```

The final Python recipe passes 30 tests, exit 0. The reached workflow suite passes
8 tests, exit 0. In `feature-red`, the current tests against base implementations
fail with `Callable measurement requires pinned CPython 3.12`, rejection of the
3.14.4 declaration, missing Linux owner evidence, and
`Expected 'remove_worktree' to not have been called. Called 1 times.` The same
22 selected tests passed with the implementation (`feature-green`).
`red_recipe.py` and its exact base-file overlay/source hashes are retained.
The full initial Python receipt also records each subsequently repaired fixture
failure; `portability-focused`, `node-path-fixtures` and the final focused/full
runs show their corrected results.

## Cross-scope failures and missing proof

The final ordinary .NET suite has 3,592 passes and three failures, exit 1:

- `GapCheckCommandTests.LauncherCancelsTheRunnerProcessTreeAndWaitsForItsDirectChild`
- `CliScenarioProcessTests.OrphanedPipe_ReportsUnconfirmedCleanupAndRetainsScratch`
- `CliScenarioProcessTests.Timeout_TerminatesAndReapsParentAndDescendant`

All three reproduce in a focused seven-case run of the exact base archive
`42882830292f44133f796ee3d152cb446ea4091f`, using the same Linux subreaper,
interpreter, .NET SDK and build environment (three failed, four passed, exit 1).
Those test sources are byte-identical to the candidate. The gate remains failed;
the captain must route the lifecycle/acceptance-harness repair outside this hop.

The final Python suite has 630 tests, one failure and 60 platform skips, exit 1.
Its failure is the nested Node suite's npm ZIP fixture:
`npm/test/download.test.cjs:125` invokes `tar -a -cf fixture.zip`; GNU tar 1.35
writes a tar archive and UnZip 6.00 rejects it with
`End-of-central-directory signature not found`. The focused base-archive Node
reproduction fails identically, exit 1, and this source too is byte-identical.
The captain must route the npm fixture repair; npm test files are not owned here.
The 52 Win32 owner tests, six Windows control-lifetime tests and two platform
cases are skipped on Linux. The nested Node suite has one existing Windows
executable fixture skip. These are missing Windows proof, not passing Windows
proof. Windows release CI still executes the native owner tests.

Hosted CI and independent review remain the captain's work. No PR, merge,
publication, tag or ticket-status action was performed by this writer.

code-writer/model: gpt-6-astra (high).

## Changed paths

- `.github/workflows/ci.yml`
- `.github/workflows/release.yml`
- `DynaDocs.Tests/Workflow/ReleaseWorkflowTests.cs`
- `DynaDocs.Tests/coverage/.python-version`
- `DynaDocs.Tests/coverage/gate_adapter.py`
- `DynaDocs.Tests/coverage/gate_collect.py`
- `DynaDocs.Tests/coverage/gate_run.py`
- `DynaDocs.Tests/coverage/gate_versions.py`
- `DynaDocs.Tests/coverage/package-lock.json`
- `DynaDocs.Tests/coverage/package.json`
- `DynaDocs.Tests/coverage/python_runtime.py`
- `DynaDocs.Tests/coverage/run_tests.py`
- `DynaDocs.Tests/coverage/tests/run-host-canaries-args.test.mjs`
- `DynaDocs.Tests/coverage/tests/test_control_dydo_lifetime.py`
- `DynaDocs.Tests/coverage/tests/test_csharp_coverage.py`
- `DynaDocs.Tests/coverage/tests/test_gate_adapter.py`
- `DynaDocs.Tests/coverage/tests/test_gate_versions.py`
- `DynaDocs.Tests/coverage/tests/test_javascript_metrics.py`
- `DynaDocs.Tests/coverage/tests/test_python_coverage.py`
- `DynaDocs.Tests/coverage/tests/test_python_metrics.py`
- `DynaDocs.Tests/coverage/tests/test_python_runtime.py`
- `DynaDocs.Tests/coverage/tests/test_runner.py`
- `DynaDocs.Tests/coverage/tests/test_testing_facade.py`
- `DynaDocs.Tests/coverage/tests/test_windows_job.py`
- `DynaDocs.Tests/coverage/windows_job.py`
- `dydo/guides/testing-strategy.md`
- `dydo/reference/coverage-tools.md`
- `viewer/package.json`
- `dydo/project/changelog/2026/2026-11-04/dyd-504-toolchain.md`
- `skills/engineering/verify-dydo/SKILL.md` (runtime prerequisite wording only)

## Final gate receipts

Each named command directory in the retained evidence contains `output.log`,
`receipt.json`, `inputs.json` and `candidate.patch`. The exact argv is in its
receipt. These are complete results, including failures:

| Receipt directory | Command | Exit | Observation |
|---|---|---|---|
| `final-release-build` | `dotnet build DynaDocs.sln -c Release --warnaserror` | 0 | Zero warnings/errors |
| `final-dotnet-suite` | `<venv-python> DynaDocs.Tests/coverage/run_tests.py` | 1 | 3,592 passed, three lifecycle failures |
| `final-python-suite` | `<venv-python> -m unittest discover -s DynaDocs.Tests/coverage/tests -p 'test_*.py'` | 1 | 630 tests, one npm-fixture failure, 60 platform skips |
| `final-static-gate` | `<venv-python> DynaDocs.Tests/coverage/gap_check.py gate static` | 0 | Four rows passed; zero findings/gaps in each |
| `final-coverage-gate` | `<venv-python> DynaDocs.Tests/coverage/gap_check.py gate coverage` | 1 | Four functional failures; zero measurement gaps |

The static facade result is
`DynaDocs.Tests/coverage/results/run-1793749611411-7875687d/result.json`.
The coverage facade result is
`DynaDocs.Tests/coverage/results/run-1793749589394-a67d0d81/result.json`.
The C# coverage owner at
`results/assurance/run-e4c59b3b666b4f7cbab22135b31c6dc3/raw-5ffd8bc3a3b6428b93f6bb84773dfca0/identities.json`
records `complete: true`, `cleanup_confirmed: true`, Linux subreaper containment,
CPython `[3,14,4]`, and subject status 1. The C# row names the same three lifecycle
failures. Python and Node coverage fail on the npm ZIP fixture. The functional
failures prevent a passing coverage/HCRAP threshold claim.

Viewer coverage reports 375 passed and nine failed tests, exit 1. The unchanged
`viewer/src/theme/useTheme.test.tsx:42` and `viewer/src/app/App.test.tsx:262` call
`localStorage.clear()` while Node 26 exposes an undefined global `localStorage`
without a storage-file option. The exact base source with a local copy of the
same locked dependencies reproduces all nine failures and 375 passes
(`viewer-base-local-deps`, exit 1). This is a target-runtime compatibility gap in
unchanged setup, not proof that Node 22 failed. A first comparison using a
cross-root dependency symlink also hit Vite filesystem restrictions; that
noncomparable receipt is retained but is not the attribution evidence.
The captain must authorize or route viewer test-environment repair: the owned
viewer scope here covers runtime declarations/locks, not `viewer/src/testSetup.ts`.
The installed pnpm lock snapshot matches `viewer/pnpm-lock.yaml` byte for byte
(SHA-256 `7397354b4b176ab77d6cc00308801919bceb383987e7e4444f97080b96332b6d`).

## Retained evidence

Persistent worktree-local artifacts are under
`dydo/_system/.local/dyd-504-proof/`. `commands.tar.gz` retains all command
receipts, full logs, input manifests, patches, the recovery diff, base source
identity manifest, and the red-overlay recipe/source. `coverage-results.tar.gz`
retains the complete native producer/consumer results tree. `SHA256SUMS` binds
the archives. These ignored local artifacts must travel with the review handoff
before the implementation worktree is removed; this committed proof remains the
durable index. The original command logs also remain at `/tmp/dyd-504-proof`.

The captain owns disposition of the three cross-scope repair needs and hosted
CI, including the missing native Windows evidence. All failed gates stay failed.

## Related

- [Coverage tools](../../../../reference/coverage-tools.md)
- [Testing strategy](../../../../guides/testing-strategy.md)
