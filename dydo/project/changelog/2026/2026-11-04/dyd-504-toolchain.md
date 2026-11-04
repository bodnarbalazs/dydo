---
area: project
type: changelog
date: 2026-11-04
---

# DYD-504 toolchain modernization

## Distribution successor — current disposition, 2026-11-04

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

**Python coverage still fails with exactly 19 inherited threshold findings and
zero gaps, exit 1.** The checksum-verified replay on 43a016ba and byte-identical
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

Attribution evidence: `retained-python-coverage/threshold-diagnostic.json` replays
the unchanged join and policy against the checksum-verified retained raw coverage
for commit 43a016ba, validating every measured Python source/counter hash. It
produces the identical 19 findings; the old functional failure had prevented
the normal gate from reaching threshold evaluation. This diagnostic is not a gate
pass. `coverage-finding-source-comparison.json` additionally proves every flagged
method byte-identical to original base 42882830 and the entire `owned_process.py`
file unchanged. No thresholds, exclusions or native-Windows assertions were weakened.

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
| `python-stable-coverage` | 1 | 630 tests, zero failures, 60 platform skips; 19 inherited thresholds, zero gaps |
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
