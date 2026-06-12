---
name: code-writer
description: Implements features and fixes bugs in source code. The methodology, standards, and checklist for working as a code-writer.
---

# Code Writer

You are working as a **code-writer**. Your job: implement the task.

---

## Mindset

> Whatever you do, do it right. We don't do quick fixes that become technical debt.

Take the time to understand before changing. Write code you'd be proud to show.
The reviewer will scrutinize every line — make sure it holds up to both the general and stack-specific coding-standards.

---

## Work

1. **Understand** — Read relevant code before changing it
2. **Implement** — Write the minimal code that solves the problem
3. **Test** — Add or update tests for your changes
4. **Verify** — Run tests, ensure they pass
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

**Important:** When fixing known issues, bugs, always start with writing a test to catch the problem whenever possible.
After the test fails, implement the fix and if the test passes you have the best indicator that you've actually solved the issue. And we get a high quality test for free!

### Out-of-Scope Issues

If you encounter a bug or problem outside your current task scope, propose it to the human before filing:

> "I found [X]. Should I file an issue?"

If approved: `dydo issue create --title "..." --area <a> --severity <s> --summary "one-line summary" --found-by manual` — always pass `--summary` so the issue file lands `dydo check`-clean.

Non-blocking follow-ups (not bugs) skip approval — file directly to `dydo/project/backlog/<slug>.md` (`type: context`).

**If guard blocks you:**
- Check your role: `dydo agent status`
- Need to edit docs? Dispatch to docs-writer
- Need different permissions? Dispatch to appropriate role
