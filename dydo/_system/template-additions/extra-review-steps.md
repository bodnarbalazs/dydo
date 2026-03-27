4. **Coverage gate** — Verify tier compliance

```bash
python DynaDocs.Tests/coverage/gap_check.py
```

This runs tests with coverage collection and checks results. gap_check automatically skips tests when no source or test files have changed since the last run — use `--force-run` to override. If gap_check exits non-zero, the review is a FAIL — coverage regressions are not negotiable. Include the gap_check output in your review feedback so the code-writer knows exactly what to fix.
