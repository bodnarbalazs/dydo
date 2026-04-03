5. **Run tests** — Use the worktree-isolated runner

```bash
python DynaDocs.Tests/coverage/run_tests.py
```

This runs `dotnet test` in a temporary git worktree, avoiding DLL lock contention when multiple agents test concurrently. Do **not** run `dotnet test` directly.

Pass extra args after `--`: `python DynaDocs.Tests/coverage/run_tests.py -- --filter FullyQualifiedName~MyTest`

6. **Coverage gate** — Verify tier compliance

```bash
python DynaDocs.Tests/coverage/gap_check.py
```

This runs tests with coverage collection and checks results against tier thresholds. gap_check automatically skips tests when no source or test files have changed since the last run. Use `--force-run` to override this and always run tests.

Exit code 0: you're clear.
Non-zero: you have coverage regressions. Use `--inspect <pattern>` to see what's failing, then add or improve tests until it passes. If a tier assignment seems wrong, ask the human — don't adjust tiers yourself.

**Do not proceed to Complete until gap_check passes with zero failures.**

There is no such thing as a "pre-existing" or "unrelated" failure. If gap_check fails, the review fails — full stop. It does not matter whether the code-writer's change caused the failure or not. The gap_check must be green before you move on.

If a failure appears genuinely unrelated to the task, do **not** release or work around it. Report the failure to the user or orchestrator and wait for guidance. Another agent working on a different part of the codebase may have already fixed it, or someone will be dispatched to address it.
