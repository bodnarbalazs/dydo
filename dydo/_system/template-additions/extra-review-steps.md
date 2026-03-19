4. **Coverage gate** — Verify tier compliance

```bash
python DynaDocs.Tests/coverage/gap_check.py --skip-tests
```

Tests already ran. If gap_check exits non-zero, the review is a FAIL — coverage regressions are not negotiable. Include the gap_check output in your review feedback so the code-writer knows exactly what to fix.
