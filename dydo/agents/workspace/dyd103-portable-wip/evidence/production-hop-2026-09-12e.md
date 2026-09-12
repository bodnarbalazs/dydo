# DYD-103 gate 8 on corrected base 83c89856 — 2026-09-12 (admiral ruling 3)

Captain: issue-captain/model: deepseek/deepseek-v4.1-flash (effective identity unavailable).
Branch `DYD-103-mutation-gate`, frozen candidate `f54faeef2c13850dda424ee9432b406f60f4b1f1`,
base for this gate-8 reading `83c89856e6cccc52cdff876ec520eeac3b74d0db`.

## Baseline greening — done (host-local, reversible)

See `../baseline-greening-2026-09-12.md`: coverage Node modules via `npm ci --prefix
DynaDocs.Tests/coverage`; the DYD-96 collector venv `dydo/_system/.local/static-gates/python`
recreated with pinned CPython 3.12.14 + `requirements.lock`; `dotnet restore`/Debug build;
DYD-96 retained G fixtures restored into the git-ignored `results/`. The two mutation baselines
the adapter uses are now green: python `unittest discover -s DynaDocs.Tests/coverage/tests -p
"test_*.py"` exit 0 (637 tests), node `node_tests.cjs` exit 0.

## Other gates on the candidate

- Gate 3 (full isolated .NET suite): green (Passed 2484 / Skipped 2 / Total 2486) on the content;
  the later owned change is one sentence of `coverage-tools.md`, which cannot affect it.
- Gate 6 `dydo check` after the docs edit: 0 errors, 0 warnings. Gate 5 build: 0/0.
- Gate 10 replay probe: green (3 real-engine tests).
- Gate 9 `--force-run` (each suite once, mutation never selected): aggregate 2. dotnet and node
  coverage/test rows PASS; the two 2s are DYD-96 static-collector environment —
  `dotnet static` reports `No package metadata was found for colorama` (the `versions` collector
  runs in-process under the gate interpreter and requires the pinned python packages importable
  there) and `python coverage` produces no artifact. These are not the mutation baseline and not
  DYD-103 changes.

## Gate 8 — cannot complete through the facade; aggregate 130

Command: `$P DynaDocs.Tests/coverage/gap_check.py gate mutation --since 83c89856e6cccc52cdff876ec520eeac3b74d0db`,
detached (PID 63876, started 2026-09-12T14:44:52+02:00). It stopped after about half an hour:

```
dotnet mutation: INTERRUPTED (child exit 1): row deadline exceeded after adapter cleanup
Aggregate: 130
```

Cause: the facade bounds every row by `gap_check.py`'s own
`EXECUTION_SECONDS_MAXIMUM = 1800` plus `CLEANUP_SECONDS = 30` (`gap_check.py:28-29, 530`,
`wait_for_child` line 299-310). At the 1830 s deadline it sends CTRL_BREAK and, after only 30 s of
grace, kills the adapter; the interrupted dotnet Stryker.NET campaign lost its lock and published
no summary (the dead lock was removed by the operator after confirming no owner process remained).
The identical constant is present at the feature head `c751a629`, so DYD-131 does not change it.

DYD-103's spec sanctions `execution_seconds_maximum=14400` on every `windows_job.run` call, and
this Issue's own config changes (`gap_check.json`, `.config/dotnet-tools.json`,
`test-associations.json`) widen all three stacks, as the spec's Plan step 9 states. A widened
Stryker.NET campaign at concurrency 1 does not finish inside 1830 s, so the spec's own gate 8 — a
multi-hour campaign — is unreachable through the consumed facade. This is a DYD-96-owned consumed
interface (`gap_check.py` is not-owned, "no edit") and cannot be corrected inside DYD-103.

Evidence in this directory: `gate-mutation-result.json` (aggregate 130),
`interrupted-dotnet-run-files.txt` (the interrupted adapter run), `stdout.log`, `stderr.log`,
`pid.txt`, `candidate.txt`, `base.txt`.
