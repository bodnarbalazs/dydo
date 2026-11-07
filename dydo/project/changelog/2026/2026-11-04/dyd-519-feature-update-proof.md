---
area: project
type: changelog
date: 2026-11-04
---

# DYD-519 — feature update and combined merge proof

One local feature-to-ticket merge updates `DYD-504-modernize-toolchain` under
the DYD-519 contract dated `2026-11-04T15:17:56.347Z`. The merge candidate is
`2a1ec33a7e6880d40464067a555cbc1a5e0f5d17`, with message
`DYD-519 merge: update toolchain candidate to feature tip`.

## Pins, conflict map and scope

- First parent, target: `da8d0f059d22ca364cf9eac8806eee9376a31b98`.
- Second parent, source `origin/feature/dydo-3-6-0`:
  `14c43837133cc1d5d103f7c413b606324b9a063d`.
- Common base: `42882830292f44133f796ee3d152cb446ea4091f`.
- Merge tree: `6666f1df5bc5774025921e44345380396758278f`.

The existing isolated worktree was clean on the pinned target branch; the source
ref and common ancestry matched. `git merge-tree --write-tree` returned the tree
above without conflicts before `git merge --no-ff` created the two-parent commit.
Every hop remains reachable. No checkout, squash, rebase or manual resolution was
needed. Required test adapters created and removed their own temporary snapshots.

The source contributes exactly 13 files, 563 insertions and 37 deletions: the
reviewed DYD-499 install nudge, command-specific warning markers, migration,
tests and documentation. Its path set has no overlap with the target's 37
changed paths since the common base. All 13 source files match the source blobs
verbatim; all 37 target files match the target blobs. `premerge.json`, `merge.json`
and both parent patches retain the complete path map and checks. The shared
behavioral seam is the guard/adoption code executing through the modernized
runtime and assurance machinery, covered by the whole-suite combined gates.

No guard, ShellReader, statechart or `owned_process.py` authoring occurred.
Node 26.13.0, SDK 10.0.303, CPython 3.14.4 and the runtime minimum 10.0.14 remain
unchanged. No new runtime/package or global default was installed or changed.
DYD-514, DYD-516 and the round-four deferred nits remain untouched.

## Fresh evidence on the merge SHA

Evidence lives in the ignored worktree directory
`DynaDocs.Tests/coverage/results/dyd-519-merge/`. Each command directory contains
the exact argv, cwd, start/end times, actual exit, complete output, tracked-input
SHA-256 manifest and before/after status. All final runtime gates ran with a clean
tree at the merge SHA and reported no input changes. The shared input-manifest
SHA-256 is `e0102dfbe5365052ea11a57ea226f713a32e9f91c922264ac205b9ac4484764d`;
the native adapter source fingerprint is
`72b5086e682e5462f26da4c28f9545bb9f336fd29ce30492df6407782b5f736f`.

Preflight verified the installed Python and Node dependency closures, metrics
NuGet lock, viewer installed lock, native runtime versions and patched AOT package
versions. Native discovery found 3,749 .NET identities, including 24 workflow
cases; 647 Python tests; 69 cancellation-selection tests; 389 viewer tests; and
13 maintained Node test files. Every configured facade capability validated.

| Gate / receipt | Exit | Observed result |
|---|---|---|
| `audited-restore` | 0 | Fresh solution restore, audit enabled in all mode, warnings as errors |
| `release-build` | 0 | Release solution build, zero warnings/errors |
| `workflow-tests` | 0 | 24 passed |
| `cancellation-tests` | 0 | 69 total, 21 passed, 48 Windows-platform skips |
| Full .NET, coverage producer | 1 | 3,746 passed; exactly three DYD-507 failures; no skips |
| Full Python, coverage producer | 0 | 647 total, 591 passed, 56 platform skips |
| `combined-coverage-isolated` | 1 | .NET functional failure and exactly 18 DYD-518 findings; Node/viewer passed; zero measurement gaps |
| `combined-static` | 0 | All four stacks passed, zero findings/gaps |
| `merge-docs` | 0 | `dydo check`: 849 files, zero errors/warnings |

The coverage producers execute the full .NET suite through `run_tests.py
--assurance-output` and the full Python suite through `-m unittest discover -s
DynaDocs.Tests/coverage/tests -p test_*.py`. Their native suite exits satisfy the
ordinary-suite obligations under the testing strategy's producer/verdict reuse
rule; no second successful full-suite execution was launched. This reuse is
within the merge candidate, never across the merge boundary. Viewer coverage ran
all 389 tests successfully. Full native logs retain Node's suite result and its
platform skip.

Commands run from the repository root, using
`dydo/_system/.local/static-gates/python/bin/python` as `<py>`:

```text
dotnet restore DynaDocs.sln --force --no-http-cache -warnaserror -p:NuGetAudit=true -p:NuGetAuditMode=all
dotnet build DynaDocs.sln -c Release --warnaserror
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~ReleaseWorkflowTests
<py> -m unittest -v test_windows_cancellation test_windows_job test_gate_adapter.CoverageCancellationTests
<py> DynaDocs.Tests/coverage/gap_check.py gate coverage
<py> DynaDocs.Tests/coverage/gap_check.py gate static
dydo check
```

The build disables shared compiler/MSBuild server reuse through process-local
environment variables. Cancellation uses
`PYTHONPATH=DynaDocs.Tests/coverage/tests` and a task-owned evidence destination.
Final coverage uses `TMPDIR`, `TMP` and `TEMP` set to
`/var/tmp/dyd519-native-2a1ec33a`; static uses separate
`/var/tmp/dyd519-static-2a1ec33a`. Exact environment prefixes are in the receipts.

