# DYD-103 baseline greening — 2026-09-12 (admiral ruling 3)

Captain: issue-captain/model: deepseek/deepseek-v4.1-flash (effective identity unavailable).
Branch `DYD-103-mutation-gate`, base `83c89856e6cccc52cdff876ec520eeac3b74d0db`, worktree
`.worktrees/dyd103-prod`. All changes here are host-local and reversible except the one owned docs
sentence recorded under "Owned change".

## The exact baselines the adapter runs

Read from the adapter, not assumed: `_manifest_argv("python")` and `_manifest_argv("node")` read
`gap_check.json`'s per-stack `capabilities.test.command`. So the applicable baselines are:

- python: `$P -m unittest discover -s DynaDocs.Tests/coverage/tests -p "test_*.py"` (run in-process
  by the generated suite runner under `sys.executable`).
- node: `node DynaDocs.Tests/coverage/node_tests.cjs`.

Neither is narrower than the manifest's own vendor command; there is no per-target narrowing in the
adapter to use.

## Node

Enumerated 7 `node_tests.cjs` failures, all `Cannot find module 'eslint'` /
`.../node_modules/knip/dist/reporters/json.js`. Install: `npm ci --prefix DynaDocs.Tests/coverage
--ignore-scripts` (the tracked `package.json`/`package-lock.json`: c8, dependency-cruiser, eslint,
eslint-plugin-sonarjs, istanbul-lib-instrument, jscpd, knip). Result: exit 0, 0 fail.

## Python

First run: 7 failures, 17 errors. Enumerated two causes:

1. The DYD-96 collector interpreter `dydo/_system/.local/static-gates/python` was absent (and the
   `-adoption`/main copies are CPython 3.13.1, which `python_runtime.CallableWitness` refuses:
   "Callable measurement requires pinned CPython 3.12 monitoring semantics"). Recreated with the
   pinned `$P` (CPython 3.12.14) and installed `DynaDocs.Tests/coverage/requirements.lock`
   (complexipy 8.0.0, coverage 7.16.0, radon 6.0.1, ruff 0.16.6, vulture 2.16).
2. `DynaDocs.Tests/coverage/metrics/GateMetrics.csproj` had no restored assets. `dotnet restore
   DynaDocs.sln` + `dotnet build DynaDocs.sln -c Debug`.

Second run: 4 failures, 3 errors. Remaining causes:
- Three tests replay untracked DYD-96 retained G artifacts (`results/native-g-20260909-hop3-05`,
  `-hop3-06`, `results/assurance/run-3510c46004a847c4b24a94e4866b6743`), which no fresh checkout
  has. Restored the exact directories from the `integration` worktree (read-only copy; host-local,
  git-ignored).
- `test_owned_policy_docs_replace_tiers_with_the_dr048_gate_set` requires the phrase
  `no surviving or uncovered changed-code mutants` in `dydo/reference/coverage-tools.md`.

Result after all of the above: `unittest discover … -p "test_*.py"` exit 0, 637 tests, no failures
or errors; `node_tests.cjs` exit 0.

## Owned change

`dydo/reference/coverage-tools.md` (DYD-103-owned mutation section): the policy sentence now reads
"no surviving or uncovered changed-code mutants remain — no changed-code mutant survives or is left
uncovered", the phrase the DYD-113 owned-policy probe requires. Nothing else changed.

## Note for DYD-96 / the host

The three retained-artifact tests depend on non-committed `results/` directories from DYD-96's
2026-09-09 G run; they cannot pass on a clean checkout unless those artifacts are present. This is a
DYD-96 test-state gap, recorded rather than fixed here (DYD-96 production code was not touched).
