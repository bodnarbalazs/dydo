---
area: project
type: changelog
date: 2026-11-04
---

# DYD-504 toolchain modernization

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
