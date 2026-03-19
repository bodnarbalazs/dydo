5. **Coverage gate** — Verify tier compliance

```bash
python DynaDocs.Tests/coverage/gap_check.py --skip-tests
```

Tests already ran. This checks the existing coverage data against tier thresholds.

Exit code 0: you're clear.
Non-zero: you have coverage regressions. Use `--inspect <pattern>` to see what's failing, then add or improve tests until it passes. If a tier assignment seems wrong, ask the human — don't adjust tiers yourself.

Do not proceed to Complete until gap_check passes.
