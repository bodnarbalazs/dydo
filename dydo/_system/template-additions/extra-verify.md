Run .NET tests through the worktree-isolated runner, never `dotnet test` directly; pass test
arguments after `--`.

A hop proves the tests its change reaches:

```bash
python DynaDocs.Tests/coverage/run_tests.py -- --filter "<the tests the change reaches>"
```

The gate the Captain names — the Issue's final gates, a merge, the landing — proves the whole set
with one command:

```bash
python DynaDocs.Tests/coverage/gap_check.py --force-run
```

`--force-run` drives the test, static and coverage rows of every stack in
`DynaDocs.Tests/coverage/gap_check.json`; the dotnet test row invokes this same isolated runner
with no filter, and the python and node rows run their own suites, so the full suites run once
each.

A non-zero exit blocks completion at whichever of these you owe; report the exact failure rather
than working around it.
