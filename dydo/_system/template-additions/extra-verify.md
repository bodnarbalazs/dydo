Run .NET tests through the worktree-isolated runner, never `dotnet test` directly:

```bash
python DynaDocs.Tests/coverage/run_tests.py
python DynaDocs.Tests/coverage/gap_check.py --force-run
```

Pass test arguments after `--`. Either command returning non-zero blocks completion; report the exact
failure rather than working around it.
