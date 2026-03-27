5. **Coverage gate** — Verify tier compliance

```bash
python DynaDocs.Tests/coverage/gap_check.py
```

This runs tests with coverage collection and checks results against tier thresholds. gap_check automatically skips tests when no source or test files have changed since the last run. Use `--force-run` to override this and always run tests.

Exit code 0: you're clear.
Non-zero: you have coverage regressions. Use `--inspect <pattern>` to see what's failing, then add or improve tests until it passes. If a tier assignment seems wrong, ask the human — don't adjust tiers yourself.

Do not proceed to Complete until gap_check passes.
