5. **Coverage gate** — Verify tier compliance

```bash
python DynaDocs.Tests/coverage/gap_check.py
```

This runs tests with coverage collection and checks results against tier thresholds. Only use `--skip-tests` if gap_check already ran tests in this session — a plain `dotnet test` does not produce coverage data.

Exit code 0: you're clear.
Non-zero: you have coverage regressions. Use `--inspect <pattern>` to see what's failing, then add or improve tests until it passes. If a tier assignment seems wrong, ask the human — don't adjust tiers yourself.

Do not proceed to Complete until gap_check passes.
