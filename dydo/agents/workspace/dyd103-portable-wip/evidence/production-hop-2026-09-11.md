# DYD-103 production hop evidence — 2026-09-11

Captain: issue-captain/model: deepseek-v4.1-flash (effective identity unavailable).
Branch `DYD-103-mutation-gate`, base `83c89856e6cccc52cdff876ec520eeac3b74d0db`,
worktree `.worktrees/dyd103-prod`, pushed `77157b1c..3743c9cb`.

## Hops

| SHA | Hop | What |
|---|---|---|
| `e12b5b3d` | merge | reviewed specification + preparation steps 1-6 onto the post-DYD-130/164 feature base |
| `48f08293` | implement | step 7 serial edits + consumed-producer re-pin |
| `52c6e626` | specify | gate 9 reconciliation (DYD-164 text adopted verbatim) |
| `0c140d70` | docs | `testing-strategy.md` and `coverage-tools.md` mutation sections |
| `3743c9cb` | specify | gate 4 pinned-interpreter propagation |

## Re-pin of the consumed DYD-96 interface at `83c89856`

| Path | pin blob | feature blob | Verdict |
|---|---|---|---|
| `inventory.py` | `c5ab1c66` | `c5ab1c66` | unchanged |
| `windows_job.py` | `5b9ae1cb` | `5b9ae1cb` | unchanged; `execution_seconds_maximum` on `validate`/`run` |
| `gate_inventory.py` | `34c6e45c` | `0260f3cb` | consumed API preserved |
| `run_tests.py` | `c023fea2` | `02b9e609` | DYD-164 removed `--coverage`; snapshot functions unchanged |
| `gate_adapter.py` | `ef8c20a2` | `5bf94e98` | `_candidate` 2-tuple unchanged; `_inventory_artifact` now 3-tuple — reconciled |
| `gap_check.py` / example | `a65ccdef` | `0f8fe6d1` | freshness + 0/1/2/130 mapping intact; `--force-run` still never selects mutation |
| `test-associations.json` | `3c79a53e` | `2 rows added` | `mutation_adapter.py`, `mutation_summary.py` |

## Gates (candidate `3743c9cb` unless noted)

| # | Command | Exit | Result |
|---|---|---|---|
| 1 | `$P -m unittest discover -s DynaDocs.Tests/coverage/tests -p "test_mutation*.py"` | 0 | 163 tests OK (after the re-pin fix) |
| 4 | `$env:PYTHON="$P"; $P DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~MutationAssurance` | 0 | 65/65 passed |
| 5 | `dotnet build DynaDocs.sln -c Release --warnaserror` | 0 | 0 warnings, 0 errors |
| 6 | `dotnet bin/Release/net10.0/dydo.dll check` | 0 | 0 errors, 0 warnings, 821 files |
| 7 | `$P DynaDocs.Tests/coverage/gap_check.py capabilities` | 0 | mutation configured on all three stacks |
| 2 | `$P -m unittest discover -s DynaDocs.Tests/coverage/tests -p "test_*.py"` | 1 | 616 tests, 16F/21E, all in pre-existing DYD-96 collector modules missing host tool deps; zero in `test_mutation*` |
| 3 | `$P DynaDocs.Tests/coverage/run_tests.py -- --verbosity minimal` | — | not run |
| 8 | `$P DynaDocs.Tests/coverage/gap_check.py gate mutation --since 2e31b1d0915529926a79224424c18620ee8003e1` | — | not run (widened real campaigns infeasible in session) |
| 9 | `$P DynaDocs.Tests/coverage/gap_check.py --force-run` | — | not run |
| 10 | `$P -m unittest discover -s DynaDocs.Tests/coverage/mutation/probes -p "test_replay.py"` | — | not runnable: `mutation/probes/test_replay.py` absent |

$P = `C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe` (3.12.14).

## Engine restores proven this session

`dotnet tool restore` → dotnet-stryker 4.16.0, altcover 9.0.102; `npm --prefix
DynaDocs.Tests/coverage/mutation ci --ignore-scripts` → StrykerJS 9.6.1; venv
`dydo/_system/.local/mutation/python` + `pip install --no-deps -r .../requirements.lock` → cosmic-ray 8.7.0.
All three verified by their listed commands.

## Remaining before acceptance

1. Author `DynaDocs.Tests/coverage/mutation/probes/test_replay.py` (gate 10 subjects).
2. Gate 9 `--force-run` (one full G; the interim route retires since DYD-166 merged).
3. Gate 3 full isolated .NET suite.
4. Gate 8 final M `--since 2e31b1d0...`; retain the three summaries and raw reports.
5. Hardener if refactored, then fresh independent CODE review, then the PR to
   `feature/dydo-3-consolidation` and DYD-131.
