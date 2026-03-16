4. **Coverage gate** — Verify tier compliance on the changed code

```bash
python DynaDocs.Tests/coverage/gap_check.py --skip-tests
```

Tests were already run in step 3. Check that gap_check passes (exit code 0).

If it fails, include the specific failures in your review feedback. Coverage regressions are a FAIL.
