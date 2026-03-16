5. **Coverage gate** — Run the gap check to verify tier compliance

```bash
python DynaDocs.Tests/coverage/gap_check.py --skip-tests
```

Tests were already run in step 4. This analyzes the existing coverage data.

- If it fails: fix coverage gaps before dispatching for review
- If a module is newly below threshold: either add tests or (if the tier is wrong) discuss with the human before changing it
- Use `--inspect <pattern>` to drill into specific modules
