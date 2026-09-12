# DYD-103 gates 3/9/8 on frozen candidate 3b9ba6d3 — 2026-09-12

Captain: issue-captain/model: deepseek/deepseek-v4.1-flash (effective identity unavailable).
Branch `DYD-103-mutation-gate`, base `83c89856e6cccc52cdff876ec520eeac3b74d0db`,
frozen candidate `3b9ba6d3c0440a8015bb7ecbc3564a959411e678`, worktree `.worktrees/dyd103-prod`.

## Gate 3 — full isolated .NET suite — PASS

`$env:PYTHON="$P"; $P DynaDocs.Tests/coverage/run_tests.py -- --verbosity minimal`
→ exit 0: Passed 2484, Skipped 2, Total 2486. The last inline facade mutation row is now
configured (admiral ruling 2); the `Schema 1 has one small concrete manifest and result shape`
scenario passes.

## Gate 9 — `--force-run` — PASS (identical before/after; mutation never selected)

Plan identity is structural: `git diff 83c89856 3b9ba6d3 -- DynaDocs.Tests/coverage/gap_check.py`
is empty, and the `gap_check.json` diff is exactly the three `mutation` rows (which `--force-run`
never selects).

Base leg run in a throwaway worktree at `83c89856`; candidate leg at `3b9ba6d3`. Row tuples
`(stack, capability, state, childExit, resultExit)` are identical, `selectedStacks` identical,
no `mutation` row selected in either, aggregate 2 in both:

```
SAME (dotnet,test,passed,0,0)          (dotnet,static,invalid,2,2)      (dotnet,coverage,passed,0,0)
SAME (python,test,invalid,None,2)      (python,static,invalid,2,2)      (python,coverage,invalid,1,2)
SAME (node,test,failed,1,1)            (node,static,invalid,2,2)        (node,coverage,failed,1,1)
```

The nonzero rows are pre-existing DYD-96 environment: the static interpreter
`dydo/_system/.local/static-gates/python` and the DYD-96 coverage tooling are not installed in this
worktree, so `static` is invalid/2 and python/node `coverage` fail identically on both legs.
Evidence: `gate9-candidate-stdout.log`, `gate9-candidate-result.json`.

## Gate 8 — final M — FAIL (aggregate 2; never a pass)

`$P DynaDocs.Tests/coverage/gap_check.py gate mutation --since 2e31b1d0915529926a79224424c18620ee8003e1`
on `3b9ba6d3` → aggregate 2, three rows invalid/2. Causes, all prerequisites the spec names and
none in DYD-103's adapter logic:

1. **dotnet** — `mode: widened`, reason `deleted or renamed source` (base `2e31b1d0` predates
   DYD-96/DYD-191). `changedTargets` includes the 19 `DynaDocs.Tests/coverage/metrics/*.cs` files
   of `GateMetrics.csproj`, which the spec names as the unselectable C# project
   (`no .NET test project route`), so the widened dotnet campaign refuses with 57 gaps. The spec:
   "GateMetrics C# is unselectable (2), which binds any Project-level M whose base predates DYD-96."
2. **python** — `baseline test run failed (exit 1)`: the python stack's manifest suite is the unit
   suite, which is red with the pre-existing DYD-96 collector failures (Ran 637 tests, 14 failures,
   20 errors; e.g. `test_python_metrics`, `test_csharp_*`, `test_knip`, `test_source_tokens`,
   `test_diagnostics`, `test_python_coverage`). The spec's Python baseline rule is nonzero → 2.
3. **node** — `baseline test run failed (exit 1)`: `node DynaDocs.Tests/coverage/node_tests.cjs`
   reports 30 tests, 7 fail, pre-existing DYD-96 node tooling.

This is the spec's unmet production prerequisite ("plus an integrated passing baseline") and its
DYD-105 condition ("a whole-M pass additionally needs DYD-105 merged"), not a DYD-103 code defect.
The inventory consumer-drift fix is proven: no `invalid inventory` gap appears; the runs now reach
selection and engine launch. Raw evidence: `dotnet-mutation.json`, `python-mutation.json`,
`node-mutation.json`, the three `*-run-report.json`, the two baseline stdout logs,
`gate-mutation-result.json`, `facade-stdout.log`.

## Outcome

Gate 10 green; gates 1/3/4/5/6/7 green; gate 9 satisfied (identical rows/aggregate, mutation never
selected); gate 8 aggregate 2 for the unmet DYD-96/DYD-105 prerequisites above. No independent
`reviewer(code)` brief is prepared and no PR is opened, because gate 8 has not passed. Candidate
pushed at `3b9ba6d3`; no campaign running; no mutation lock or snapshot remains.
