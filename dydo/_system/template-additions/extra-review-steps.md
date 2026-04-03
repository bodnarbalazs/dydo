4. **Run tests** — Use the worktree-isolated runner

```bash
python DynaDocs.Tests/coverage/run_tests.py
```

Do **not** run `dotnet test` directly — use the worktree runner to avoid DLL lock contention.

5. **Coverage gate** — Verify tier compliance

```bash
python DynaDocs.Tests/coverage/gap_check.py
```

This runs tests with coverage collection and checks results. gap_check automatically skips tests when no source or test files have changed since the last run — use `--force-run` to override.

**If gap_check exits non-zero, the review is a FAIL.** There is no such thing as a "pre-existing" or "unrelated" failure. It does not matter whether the code-writer's change caused the failure or not — the gap_check must be green for the review to pass.

If a failure appears genuinely unrelated to the task, do **not** pass the review or release. Report the failure to the user or orchestrator and wait for guidance. Another agent working on a different part of the codebase may have already fixed it, or someone will be dispatched to address it.

Include the gap_check output in your review feedback so the code-writer knows exactly what to fix.