### Temporary-root diagnosis and the interrupted attempt

An empty `/tmp/.git` directory predated this task (mtime
`2026-11-04T14:47:56.993562+00:00`). The portable facade resolves its repository
root by walking ancestors, so fixtures under `/tmp` wrote receipts there instead
of inside their own directory. The first .NET campaign failed 28 cases: the three
known lifecycle cases and 25 fixture-root failures. Its exact identities and raw
logs are retained. Python then encountered the same environment problem, so the
owning facade was interrupted; the aggregate remains exit 130.

The unchanged failing Node conformance fixture passed with task-local temp storage
under `/var/tmp`, outside any Git ancestor. The corrected full campaign then
eliminated all 25 additional .NET failures and passed the full Python suite.
No source fix or shared `/tmp/.git` deletion was used. The first .NET owner
confirmed cleanup, its snapshot and Git registration disappeared, and the
interrupted facade exited. `environment-diagnosis.json`, `retained-aborted/` and
`interrupted-cleanup.json` preserve this distinction; the interrupted attempt is
not passing evidence. The final .NET owner also reports complete execution and
confirmed Linux-subreaper cleanup.

## Exact remaining failures

DYD-507's three failures match by full test identity, with no additions or removals:

- `DynaDocs.Tests.Commands.GapCheckCommandTests.LauncherCancelsTheRunnerProcessTreeAndWaitsForItsDirectChild`
- `DynaDocs.Tests.Steps.CliScenarioProcessTests.OrphanedPipe_ReportsUnconfirmedCleanupAndRetainsScratch`
- `DynaDocs.Tests.Steps.CliScenarioProcessTests.Timeout_TerminatesAndReapsParentAndDescendant`

The native errors remain process-absence/reaping assertions and the retained
orphaned-pipe probe. The .NET gate stays **FAILED**; coverage/HCRAP thresholds
remain **unestablished** because functional failures short-circuit them.

DYD-518's 18 findings match the retained reviewed report by exact path, gate and
member identity, including line/column suffixes. `python-identity-comparison.json`
and `retained-coverage/summary.json` record empty added/removed sets:

| Path | Failed measurements / exact members |
|---|---|
| `DynaDocs.Tests/coverage/run_tests.py` | HCRAP `_end_job_members:610:0` = 44 |
| `DynaDocs.Tests/coverage/windows_job.py` | Line 35.924933%, branch 24.193548%; HCRAP `members:175:0` = 33, `retain_members:188:0` = 32, `poll_pipe:224:0` = 47, `close_pipe:245:0` = 43, `make_pipe:267:0` = 22, `pipes:294:0` = 29, `launch:326:0` = 40, `finish:348:0` = 236, `wait_campaign:369:0` = 113, `supervise:386:0` = 55, `native_run:407:0` = 31 |
| `skills/engineering/verify-dydo/scripts/owned_process.py` | Line 17.948718%, branch 0%; HCRAP `supervise:16:0` = 29, `run_owned:64:0` = 55 |

Thresholds remain line 80%, branch 60%, HCRAP 20, with no new exclusions, tiers or
scope expansion. Python coverage stays **FAILED**. The recorded dispositions allow
these exact failures for feature integration only; DYD-507 and DYD-518 still block
release landing. This proof makes no independent-review PASS claim.

## Captain recipe and retained artifacts

The captain can inspect `git show --no-patch --format=raw
2a1ec33a7e6880d40464067a555cbc1a5e0f5d17` and `merge.json` for parents, tree and
verbatim-path comparisons. Then, from this worktree with existing locked tools:

```bash
mkdir -p /var/tmp/dyd519-captain-proof
export TMPDIR=/var/tmp/dyd519-captain-proof
export TMP=/var/tmp/dyd519-captain-proof
export TEMP=/var/tmp/dyd519-captain-proof
py=dydo/_system/.local/static-gates/python/bin/python
dotnet build DynaDocs.sln -c Release --warnaserror
dotnet test DynaDocs.Tests/DynaDocs.Tests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~ReleaseWorkflowTests
PYTHONPATH=DynaDocs.Tests/coverage/tests "$py" -m unittest -v test_windows_cancellation test_windows_job test_gate_adapter.CoverageCancellationTests
dydo check dydo/project/changelog/2026/2026-11-04/dyd-519-feature-update-proof.md
```

Verify that the scratch directory and its ancestors have no `.git` before running.
Expected focused results are 24 workflow passes and 21 cancellation passes with
48 platform skips. Skips are missing native Windows proof, not Windows passes.
Round-four Claude PASS and hosted Windows run 41854869189/job 126660643741 belong
to parent `da8d0f05`; they do not establish Windows proof on this merge SHA.

The retained `retained-coverage/` and `retained-static/` directories hold the exact
facade and adapter reports. `native-artifacts.tar.gz` retains their immutable
producer directories plus the interrupted attempt. `native-artifacts-manifest.json`
and `SHA256SUMS` bind the native inputs, reports, full logs and local receipts.
`final-integrity.json` records the final proof commit, source equality and cleanup.
These ignored artifacts must travel with the handoff before this worktree is removed.

This document is committed separately after runtime gates; the only post-merge
tracked change is this proof. Documentation validation covers it without rerunning
unchanged executable inputs. The captain owns push/link, hosted checks and a fresh
merge review. PR 227's later integration into feature is a separate Merge
Sub-ticket; no push, PR edit, watch, release, tag or publication was performed here.

code-writer/model: gpt-6-astra (high).

## Related

- [Testing strategy](../../../../guides/testing-strategy.md)
- [Coverage tools](../../../../reference/coverage-tools.md)
- [DYD-504 historical toolchain proof](dyd-504-toolchain.md)
