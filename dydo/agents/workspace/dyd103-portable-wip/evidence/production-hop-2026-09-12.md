# DYD-103 production hop evidence — 2026-09-12 (gate 10)

Captain: issue-captain/model: deepseek/deepseek-v4.1-flash (effective identity unavailable).
Branch `DYD-103-mutation-gate`, base `83c89856e6cccc52cdff876ec520eeac3b74d0db`,
worktree `.worktrees/dyd103-prod`, HEAD at commit time `c8234037` plus this hop.

## Gate 10 — authored and green on the pinned engines

New owned path `DynaDocs.Tests/coverage/mutation/probes/test_replay.py` (gate 10): three probes,
one per engine, each building a throwaway Git repository from data-string subjects and driving the
completed adapter's CLI seam
(`mutation_adapter.py --stack <name> --since <base> --root <subject> --output <summary>`) with the
real Stryker.NET 4.16.0, StrykerJS 9.6.1 and Cosmic Ray 8.7.0. The probe assets are linked into the
subject by directory junction and removed without following the link, so no engine is copied.

Per engine, weak-assertion subject → survived 1 / adapter exit 1; strong-assertion subject → killed 1,
score 100, adapter exit 0. Concurrency witnesses asserted in the retained raw stdout:
`Stryker will use a max of 1 parallel testsessions.` (dotnet) and
`ConcurrencyTokenProvider Creating 1 test runner process(es).` (node).

Red before green: the first authoring runs failed (subject builder bugs, then a `widened` reading
from an avoidable test-file change, refactored to one subject per variant). Controlled red after
green: with the Cosmic Ray venv renamed away, the python probe failed with adapter exit 2 and
`survived 0`; restored, it passed. A stub could not pass the gate this way.

## Gate results (candidate `c8234037` + this hop; `$P` = bundled CPython 3.12.14)

| # | Command | Exit | Result |
|---|---|---|---|
| 1 | `$P -m unittest discover -s DynaDocs.Tests/coverage/tests -p "test_mutation*.py"` | 0 | 163 tests OK |
| 4 | `$env:PYTHON="$P"; $P DynaDocs.Tests/coverage/run_tests.py -- --verbosity minimal --filter FullyQualifiedName~MutationAssurance` | 0 | 65/65 passed |
| 5 | `dotnet build DynaDocs.sln -c Release --warnaserror` | 0 | 0 warnings, 0 errors |
| 6 | `dotnet bin/Release/net10.0/dydo.dll check` | 0 | 0 errors, 0 warnings, 821 files |
| 7 | `$P DynaDocs.Tests/coverage/gap_check.py capabilities` | 0 | mutation configured on all three stacks |
| 10 | `$P -m unittest discover -s DynaDocs.Tests/coverage/mutation/probes -p "test_replay.py"` | 0 | 3 tests OK in 42.5 s, real engines |
| 3 | `$env:PYTHON="$P"; $P DynaDocs.Tests/coverage/run_tests.py -- --verbosity minimal` | 1 | Failed 2, Passed 2482, Skipped 2, Total 2486 — both `test_active_manifest`, below |
| 9 | `$P DynaDocs.Tests/coverage/gap_check.py --force-run` | — | not run: blocked by the gate-3 conflict, see below |
| 8 | `$P DynaDocs.Tests/coverage/gap_check.py gate mutation --since 2e31b1d0…` | 2 | aggregate 2, three rows invalid/2 — see below |

Engine restores proven this session in the worktree: `dotnet tool restore` (dotnet-stryker 4.16.0),
`npm --prefix DynaDocs.Tests/coverage/mutation ci --ignore-scripts` (StrykerJS 9.6.1), venv
`dydo/_system/.local/mutation/python` + `pip install --no-deps -r …/requirements.lock` (cosmic-ray
8.7.0), each verified by its listed command.

## Blocker 1 — gate 3: DYD-103's owned manifest edit breaks a DYD-113 contract probe

DYD-103 spec step 7 (owned `DynaDocs.Tests/coverage/gap_check.json`) flips each stack's `mutation`
row from `{"state":"unavailable","reason":"Pending DYD-103"}` to the configured adapter row. The
DYD-113 probe `DynaDocs.Tests/coverage/tests/test_testing_facade.py::test_active_manifest`
(`TestingFacadeTests` and `PortableTestingFacadeTests`, bound to the feature scenario *DynaDocs uses
its real adapters and admits missing assurance*) still hard-asserts
`unavailable('Pending DYD-103')`, so it now fails in both classes.

That file is not in DYD-103's `owned-paths.json`; `DynaDocs.Tests/Features/testing-facade.feature`
is explicitly `not-owned` ("DYD-113 contract; cited only"). Precedent: when DYD-96 configured the
`static`/`coverage` rows, its implement hop `0d3f0995` updated exactly this probe's assertions. The
feature prose still says "static and coverage are unavailable pending DYD-96", so the probe, not the
prose, is authoritative here. The one-line probe update (assert the configured mutation row per
stack, as `test_mutation_facade.test_each_stack_mutation_row_names_the_adapter_and_base_placeholder`
already does) is the mechanical consequence, but it edits a DYD-113 contract probe and so needs the
admiral's authorization or a DYD-113 amendment. Gate 9 cannot pass while this fails, because its
coverage rows run the same suites.

## Blocker 2 — gate 8: the adapter rejects DYD-96's real `native-evidence-fixture` origin shape

Gate 8 returned aggregate 2 with all three stacks `invalid` (child exit 2) against candidate
`c8234037`; the adapter never reached an engine. All three summaries carry the same gap:

```
invalid inventory: excluded row
dydo/agents/workspace/dyd96-portable-wip/native-altcover-evidence/cfg-probe/Program.cs
```

Cause: `mutation_adapter.INVENTORY_ORIGIN_KEYS["native-evidence-fixture"]` pins
`{"manifest", "manifestSha256"}`, but DYD-96's producer (`gate_inventory._fixture_exclusion`,
`gate_inventory.py:71-73`) emits `{"manifest", "entry", "sha256", "manifestSha256"}`. The
specification's `excluded` origin field table (specification.md:150-151) pins the same two keys, so
this is a **spec return** — a consumed interface field departed — not a silent adapter edit. The
fixture file is tracked in the candidate, so every stack is refused before selection. `derived-copy`
origins match the spec. The exact correction is the four producer keys in both the spec field table
and `INVENTORY_ORIGIN_KEYS`; a fresh specifier/review is owed before production resumes.

Gate 8 evidence: `DynaDocs.Tests/coverage/results/run-1789204185142-fc25b499/result.json`
(aggregateExit 2), and the three `results/adapters/<stack>-mutation.json` summaries.

## Resume state

- Branch `DYD-103-mutation-gate`, base `83c89856`, worktree `.worktrees/dyd103-prod`; gate-10 hop
  pushed to origin.
- Gate 10 is done and green. Gates 1/4/5/6/7 green. Gate 3 red for blocker 1 (2 failures, both
  `test_active_manifest`). Gate 9 not run (same root cause). Gate 8 aggregate 2 for blocker 2.
- Next: resolve blocker 2 (bounded spec amendment pinning the producer's four `native-evidence-fixture`
  origin keys, then the one-line `INVENTORY_ORIGIN_KEYS` fix) and obtain authorization for blocker 1's
  DYD-113 probe update. Then rerun gate 8 (`--since 2e31b1d0…`, detached) and gate 9, and gate 3.
- No campaign is left running; no mutation lock or snapshot remains.
