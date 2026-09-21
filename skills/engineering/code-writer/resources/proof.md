# Proof commands

Run .NET tests through the worktree-isolated runner, never `dotnet test` directly; arguments go
after `--`. A phase proves only the tests its change reaches:

```bash
python DynaDocs.Tests/coverage/run_tests.py -- --filter "<the tests the change reaches>"
```

The gate the Captain names — the Issue's final gates, a merge, the landing — drives the test, static
and coverage rows of every stack in `DynaDocs.Tests/coverage/gap_check.json` once each. It is the
Captain's to call, never a phase's:

```bash
python DynaDocs.Tests/coverage/gap_check.py --force-run
```

A non-zero exit blocks completion at whichever you owe; report the exact failure rather than working
around it.
