4. **Coverage gate** — Verify tier compliance

```bash
python DynaDocs.Tests/coverage/gap_check.py
```

This runs tests with coverage collection and checks results. Only use `--skip-tests` if gap_check already ran tests in this session — a plain `dotnet test` does not produce coverage data. If gap_check exits non-zero, the review is a FAIL — coverage regressions are not negotiable. Include the gap_check output in your review feedback so the code-writer knows exactly what to fix.
